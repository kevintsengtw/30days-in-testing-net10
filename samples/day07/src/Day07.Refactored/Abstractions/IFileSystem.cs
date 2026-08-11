namespace Day07.Refactored.Abstractions;

/// <summary>
/// interface IFileSystem - 檔案系統介面
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// 路徑操作
    /// </summary>
    IPath Path { get; }
    /// <summary>
    /// 檢查檔案是否存在
    /// </summary>
    /// <param name="path">檔案路徑</param>
    /// <returns>是否存在</returns>
    bool FileExists(string path);

    /// <summary>
    /// 獲取檔案資訊
    /// </summary>
    /// <param name="path">檔案路徑</param>
    /// <returns>檔案資訊</returns>
    IFileInfo GetFileInfo(string path);

    /// <summary>
    /// 複製檔案
    /// </summary>
    /// <param name="sourcePath">來源路徑</param>
    /// <param name="destinationPath">目標路徑</param>
    void CopyFile(string sourcePath, string destinationPath);
}

/// <summary>
/// interface IFileInfo - 檔案資訊介面
/// </summary>
public interface IFileInfo
{
    /// <summary>
    /// 獲取檔案大小
    /// </summary>
    long Length { get; }

    /// <summary>
    /// 獲取檔案名稱
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 獲取檔案完整名稱
    /// </summary>
    string FullName { get; }
}

/// <summary>
/// interface IPath - 路徑操作介面
/// </summary>
public interface IPath
{
    string GetFileNameWithoutExtension(string path);

    string GetExtension(string path);

    string Combine(string path1, string path2);
}
