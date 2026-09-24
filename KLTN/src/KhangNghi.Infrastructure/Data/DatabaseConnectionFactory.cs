using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace KhangNghi.Infrastructure.Data
{
    public interface IDatabaseConnectionFactory
    {
        IDbConnection CreateConnection();
        string ConnectionString { get; }
        void UpdateConnectionString(string newConnStr);
        bool TestConnection(out string message);
    }

    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private string _connectionString;

        public string ConnectionString => _connectionString;

        public DatabaseConnectionFactory(string? initialConnectionString = null)
        {
            if (!string.IsNullOrWhiteSpace(initialConnectionString))
            {
                _connectionString = initialConnectionString;
            }
            else
            {
                // Mặc định kết nối tới SQLEXPRESS trên máy cục bộ
                _connectionString = "Server=localhost\\SQLEXPRESS;Database=DWH_KhangNghi;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;";
            }
        }

        public void UpdateConnectionString(string newConnStr)
        {
            if (!string.IsNullOrWhiteSpace(newConnStr))
            {
                _connectionString = newConnStr;
            }
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public bool TestConnection(out string message)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'";
                var count = cmd.ExecuteScalar();
                message = $"Kết nối thành công! Đã tìm thấy {count} bảng trong CSDL.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Lỗi kết nối: {ex.Message}";
                return false;
            }
        }
    }
}
