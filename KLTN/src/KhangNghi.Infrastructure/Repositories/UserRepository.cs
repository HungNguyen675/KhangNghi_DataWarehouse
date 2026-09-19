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
                var count = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Sys_User");
                if (count == 0)
                {
                    // Tạo sẵn các tài khoản chuẩn theo phân quyền 5 role + Admin
                    var defaultUsers = new[]
                    {
                        new SysUser { Username = "admin", FullName = "Quản Trị Viên Hệ Thống", Email = "admin@khangnghi.com.vn", Role = "Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") },
                        new SysUser { Username = "giamdoc", FullName = "Ban Giám Đốc Khang Nghị", Email = "ceo@khangnghi.com.vn", Role = "GiamDoc", PasswordHash = BCrypt.Net.BCrypt.HashPassword("giamdoc123") },
                        new SysUser { Username = "kinhdoanh", FullName = "Trưởng Phòng Kinh Doanh", Email = "sales@khangnghi.com.vn", Role = "QuanLyKinhDoanh", PasswordHash = BCrypt.Net.BCrypt.HashPassword("kinhdoanh123") },
                        new SysUser { Username = "muahang", FullName = "Trưởng Phòng Mua Hàng", Email = "purchase@khangnghi.com.vn", Role = "QuanLyMuaHang", PasswordHash = BCrypt.Net.BCrypt.HashPassword("muahang123") },
                        new SysUser { Username = "kho", FullName = "Trưởng Phòng Quản Lý Kho", Email = "kho@khangnghi.com.vn", Role = "QuanLyKho", PasswordHash = BCrypt.Net.BCrypt.HashPassword("kho123") },
                        new SysUser { Username = "vanchuyen", FullName = "Trưởng Phòng Logistics", Email = "shipping@khangnghi.com.vn", Role = "QuanLyVanChuyen", PasswordHash = BCrypt.Net.BCrypt.HashPassword("vanchuyen123") }
                    };

                    const string insertSql = @"
                        INSERT INTO dbo.Sys_User (Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                        VALUES (@Username, @PasswordHash, @FullName, @Email, @Role, 1, GETDATE());";

                    foreach (var u in defaultUsers)
                    {
                        await db.ExecuteAsync(insertSql, u);
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu bảng chưa sẵn sàng
            }
        }
    }
}
