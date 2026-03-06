using Amazon.S3;
using Amazon.CognitoIdentityProvider;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.API.Data;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Repositories.Implementations;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Services.Implementations;
using SmartInvoice.API.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SmartInvoice.API.Services;
using Microsoft.AspNetCore.Authentication;
using SmartInvoice.API.Security;
using System.Reflection;
using SmartInvoice.API.Constants;
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
    connectionString = $"Host={builder.Configuration["POSTGRES_HOST"]};Port={builder.Configuration["POSTGRES_PORT"]};Database={builder.Configuration["POSTGRES_DB"]};Username={builder.Configuration["POSTGRES_USER"]};Password={builder.Configuration["POSTGRES_PASSWORD"]}";
}

var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<Npgsql.NpgsqlDataSource>()));

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

// Add HttpClient for Services to use (like VietQR API calls)
builder.Services.AddHttpClient();

// Add Memory Cache
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// Register Internal OCR Client
var ocrApiEndpoint = builder.Configuration["OCR_API_ENDPOINT"] ?? "http://localhost:8000";
builder.Services.AddHttpClient<IOcrClientService, OcrClientService>(client =>
{
    client.BaseAddress = new Uri(ocrApiEndpoint);
});

// Configure Custom Claims Transformer
builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformer>();

// 5. Config AWS Cognito
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();



// 7. Config Authentication (Cognito)
var region = builder.Configuration["AWS_REGION"];
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
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = authority,
        ValidateAudience = false, // Cognito Access Token often doesn't contain audience, Id Token does.
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
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
            options.AddPolicy(permissionValue, policy => policy.RequireClaim("Permission", permissionValue));
        }
    }
});



// 6. Config CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials(); // Important for cookies/auth if needed
        });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Dòng này giúp bỏ qua lỗi vòng lặp (Cycle) khi 2 bảng trỏ qua lại
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Swagger để test API
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\nNhập 'Bearer' [khoảng trắng] và chuỗi token của bạn.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc, null),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        Console.WriteLine("Database migration applied successfully.");

        // Seed basic document types if missing
        if (!context.Set<DocumentType>().Any())
        {
            context.Set<DocumentType>().AddRange(
                new DocumentType
                {
                    TypeCode = "GTGT",
                    TypeName = "Hóa đơn giá trị gia tăng",
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new DocumentType
                {
                    TypeCode = "SALE",
                    TypeName = "Hóa đơn bán hàng",
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
            Console.WriteLine("Seeded initial DocumentTypes.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
        // In production, you might want to stop the app if DB fails, but for now we log it.
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled for local Docker dev to prevent port issues

app.UseCors(x => x
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(origin => true) // Allow any origin
    .AllowCredentials());

// app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();



app.Run();