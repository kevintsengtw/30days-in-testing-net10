namespace Day17.FileSystemTesting.Core;

/// <summary>
/// 負責設定檔案的讀取與寫入
/// </summary>
public class ConfigurationService
{
    private readonly IFileSystem _fileSystem;

    public ConfigurationService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// 載入設定值，如果檔案不存在則回傳預設值
    /// </summary>
    /// <param name="filePath">設定檔案路徑</param>
    /// <param name="defaultValue">預設值</param>
    /// <returns>設定值</returns>
    public async Task<string> LoadConfigurationAsync(string filePath, string defaultValue = "")
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return defaultValue;
        }

        try
        {
            return await _fileSystem.File.ReadAllTextAsync(filePath);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 儲存設定到檔案
    /// </summary>
    /// <param name="filePath">設定檔案路徑</param>
    /// <param name="value">要儲存的值</param>
    public async Task SaveConfigurationAsync(string filePath, string value)
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        await _fileSystem.File.WriteAllTextAsync(filePath, value);
    }

    /// <summary>
    /// 載入 JSON 設定
    /// </summary>
    /// <typeparam name="T">設定類型</typeparam>
    /// <param name="filePath">設定檔案路徑</param>
    /// <returns>設定物件，如果檔案不存在或解析失敗則回傳預設值</returns>
    public async Task<T?> LoadJsonConfigurationAsync<T>(string filePath) where T : class
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return default;
        }

        try
        {
            var jsonContent = await _fileSystem.File.ReadAllTextAsync(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<T>(jsonContent);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// 儲存 JSON 設定
    /// </summary>
    /// <typeparam name="T">設定類型</typeparam>
    /// <param name="filePath">設定檔案路徑</param>
    /// <param name="configuration">設定物件</param>
    public async Task SaveJsonConfigurationAsync<T>(string filePath, T configuration) where T : class
    {
        var directory = _fileSystem.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(configuration, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await _fileSystem.File.WriteAllTextAsync(filePath, jsonContent);
    }
}
