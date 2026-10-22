using System;
using System.Text;
using KhangNghi.Api;
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

// Nạp .env (API key LLM...) trước khi đọc cấu hình
var dotEnvPath = DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Database Connection
var connectionString = builder.Configuration.GetConnectionString("DwhConnection") 
    ?? "Server=localhost\\SQLEXPRESS;Database=DWH_KhangNghi;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;";

builder.Services.AddSingleton<IDatabaseConnectionFactory>(_ => new DatabaseConnectionFactory(connectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEtlService, EtlService>();
builder.Services.AddScoped<IDwhAnalyticsRepository, DwhAnalyticsRepository>();
builder.Services.AddScoped<ISalesForecastService, SalesForecastService>();
builder.Services.AddScoped<ISsasRepository, SsasRepository>();

// Cấu hình tầng 3 AI Copilot: LLM tương thích OpenAI gọi tool (đọc từ .env / biến môi trường)
var llmOptions = new LlmOptions
{
    BaseUrl = string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_BASE_URL"]) ? "https://api.openai.com/v1" : builder.Configuration["OPENAI_BASE_URL"]!,
    ApiKey = builder.Configuration["OPENAI_API_KEY"] ?? string.Empty,
    Model = builder.Configuration["OPENAI_MODEL"] ?? string.Empty,
    TimeoutSeconds = int.TryParse(builder.Configuration["OPENAI_TIMEOUT_SECONDS"], out var llmTimeout) ? llmTimeout : 60
};
builder.Services.AddSingleton(llmOptions);
builder.Services.AddHttpClient<ILlmCopilotService, LlmCopilotService>(client => client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds));

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

// Ma trận phân quyền phía Backend - khớp với ROLE_PERMISSIONS của Web BI (wwwroot/js/authService.js)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DirectorOrAdmin", policy => policy.RequireRole("Admin", "Director"));
    options.AddPolicy("SalesAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager"));
    options.AddPolicy("PurchaseAccess", policy => policy.RequireRole("Admin", "Director", "PurchaseManager"));
    options.AddPolicy("InventoryAccess", policy => policy.RequireRole("Admin", "Director", "InventoryManager"));
    options.AddPolicy("ShippingAccess", policy => policy.RequireRole("Admin", "Director", "ShippingStaff"));
    // Trang Sản phẩm (Kinh doanh, Mua hàng, Kho) và trang Địa lý (Kinh doanh, Logistics) dùng chung API /sales
    options.AddPolicy("ProductAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager", "PurchaseManager", "InventoryManager"));
    options.AddPolicy("GeographyAccess", policy => policy.RequireRole("Admin", "Director", "SalesManager", "ShippingStaff"));
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

app.Logger.LogInformation("AI Copilot tầng 3 (LLM): {Status}", llmOptions.IsConfigured
    ? $"BẬT - model {llmOptions.Model} @ {llmOptions.BaseUrl} (.env: {dotEnvPath ?? "biến môi trường"})"
    : "TẮT - chưa cấu hình OPENAI_MODEL (copy .env.example thành .env)");

// Khởi tạo tài khoản mặc định
using (var scope = app.Services.CreateScope())
{
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
