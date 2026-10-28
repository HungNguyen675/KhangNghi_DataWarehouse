namespace KhangNghi.Infrastructure.Services;

public static class EtlRuntime
{
    public static void ValidateLocalFile(string path)
    {
        ValidateCommandPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Configured ETL file does not exist.", path);
    }

    // Agent paths belong to the server; existence is checked by the runner there.
    public static void ValidateCommandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.IndexOfAny(['"', '\r', '\n', '%', '!']) >= 0)
            throw new ArgumentException("Use an absolute path without quotes, newlines or command expansion characters.");
    }
}
