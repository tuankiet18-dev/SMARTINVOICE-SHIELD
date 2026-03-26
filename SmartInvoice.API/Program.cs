using System.Reflection;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using SmartInvoice.API.Constants;
using SmartInvoice.API.Data;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Repositories.Implementations;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Security;
using SmartInvoice.API.Services;
using SmartInvoice.API.Services.Implementations;
using SmartInvoice.API.Services.Interfaces;

using System.Security.Claims;
using SmartInvoice.API.Middleware;
// DotNetEnv logic removed since we now use parameter store

var builder = WebApplication.CreateBuilder(args);

// Load local .env file for AWS credentials if it exists
if (File.Exists(".env"))
{
    foreach (var line in File.ReadAllLines(".env"))
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

// Load AWS Systems Manager Parameter Store
builder.Configuration.AddSystemsManager("/SmartInvoice/dev/");

// 1. Kết nối PostgreSQL
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString =
        $"Host={builder.Configuration["POSTGRES_HOST"]};Port={builder.Configuration["POSTGRES_PORT"]};Database={builder.Configuration["POSTGRES_DB"]};Username={builder.Configuration["POSTGRES_USER"]};Password={builder.Configuration["POSTGRES_PASSWORD"]}";
}

var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AppDbContext>(
    (sp, options) => options.UseNpgsql(sp.GetRequiredService<Npgsql.NpgsqlDataSource>())
);

// 2. Kết nối AWS S3
// (Nó sẽ tự tìm AWS Credentials trong máy bạn ở ~/.aws/credentials hoặc biến môi trường)
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<SmartInvoice.API.Services.StorageService>();

// 3. Đăng ký Repositories & Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();

// 4. Đăng ký Services
builder.Services.AddScoped<IOcrClientService, OcrClientService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IInvoiceProcessorService, InvoiceProcessorService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ILocalBlacklistService, LocalBlacklistService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IAwsS3Service, AwsS3Service>();
builder.Services.AddScoped<IExportConfigService, ExportConfigService>();
builder.Services.AddScoped<IExportService, ExportService>();

// Add HttpClient for Services to use (like VietQR API calls)
builder.Services.AddHttpClient();

// Add Memory Cache
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISystemConfigProvider, SystemConfigProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// Register Internal OCR Client
// Note: OCR Python runs on host machine (not Docker), so use host.docker.internal
var ocrApiEndpoint = builder.Configuration["OCR_API_ENDPOINT"] ?? "http://host.docker.internal:5000";
builder.Services.AddHttpClient<IOcrClientService, OcrClientService>(client =>
{
    client.BaseAddress = new Uri(ocrApiEndpoint);
});

// ==================== VIETQR SERVICE WITH RESILIENCE POLICIES ====================
// Register VietQR HttpClient with Polly resilience policies:
// - Timeout: 5 seconds per request
// - Retry: 3 attempts with exponential backoff (1s, 2s, 4s) for 429 and 5xx errors
// - Circuit Breaker: Break after 5 consecutive failures, stay broken for 1 minute
builder
    .Services.AddHttpClient("VietQR")
    .AddTransientHttpErrorPolicy(p =>
        p.Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                {
                    // Exponential backoff: 2^attempt seconds (1s, 2s, 4s)
                    var delaySeconds = Math.Pow(2, attempt);
                    return TimeSpan.FromSeconds(delaySeconds);
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[VietQR Retry] Attempt {retryCount} after {timespan.TotalSeconds}s"
                    );
                }
            )
    )
    .AddTransientHttpErrorPolicy(p =>
        p.Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (outcome, timespan, context) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[VietQR Circuit Breaker] Circuit opened for {timespan.TotalMinutes} minutes"
                    );
                },
                onReset: (context) =>
                {
                    System.Diagnostics.Debug.WriteLine("[VietQR Circuit Breaker] Circuit reset");
                }
            )
    )
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)));

// Register VietQR Service
builder.Services.AddScoped<IVietQrClientService, VietQrClientService>();

// ==================== END VIETQR CONFIGURATION ====================

// Configure Custom Claims Transformer
builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformer>();

// ==================== AWS SERVICES CONFIGURATION ====================
// 5. Config AWS Cognito & SQS
var awsOptions = builder.Configuration.GetAWSOptions();
if (awsOptions.Region == null)
{
    awsOptions.Region = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS_REGION"] ?? "ap-southeast-1");
}
builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddAWSService<IAmazonSQS>();

// ==================== SQS PUBLISHER & BACKGROUND CONSUMER ====================
// Register SQS message publisher for VietQR validation requests (Required by InvoiceService)
builder.Services.AddScoped<ISqsMessagePublisher, SqsMessagePublisher>();

// Register VietQR SQS Consumer as a hosted background service (Commented out to prevent OCR message theft)
// builder.Services.AddHostedService<VietQrSqsConsumerService>();
// ==================== END SQS CONFIGURATION ====================

// ==================== OCR WORKER CONFIGURATION ====================
// Generic SQS service used by both the upload endpoint (producer) and OCR worker (consumer)
builder.Services.AddScoped<ISqsService, SqsService>();

// Named HttpClient for OCR Worker → calls Python OCR at localhost:5000
builder.Services.AddHttpClient("OcrWorker", client =>
{
    client.BaseAddress = new Uri(ocrApiEndpoint);
    client.Timeout = TimeSpan.FromMinutes(5); // Increased from 3m to 5m for batch stability
});

// Background worker that polls SQS OCR queue, downloads from S3, calls OCR API, updates DB
builder.Services.AddHostedService<OcrWorkerService>();
// ==================== END OCR WORKER CONFIGURATION ====================



// 7. Config Authentication (Cognito)
var region = builder.Configuration["AWS_REGION"] ?? builder.Configuration["AWS_DEFAULT_REGION"] ?? "ap-southeast-1";
var userPoolId = builder.Configuration["COGNITO_USER_POOL_ID"];
var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;

    // Explicitly set MetadataAddress to ensure .NET finds the AWS Cognito signing keys
    options.MetadataAddress = $"{authority}/.well-known/openid-configuration";

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, 
        
        ValidIssuers = new[] { authority, $"{authority}/" }, 
        
        ValidateAudience = false, 
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true, 
        
        RoleClaimType = "custom:role" 
    };
});

// 8. Config Authorization Policies based on Permissions
// We iterate over the constants in the Permissions class and dynamically create a requirement
builder.Services.AddAuthorization(options =>
{
    var permissionFields = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string));

    foreach (var field in permissionFields)
    {
        var permissionValue = field.GetRawConstantValue()?.ToString();
        if (!string.IsNullOrEmpty(permissionValue))
        {
            // Dùng RequireAssertion để chấp nhận quyền cụ thể HOẶC quyền "*"
            options.AddPolicy(
                permissionValue,
                policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c =>
                            c.Type == "Permission" && (c.Value == permissionValue || c.Value == "*")
                        )
                    )
            );
        }
    }
});

// 6. Config CORS
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   ?? new[] { "http://localhost:3000", "https://main.d3nvvjzg8ojoqd.amplifyapp.com" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAmplify",
        policyBuilder =>
        {
            policyBuilder.WithOrigins(allowedOrigins)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials(); // Important for cookies/auth if needed
        });
});

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Dòng này giúp bỏ qua lỗi vòng lặp (Cycle) khi 2 bảng trỏ qua lại
        options.JsonSerializerOptions.ReferenceHandler = System
            .Text
            .Json
            .Serialization
            .ReferenceHandler
            .IgnoreCycles;
    });

// Swagger để test API
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Description =
                "JWT Authorization header using the Bearer scheme. \r\n\r\nNhập 'Bearer' [khoảng trắng] và chuỗi token của bạn.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
        }
    );

    c.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc, null),
            new List<string>()
        },
    });
});

var app = builder.Build();

// Auto-migrate Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Sử dụng Polly để thử lại tối đa 5 lần, mỗi lần cách nhau 3 giây
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 5,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(3),
            onRetry: (exception, timeSpan, attempt, context) =>
            {
                logger.LogWarning($"[Auto-Migrate] Database chưa sẵn sàng, đang thử lại lần {attempt} sau {timeSpan.TotalSeconds}s... Lỗi: {exception.Message}");
            });

    await retryPolicy.ExecuteAsync(async () =>
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        Console.WriteLine("Database migration applied successfully.");

        // Seed basic document types if missing
        if (!context.Set<DocumentType>().Any())
        {
            context.Set<DocumentType>().AddRange(
                new DocumentType { TypeCode = "GTGT", TypeName = "Hóa đơn giá trị gia tăng", IsActive = true, DisplayOrder = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new DocumentType { TypeCode = "SALE", TypeName = "Hóa đơn bán hàng", IsActive = true, DisplayOrder = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();
            Console.WriteLine("Seeded initial DocumentTypes.");
        }
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled for local Docker dev to prevent port issues

app.UseCors("AllowAmplify");

app.UseMiddleware<MaintenanceMiddleware>();

app.UseAuthentication();
app.UseMiddleware<SmartInvoice.API.Middlewares.TenantStatusMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
