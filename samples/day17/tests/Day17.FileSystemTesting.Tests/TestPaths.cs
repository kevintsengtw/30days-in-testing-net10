namespace Day17.FileSystemTesting.Tests;

/// <summary>
/// 將教學用的 Windows 範例路徑轉成執行主機可解析的測試路徑。
/// </summary>
public static class TestPaths
{
    public static string FromWindowsPath(string windowsPath)
    {
        var segments = windowsPath
            .Replace(':', Path.DirectorySeparatorChar)
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        return Path.GetFullPath(Path.Combine(["test-root", .. segments]));
    }

    public static string Combine(string rootPath, params string[] pathSegments) =>
        Path.Combine([rootPath, .. pathSegments]);
}
