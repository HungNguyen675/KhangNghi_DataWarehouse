using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Data;

namespace KhangNghi.Infrastructure.Repositories
{
    public interface IUserRepository
    {
        Task<SysUser?> GetByUsernameAsync(string username);
        Task<SysUser?> GetByIdAsync(int userId);
        Task<IEnumerable<SysUser>> GetAllAsync();
        Task<int> CreateUserAsync(SysUser user, string plainPassword);
        Task<bool> UpdateUserAsync(SysUser user, string? newPlainPassword = null);
        Task<bool> DeleteUserAsync(int userId);
        Task EnsureDefaultUsersAsync();
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;

        public UserRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<SysUser?> GetByUsernameAsync(string username)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM dbo.Sys_User WHERE Username = @Username";
            return await db.QueryFirstOrDefaultAsync<SysUser>(sql, new { Username = username });
        }

        public async Task<SysUser?> GetByIdAsync(int userId)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM dbo.Sys_User WHERE UserID = @UserID";
            return await db.QueryFirstOrDefaultAsync<SysUser>(sql, new { UserID = userId });
        }

        public async Task<IEnumerable<SysUser>> GetAllAsync()
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = "SELECT * FROM dbo.Sys_User ORDER BY UserID ASC";
            return await db.QueryAsync<SysUser>(sql);
        }

        public async Task<int> CreateUserAsync(SysUser user, string plainPassword)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            user.CreatedAt = DateTime.Now;

            using var db = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO dbo.Sys_User (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                VALUES (@Username, @PasswordHash, @FullName, @Email, @Role, @IsActive, @CreatedAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            return await db.ExecuteScalarAsync<int>(sql, user);
        }

        public async Task<bool> UpdateUserAsync(SysUser user, string? newPlainPassword = null)
        {
            using var db = _connectionFactory.CreateConnection();
            string sql;
            if (!string.IsNullOrWhiteSpace(newPlainPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPlainPassword);
                sql = @"
                    UPDATE dbo.Sys_User 
                    SET FullName = @FullName, Email = @Email, Role = @Role, IsActive = @IsActive, PasswordHash = @PasswordHash
                    WHERE UserID = @UserID";
            }
            else
            {
                sql = @"
                    UPDATE dbo.Sys_User 
                    SET FullName = @FullName, Email = @Email, Role = @Role, IsActive = @IsActive
                    WHERE UserID = @UserID";
            }

            var affected = await db.ExecuteAsync(sql, user);
            return affected > 0;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var db = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM dbo.Sys_User WHERE UserID = @UserID";
            var affected = await db.ExecuteAsync(sql, new { UserID = userId });
            return affected > 0;
        }

        public async Task EnsureDefaultUsersAsync()
        {
            try
            {
                using var db = _connectionFactory.CreateConnection();
                const string createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sys_User')
                    BEGIN
                        CREATE TABLE dbo.Sys_User (
                            UserID INT IDENTITY(1,1) PRIMARY KEY,
                            Username NVARCHAR(100) NOT NULL UNIQUE,
                            PasswordHash NVARCHAR(255) NOT NULL,
                            FullName NVARCHAR(255) NULL,
                            Email NVARCHAR(255) NULL,
                            Role NVARCHAR(50) NOT NULL DEFAULT 'Admin',
                            IsActive BIT NOT NULL DEFAULT 1,
                            CreatedAt DATETIME DEFAULT GETDATE()
                        );
                    END";
                await db.ExecuteAsync(createTableSql);

                // Tạo hoặc cập nhật các tài khoản chuẩn với mật khẩu bảo mật riêng biệt cho từng vai trò
                    var defaultUsers = new[]
                    {
                        new SysUser { Username = "admin", FullName = "Quản Trị Viên Hệ Thống", Email = "admin@khangnghi.com.vn", Role = "Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@KhangNghi2026#") },
                        new SysUser { Username = "giamdoc", FullName = "Ban Giám Đốc Khang Nghị", Email = "ceo@khangnghi.com.vn", Role = "Director", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Ceo@KhangNghi2026!") },
                        new SysUser { Username = "kinhdoanh", FullName = "Trưởng Phòng Kinh Doanh", Email = "sales@khangnghi.com.vn", Role = "SalesManager", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Sales@KhangNghi2026$") },
                        new SysUser { Username = "muahang", FullName = "Trưởng Phòng Mua Hàng", Email = "purchase@khangnghi.com.vn", Role = "PurchaseManager", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Purchase@KhangNghi2026#") },
                        new SysUser { Username = "kho", FullName = "Trưởng Phòng Quản Lý Kho", Email = "kho@khangnghi.com.vn", Role = "InventoryManager", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Warehouse@KhangNghi2026%") },
                        new SysUser { Username = "vanchuyen", FullName = "Trưởng Phòng Logistics", Email = "shipping@khangnghi.com.vn", Role = "ShippingStaff", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Shipping@KhangNghi2026&") }
                    };

                    const string upsertSql = @"
                        IF EXISTS (SELECT 1 FROM dbo.Sys_User WHERE Username = @Username)
                        BEGIN
                            UPDATE dbo.Sys_User 
                            SET PasswordHash = @PasswordHash, FullName = @FullName, Email = @Email, Role = @Role, IsActive = 1
                            WHERE Username = @Username;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Sys_User (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                            VALUES (@Username, @PasswordHash, @FullName, @Email, @Role, 1, GETDATE());
                        END";

                foreach (var u in defaultUsers)
                {
                    await db.ExecuteAsync(upsertSql, u);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EnsureDefaultUsersAsync error: " + ex.Message);
            }
        }
    }
}
