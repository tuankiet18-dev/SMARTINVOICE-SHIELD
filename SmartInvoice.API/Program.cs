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
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using SmartInvoice.API.Services;
using Microsoft.AspNetCore.Authentication;
using SmartInvoice.API.Security;

// Load .env file
// Load .env file (if exists, mainly for local dev without Docker)
if (File.Exists(".env"))
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// 1. Kết nối PostgreSQL
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = $"Host={Env.GetString("POSTGRES_HOST")};Port={Env.GetString("POSTGRES_PORT")};Database={Env.GetString("POSTGRES_DB")};Username={Env.GetString("POSTGRES_USER")};Password={Env.GetString("POSTGRES_PASSWORD")}";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// Add HttpClient for Services to use (like VietQR API calls)
builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailService, MockEmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Internal OCR Client
var ocrApiEndpoint = Env.GetString("OCR_API_ENDPOINT") ?? builder.Configuration["OcrApiEndpoint"] ?? "http://localhost:8000";
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
var region = Env.GetString("AWS_REGION");
var userPoolId = Env.GetString("COGNITO_USER_POOL_ID");
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

builder.Services.AddControllers();
// Swagger để test API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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