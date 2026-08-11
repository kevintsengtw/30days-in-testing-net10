using Day07.Refactored.Abstractions;

namespace Day07.Refactored.Implementations;

/// <summary>
/// class FileSystemWrapper - 檔案系統包裝器
/// </summary>
public class FileSystemWrapper : IFileSystem
{
    public IPath Path { get; } = new PathWrapper();
    /// <summary>
    /// 檢查檔案是否存在
    /// </summary>
    /// <param name="path">檔案路徑</param>
    /// <returns>是否存在</returns>
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>
    /// 獲取檔案資訊
    /// </summary>
    /// <param name="path">檔案路徑</param>
    /// <returns>檔案資訊</returns>
    public IFileInfo GetFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);
        return new FileInfoWrapper(fileInfo);
    }

    /// <summary>
    /// 複製檔案
    /// </summary>
    /// <param name="sourcePath">來源路徑</param>
    /// <param name="destinationPath">目標路徑</param>
    public void CopyFile(string sourcePath, string destinationPath)
    {
        File.Copy(sourcePath, destinationPath);
    }
}

/// <summary>
/// class FileInfoWrapper - 檔案資訊包裝器
/// </summary>
public class FileInfoWrapper : IFileInfo
{
    private readonly FileInfo _fileInfo;

    /// <summary>
    /// 檔案資訊包裝器建構子
    /// </summary>
    /// <param name="fileInfo">檔案資訊</param>
    public FileInfoWrapper(FileInfo fileInfo)
    {
        this._fileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
    }

    /// <summary>
    /// 獲取檔案大小
    /// </summary>
    public long Length => this._fileInfo.Length;

    /// <summary>
    /// 獲取檔案名稱
    /// </summary>
    public string Name => this._fileInfo.Name;

    /// <summary>
    /// 獲取檔案完整名稱
    /// </summary>
    public string FullName => this._fileInfo.FullName;
}

/// <summary>
/// class PathWrapper - 路徑操作包裝器
/// </summary>
public class PathWrapper : IPath
{
    public string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

    public string GetExtension(string path) => System.IO.Path.GetExtension(path);

    public string Combine(string path1, string path2) => System.IO.Path.Combine(path1, path2);
}
