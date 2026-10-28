using System;

namespace KhangNghi.Core.Configuration
{
    public class AppSettings
    {
        public ConnectionStringsOptions ConnectionStrings { get; set; } = new();
        public EtlSettingsOptions EtlSettings { get; set; } = new();
        public PowerBiSettingsOptions PowerBiSettings { get; set; } = new();
    }

    public class ConnectionStringsOptions
    {
        public string DwhConnection { get; set; } = "Server=.;Database=DWH_KhangNghi;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;";
        public string SsasConnection { get; set; } = "Provider=MSOLAP;Data Source=localhost\\SQL2025;Initial Catalog=KhangNghi_SSAS;Integrated Security=SSPI;";
    }

    public class EtlSettingsOptions
    {
        // Paths on the SQL Agent host. Runner config must be readable by its execution identity.
        public string RunnerPath { get; set; } = string.Empty;
        public string RunnerConfigPath { get; set; } = string.Empty;
        public string DtExecPath { get; set; } = @"C:\Program Files\Microsoft SQL Server\170\DTS\Binn\DTExec.exe";
        public string MasterPackagePath { get; set; } = @"E:\CNTT_KLCN235_Nguyễn Trường Hưng\KhangNghi_DataWarehouse\KhangNghi_ETL\Master_ETL.dtsx";
        public string SsasServer { get; set; } = @"localhost\SQL2025";
        public bool ProcessSsasAfterLoad { get; set; } = false;
        public string SsasDatabase { get; set; } = @"KhangNghi_SSAS";
    }

    public class PowerBiSettingsOptions
    {
        public string SalesPbixPath { get; set; } = @"E:\CNTT_KLCN235_Nguyễn Trường Hưng\KhangNghi_DataWarehouse\BaoCao_KinhDoanh.pbix";
        public string InventoryPbixPath { get; set; } = @"E:\CNTT_KLCN235_Nguyễn Trường Hưng\KhangNghi_DataWarehouse\BaoCao_TonKho.pbix";
    }
}
