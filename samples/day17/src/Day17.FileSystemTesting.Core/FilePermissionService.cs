namespace Day17.FileSystemTesting.Core;

/// <summary>
/// 檔案權限檢查服務
/// </summary>
public class FilePermissionService
{
    private readonly IFileSystem _fileSystem;

    public FilePermissionService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// 檢查是否可以讀取檔案
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>是否可讀取</returns>
    public bool CanReadFile(string filePath)
    {
        try
        {
            if (!_fileSystem.File.Exists(filePath))
            {
                return false;
            }

            // 嘗試開啟檔案進行讀取
            using var stream = _fileSystem.File.OpenRead(filePath);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 檢查是否可以寫入檔案
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>是否可寫入</returns>
    public bool CanWriteFile(string filePath)
    {
        try
        {
            if (_fileSystem.File.Exists(filePath))
            {
                // 檔案存在，檢查是否可以寫入
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                if (fileInfo.IsReadOnly)
                {
                    return false;
                }

                using var stream = _fileSystem.File.OpenWrite(filePath);
                return true;
            }
            else
            {
                // 檔案不存在，檢查是否可以在目錄中建立檔案
                var directory = _fileSystem.Path.GetDirectoryName(filePath);
                return CanWriteToDirectory(directory ?? "");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 檢查是否可以寫入目錄
    /// </summary>
    /// <param name="directoryPath">目錄路徑</param>
    /// <returns>是否可寫入</returns>
    public bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            if (!_fileSystem.Directory.Exists(directoryPath))
            {
                return false;
            }

            // 嘗試在目錄中建立暫時檔案
            var tempFileName = _fileSystem.Path.Combine(directoryPath,
                $"temp_{Guid.NewGuid()}.tmp");

            _fileSystem.File.WriteAllText(tempFileName, "test");
            _fileSystem.File.Delete(tempFileName);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 檢查檔案權限摘要
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>權限摘要</returns>
    public FilePermissionSummary GetFilePermissions(string filePath)
    {
        return new FilePermissionSummary
        {
            FilePath = filePath,
            Exists = _fileSystem.File.Exists(filePath),
            CanRead = CanReadFile(filePath),
            CanWrite = CanWriteFile(filePath),
            IsReadOnly = _fileSystem.File.Exists(filePath) &&
                        _fileSystem.FileInfo.New(filePath).IsReadOnly
        };
    }

    /// <summary>
    /// 檔案權限摘要
    /// </summary>
    public class FilePermissionSummary
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
