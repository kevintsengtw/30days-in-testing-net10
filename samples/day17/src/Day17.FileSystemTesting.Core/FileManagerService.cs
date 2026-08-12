namespace Day17.FileSystemTesting.Core;

/// <summary>
/// 檔案管理服務
/// </summary>
public class FileManagerService
{
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;

    public FileManagerService(IFileSystem fileSystem, TimeProvider? timeProvider = null)
    {
        _fileSystem = fileSystem;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 複製檔案到指定目錄
    /// </summary>
    /// <param name="sourceFilePath">來源檔案路徑</param>
    /// <param name="targetDirectory">目標目錄</param>
    /// <returns>目標檔案路徑</returns>
    public string CopyFileToDirectory(string sourceFilePath, string targetDirectory)
    {
        if (!_fileSystem.File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"來源檔案不存在: {sourceFilePath}");
        }

        if (!_fileSystem.Directory.Exists(targetDirectory))
        {
            _fileSystem.Directory.CreateDirectory(targetDirectory);
        }

        var fileName = _fileSystem.Path.GetFileName(sourceFilePath);
        var targetFilePath = _fileSystem.Path.Combine(targetDirectory, fileName);

        _fileSystem.File.Copy(sourceFilePath, targetFilePath, overwrite: true);
        return targetFilePath;
    }

    /// <summary>
    /// 備份檔案（加上時間戳記）
    /// </summary>
    /// <param name="filePath">要備份的檔案路徑</param>
    /// <returns>備份檔案路徑</returns>
    public string BackupFile(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            throw new FileNotFoundException($"檔案不存在: {filePath}");
        }

        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        var fileNameWithoutExtension = _fileSystem.Path.GetFileNameWithoutExtension(filePath);
        var extension = _fileSystem.Path.GetExtension(filePath);
        var timestamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd_HHmmss_fff");

        var backupFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";
        var backupFilePath = _fileSystem.Path.Combine(directory ?? "", backupFileName);

        _fileSystem.File.Copy(filePath, backupFilePath);
        return backupFilePath;
    }

    /// <summary>
    /// 清理舊備份檔案（保留指定數量的最新檔案）
    /// </summary>
    /// <param name="directory">備份目錄</param>
    /// <param name="pattern">檔案模式</param>
    /// <param name="keepCount">保留的檔案數量</param>
    /// <returns>刪除的檔案數量</returns>
    public int CleanupOldBackups(string directory, string pattern, int keepCount)
    {
        if (!_fileSystem.Directory.Exists(directory))
        {
            return 0;
        }

        var files = _fileSystem.Directory.GetFiles(directory, pattern)
            .Select(f => new
            {
                Path = f,
                CreationTime = _fileSystem.File.GetCreationTime(f)
            })
            .OrderByDescending(f => f.CreationTime)
            .Skip(keepCount)
            .ToList();

        foreach (var file in files)
        {
            _fileSystem.File.Delete(file.Path);
        }

        return files.Count;
    }

    /// <summary>
    /// 取得檔案資訊
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>檔案資訊</returns>
    public FileInfoData? GetFileInfo(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return null;
        }

        var fileInfo = _fileSystem.FileInfo.New(filePath);
        return new FileInfoData
        {
            Name = fileInfo.Name,
            FullPath = fileInfo.FullName,
            Size = fileInfo.Length,
            CreationTime = fileInfo.CreationTime,
            LastWriteTime = fileInfo.LastWriteTime,
            IsReadOnly = fileInfo.IsReadOnly
        };
    }

    /// <summary>
    /// 檔案資訊資料類別
    /// </summary>
    public class FileInfoData
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
