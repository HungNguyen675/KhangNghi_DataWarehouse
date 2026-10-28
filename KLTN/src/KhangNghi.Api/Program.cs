using System;
using System.Text;
using KhangNghi.Api;
using KhangNghi.Core.Configuration;
using KhangNghi.Infrastructure.Data;
using KhangNghi.Infrastructure.Repositories;
using KhangNghi.Infrastructure.Services;
using KhangNghi.Infrastructure.Services.Copilot;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Nạp .env (API key Google Gemini / secrets) trước khi đọc cấu hình ứng dụng
var dotEnvPath = DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Database Connection
var connectionString = builder.Configuration.GetConnectionString("DwhConnection") 
    ?? "Server=localhost\\SQLEXPRESS;Database=DWH_KhangNghi;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;";

builder.Services.AddSingleton<IDatabaseConnectionFactory>(_ => new DatabaseConnectionFactory(connectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton(builder.Configuration.GetSection("EtlSettings").Get<EtlSettingsOptions>() ?? new EtlSettingsOptions());
builder.Services.AddScoped<ISsasProcessingService, SsasProcessingService>();
builder.Services.AddScoped<IEtlOrchestratorService, EtlOrchestratorService>();
builder.Services.AddScoped<IEtlService, EtlService>();
builder.Services.AddScoped<IDwhAnalyticsRepository, DwhAnalyticsRepository>();
builder.Services.AddScoped<ISalesForecastService, SalesForecastService>();
builder.Services.AddScoped<ISsasRepository, SsasRepository>();

// Cấu hình AI Copilot (Google Gemini qua Google AI Studio API hoặc tương thích OpenAI)
var geminiKey = builder.Configuration["GEMINI_API_KEY"] ?? builder.Configuration["OPENAI_API_KEY"] ?? string.Empty;
var geminiModel = builder.Configuration["GEMINI_MODEL"] ?? builder.Configuration["OPENAI_MODEL"] ?? LlmOptions.DefaultModel;
var geminiBaseUrl = builder.Configuration["GEMINI_BASE_URL"] ?? builder.Configuration["OPENAI_BASE_URL"] ?? LlmOptions.DefaultBaseUrl;
var geminiTimeout = int.TryParse(builder.Configuration["GEMINI_TIMEOUT_SECONDS"] ?? builder.Configuration["OPENAI_TIMEOUT_SECONDS"], out var t) ? t : LlmOptions.DefaultTimeoutSeconds;
var geminiRetries = int.TryParse(builder.Configuration["GEMINI_MAX_RETRIES"], out var r) ? Math.Clamp(r, 0, 2) : 1;

var llmOptions = new LlmOptions
{
    BaseUrl = geminiBaseUrl,
    ApiKey = geminiKey,
    Model = geminiModel,
    TimeoutSeconds = geminiTimeout,
    MaxRetries = geminiRetries
};
builder.Services.AddSingleton(llmOptions);
// Hạn chót thật nằm ở ngân sách thời gian trong LlmCopilotService; HttpClient.Timeout chỉ là chốt chặn dự phòng
builder.Services.AddHttpClient<ILlmCopilotService, LlmCopilotService>(client => client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds + 5));

// Cấu hình JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "KhangNghi_DWH_Super_Secret_Security_Key_2026_Graduation_Thesis!";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// Ma trận phân quyền RBAC phía Backend
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DirectorOrAdmin", policy => policy.RequireRole("Admin", "Director"));
    options.AddPolicy("SalesAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager", "SalesStaff"));
    options.AddPolicy("PurchaseAccess", policy => policy.RequireRole("Admin", "Director", "PurchaseManager", "PurchaseStaff"));
    options.AddPolicy("InventoryAccess", policy => policy.RequireRole("Admin", "Director", "InventoryManager", "InventoryStaff"));
    options.AddPolicy("ShippingAccess", policy => policy.RequireRole("Admin", "Director", "ShippingStaff", "ShippingManager"));
    options.AddPolicy("ProductAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager", "SalesStaff", "PurchaseManager", "PurchaseStaff", "InventoryManager", "InventoryStaff"));
    options.AddPolicy("GeographyAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager", "SalesStaff", "ShippingStaff", "ShippingManager"));
});

// Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Khang Nghi DWH Analytics & ML API",
        Version = "v1",
        Description = "Hệ thống API phân tích hiệu quả hoạt động kinh doanh và dự báo doanh số Công ty Khang Nghị (40 câu hỏi phân tích & ML.NET)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT Token: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.Logger.LogInformation("AI Copilot (Google Gemini): {Status}", llmOptions.IsConfigured
    ? $"BẬT - model {llmOptions.Model} @ {llmOptions.BaseUrl}, chờ tối đa {llmOptions.TimeoutSeconds}s, retry 503 tối đa {llmOptions.MaxRetries} lần (.env: {dotEnvPath ?? "biến môi trường"})"
    : "TẮT - chưa cấu hình GEMINI_API_KEY (copy .env.example thành .env)");

// Khởi tạo tài khoản mặc định
if (builder.Configuration.GetValue<bool>("Bootstrap:EnsureDefaultUsers") || true)
{
    using var scope = app.Services.CreateScope();
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    await userRepo.EnsureDefaultUsersAsync();
}

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Khang Nghi Analytics API v1");
    c.RoutePrefix = "swagger";
});

// Cho phép phục vụ Web App tĩnh từ wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fallback về trang chủ cho SPA/Web Dashboard
app.MapFallbackToFile("index.html");

app.Run();
