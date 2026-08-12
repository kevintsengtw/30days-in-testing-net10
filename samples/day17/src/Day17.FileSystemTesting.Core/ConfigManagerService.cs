namespace Day17.FileSystemTesting.Core;

/// <summary>
/// 整合設定管理器，示範實際應用情境
/// </summary>
public class ConfigManagerService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configDirectory;
    private readonly TimeProvider _timeProvider;

    public ConfigManagerService(
        IFileSystem fileSystem,
        string configDirectory = "config",
        TimeProvider? timeProvider = null)
    {
        _fileSystem = fileSystem;
        _configDirectory = configDirectory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 初始化設定目錄
    /// </summary>
    public void InitializeConfigDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_configDirectory) && !_fileSystem.Directory.Exists(_configDirectory))
        {
            _fileSystem.Directory.CreateDirectory(_configDirectory);
        }
    }

    /// <summary>
    /// 載入應用程式設定
    /// </summary>
    /// <returns>應用程式設定</returns>
    public async Task<AppSettings> LoadAppSettingsAsync()
    {
        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");

        if (!_fileSystem.File.Exists(configPath))
        {
            var defaultSettings = new AppSettings();
            await SaveAppSettingsAsync(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var jsonContent = await _fileSystem.File.ReadAllTextAsync(configPath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(jsonContent);
            return settings ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// 儲存應用程式設定
    /// </summary>
    /// <param name="settings">應用程式設定</param>
    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        InitializeConfigDirectory();

        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await _fileSystem.File.WriteAllTextAsync(configPath, jsonContent);
    }

    /// <summary>
    /// 備份現有設定
    /// </summary>
    /// <returns>備份檔案路徑</returns>
    public string BackupConfiguration()
    {
        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");

        if (!_fileSystem.File.Exists(configPath))
        {
            throw new FileNotFoundException("找不到要備份的設定檔案");
        }

        var backupDirectory = _fileSystem.Path.Combine(_configDirectory, "backup");
        if (!_fileSystem.Directory.Exists(backupDirectory))
        {
            _fileSystem.Directory.CreateDirectory(backupDirectory);
        }

        var timestamp = _timeProvider.GetLocalNow().ToString("yyyyMMdd_HHmmss_fff");
        var backupFileName = $"appsettings_{timestamp}.json";
        var backupPath = _fileSystem.Path.Combine(backupDirectory, backupFileName);

        _fileSystem.File.Copy(configPath, backupPath);
        return backupPath;
    }

    /// <summary>
    /// 取得所有可用的備份檔案
    /// </summary>
    /// <returns>備份檔案清單</returns>
    public List<BackupInfo> GetAvailableBackups()
    {
        var backupDirectory = _fileSystem.Path.Combine(_configDirectory, "backup");

        if (!_fileSystem.Directory.Exists(backupDirectory))
        {
            return new List<BackupInfo>();
        }

        return _fileSystem.Directory.GetFiles(backupDirectory, "appsettings_*.json")
            .Select(filePath => new BackupInfo
            {
                FilePath = filePath,
                FileName = _fileSystem.Path.GetFileName(filePath),
                CreationTime = _fileSystem.File.GetCreationTime(filePath),
                Size = _fileSystem.FileInfo.New(filePath).Length
            })
            .OrderByDescending(b => b.CreationTime)
            .ToList();
    }

    /// <summary>
    /// 從備份還原設定
    /// </summary>
    /// <param name="backupFilePath">備份檔案路徑</param>
    public void RestoreFromBackup(string backupFilePath)
    {
        if (!_fileSystem.File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"備份檔案不存在: {backupFilePath}");
        }

        var configPath = _fileSystem.Path.Combine(_configDirectory, "appsettings.json");
        _fileSystem.File.Copy(backupFilePath, configPath, overwrite: true);
    }

    /// <summary>
    /// 應用程式設定
    /// </summary>
    public class AppSettings
    {
        public string ApplicationName { get; set; } = "Day17 FileSystem Testing Demo";
        public string Version { get; set; } = "1.0.0";
        public DatabaseSettings Database { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
    }

    /// <summary>
    /// 資料庫設定
    /// </summary>
    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Server=localhost;Database=TestDb;";
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// 日誌設定
    /// </summary>
    public class LoggingSettings
    {
        public string Level { get; set; } = "Information";
        public bool EnableFileLogging { get; set; } = true;
        public string LogDirectory { get; set; } = "logs";
    }

    /// <summary>
    /// 備份資訊
    /// </summary>
    public class BackupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; }
        public long Size { get; set; }
    }
}
