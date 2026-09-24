using System;
using System.Text;
using KhangNghi.Infrastructure.Data;
using KhangNghi.Infrastructure.Repositories;
using KhangNghi.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Database Connection
var connectionString = builder.Configuration.GetConnectionString("DwhConnection") 
    ?? "Server=localhost\\SQLEXPRESS;Database=DWH_KhangNghi;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;";

builder.Services.AddSingleton<IDatabaseConnectionFactory>(_ => new DatabaseConnectionFactory(connectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEtlService, EtlService>();
builder.Services.AddScoped<IDwhAnalyticsRepository, DwhAnalyticsRepository>();
builder.Services.AddScoped<ISalesForecastService, SalesForecastService>();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DirectorOrAdmin", policy => policy.RequireRole("Admin", "Director"));
    options.AddPolicy("InventoryAccess", policy => policy.RequireRole("Admin", "Director", "InventoryManager"));
    options.AddPolicy("ShippingAccess", policy => policy.RequireRole("Admin", "Director", "ShippingStaff"));
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
