using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KhangNghi.Infrastructure.Repositories
{
    public interface ISsasRepository
    {
        Task<List<Dictionary<string, object?>>> ExecuteMdxQueryAsync(string mdxQuery);
        Task<List<Dictionary<string, object?>>> GetRevenueByYearFromCubeAsync();
        Task<List<Dictionary<string, object?>>> GetTopProductsFromCubeAsync(int top = 10);
        Task<List<Dictionary<string, object?>>> GetRevenueByCategoryFromCubeAsync();
    }

    public class SsasRepository : ISsasRepository
    {
        private readonly string _ssasConnectionString;

        public SsasRepository(IConfiguration configuration)
        {
            _ssasConnectionString = configuration.GetConnectionString("SsasConnection")
                ?? "Provider=MSOLAP;Data Source=localhost;Initial Catalog=KhangNghi_SSAS;Integrated Security=SSPI;";
        }

        public async Task<List<Dictionary<string, object?>>> ExecuteMdxQueryAsync(string mdxQuery)
        {
            var result = new List<Dictionary<string, object?>>();

            using var conn = new OleDbConnection(_ssasConnectionString);
            await conn.OpenAsync();

            using var cmd = new OleDbCommand(mdxQuery, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    // Simplify column names if MDX returns full member names like [Dim Time].[Year].[MEMBER_CAPTION]
                    row[colName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Add(row);
            }

            return result;
        }

        public Task<List<Dictionary<string, object?>>> GetRevenueByYearFromCubeAsync()
        {
            string mdx = @"
                SELECT 
                    [Measures].[Total Sales] ON COLUMNS,
                    [Dim Time].[Year].MEMBERS ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }

        public Task<List<Dictionary<string, object?>>> GetTopProductsFromCubeAsync(int top = 10)
        {
            string mdx = $@"
                SELECT 
                    [Measures].[Total Profit] ON COLUMNS,
                    TOPCOUNT([Dim Product].[Product Name].MEMBERS, {top}, [Measures].[Total Profit]) ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }

        public Task<List<Dictionary<string, object?>>> GetRevenueByCategoryFromCubeAsync()
        {
            string mdx = @"
                SELECT 
                    [Measures].[Total Sales] ON COLUMNS,
                    [Dim Product].[Category].MEMBERS ON ROWS
                FROM [KhangNghi_Cube]
            ";
            return ExecuteMdxQueryAsync(mdx);
        }
    }
}
