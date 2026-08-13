---
day: 17
title: "Day 17 - 檔案與 IO 測試：使用 System.IO.Abstractions 模擬檔案系統 - 打造可測試的檔案操作"
sample: samples/day17
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.TrxReport
  - NSubstitute
  - System.IO.Abstractions
  - System.IO.Abstractions.TestingHelpers
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 17 - 檔案與 IO 測試：使用 System.IO.Abstractions 模擬檔案系統 - 打造可測試的檔案操作

<!-- toc -->

- [前言](#前言)
- [檔案系統測試的根本挑戰](#檔案系統測試的根本挑戰)
- [System.IO.Abstractions 解決方案](#systemioabstractions-解決方案)
- [重構檔案相依程式碼](#重構檔案相依程式碼)
- [MockFileSystem 測試實戰](#mockfilesystem-測試實戰)
- [目錄操作與路徑處理測試](#目錄操作與路徑處理測試)
- [進階檔案操作測試技巧](#進階檔案操作測試技巧)
- [大檔案與串流操作測試](#大檔案與串流操作測試)
- [實務整合範例：設定檔管理器](#實務整合範例設定檔管理器)
- [最佳實務與注意事項](#最佳實務與注意事項)
- [效能考量與限制](#效能考量與限制)
- [與其他測試技術的整合](#與其他測試技術的整合)
- [今日小結](#今日小結)
- [延伸閱讀](#延伸閱讀)

<!-- /toc -->

## 前言

前一天學會了如何處理時間相依性的測試問題，現在要面對另一個常見的測試挑戰：**檔案系統相依性**。

實際開發中，經常需要處理檔案操作：

- 讀取設定檔
- 處理上傳的檔案
- 產生報表並儲存
- 處理日誌檔案
- 資料匯入匯出

傳統上，會直接使用 `System.IO.File`、`System.IO.Directory` 等靜態類別來處理這些操作。但是，當要為這些程式碼寫單元測試時，就會遇到許多問題。

今天將學習如何使用 **System.IO.Abstractions** 來解決檔案系統測試的根本問題，建立快速、可靠、不依賴真實檔案系統的測試。

---

## 檔案系統測試的根本挑戰

### 問題一：實際 IO 操作的速度與可靠性問題

先看一個典型的檔案處理程式碼：

```csharp
public class ConfigurationService
{
    public string LoadConfig(string configPath)
    {
        return File.ReadAllText(configPath);
    }

    public void SaveConfig(string configPath, string content)
    {
        File.WriteAllText(configPath, content);
    }

    public bool ConfigExists(string configPath)
    {
        return File.Exists(configPath);
    }
}
```

這段程式碼看起來很正常，但當要寫測試時會遇到：

1. **速度問題**：實際的檔案 IO 操作比記憶體操作慢很多
2. **可靠性問題**：依賴磁碟狀態、檔案權限、路徑是否存在
3. **測試隔離問題**：多個測試可能會互相影響

### 問題二：環境相依性

```csharp
[Fact]
public void LoadConfig_檔案存在_應回傳內容()
{
    // Arrange
    var configPath = "config.json";
    var expectedContent = "{ \"key\": \"value\" }";

    // 這裡需要先建立實際檔案
    File.WriteAllText(configPath, expectedContent);

    var service = new ConfigurationService();

    // Act
    var result = service.LoadConfig(configPath);

    // Assert
    result.Should().Be(expectedContent);

    // 測試後需要清理檔案
    File.Delete(configPath);
}
```

這個測試有以下問題：

- **環境相依**：依賴檔案系統狀態
- **副作用**：在檔案系統中留下痕跡
- **權限問題**：可能因為檔案權限而失敗
- **路徑問題**：在不同作業系統上可能有不同的行為
- **清理困難**：測試失敗時可能無法正確清理檔案

### 問題三：並行測試的檔案競爭

```csharp
// 這些測試如果並行執行，可能會互相干擾
[Fact]
public void Test1() => File.WriteAllText("temp.txt", "content1");

[Fact]
public void Test2() => File.WriteAllText("temp.txt", "content2");
```

### 問題四：難以模擬錯誤情境

要如何測試以下情況：

- 檔案不存在
- 權限不足
- 磁碟空間不足
- 檔案被其他處理程序鎖定
- 網路磁碟機連線中斷

---

## System.IO.Abstractions 解決方案

### 什麼是 System.IO.Abstractions？

System.IO.Abstractions 是一個 .NET 套件，它將 System.IO 的靜態類別包裝成介面，支援在測試中使用相依性注入和模擬。

**核心特色**：

1. **抽象化檔案系統操作**：將 File、Directory、FileInfo、DirectoryInfo 等包裝成介面
2. **支援相依性注入**：可以直接將檔案系統操作注入到服務中
3. **測試友善設計**：提供 MockFileSystem 進行記憶體檔案系統模擬
4. **完全相容**：API 與 System.IO 完全相同，重構成本低

### 核心介面架構

```csharp
// 主要介面
public interface IFileSystem
{
    IFile File { get; }
    IDirectory Directory { get; }
    IFileInfoFactory FileInfo { get; }
    IDirectoryInfoFactory DirectoryInfo { get; }
    IPath Path { get; }
    IDriveInfoFactory DriveInfo { get; }
    // ...（另有 FileStream、FileSystemWatcher、FileVersionInfo 等工廠成員）
}

// 檔案操作介面
public interface IFile
{
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    bool Exists(string path);
    void Delete(string path);
    void Copy(string sourceFileName, string destFileName);
    // ... 更多方法
}
```

留意 `FileInfo`、`DirectoryInfo`、`DriveInfo` 這三個屬性是**工廠介面**，要用 `.New(path)` 取得對應的 `IFileInfo` 等執行個體——這就是後面範例裡 `_fileSystem.FileInfo.New(filePath)` 寫法的由來。

---

## 重構檔案相依程式碼

將剛才的 ConfigurationService 重構為可測試的版本：

### 重構前（不可測試）

```csharp
public class ConfigurationService
{
    public string LoadConfig(string configPath)
    {
        return File.ReadAllText(configPath);
    }

    public void SaveConfig(string configPath, string content)
    {
        File.WriteAllText(configPath, content);
    }

    public bool ConfigExists(string configPath)
    {
        return File.Exists(configPath);
    }
}
```

### 重構後（可測試）

```csharp
using System.IO.Abstractions;

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
}
```

### 在實際應用中註冊 IFileSystem

```csharp
// Program.cs 或 Startup.cs
services.AddSingleton<IFileSystem, FileSystem>();
services.AddScoped<ConfigurationService>();
```

---

## MockFileSystem 測試實戰

### 基礎檔案操作測試

在重構了 ConfigurationService 之後，現在可以使用 MockFileSystem 來建立可控制的測試環境。這個章節將說明如何測試最基本的檔案操作：讀取、寫入和檢查檔案是否存在。

**MockFileSystem 的核心優勢**：

1. **記憶體模擬**：所有檔案操作都在記憶體中進行，速度極快
2. **狀態控制**：可以直接定義檔案系統的初始狀態
3. **驗證容易**：測試後可以直接檢查檔案系統的最終狀態

**跨平台測試路徑**：本文的教學範例沿用 Windows 風格路徑（`C:\config\...`）比較直觀，但測試要能在 macOS／Linux 上跑（例如 CI），所以範例專案用一個小工具類別把 Windows 範例路徑轉成執行主機可解析的路徑：

```csharp
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
```

之後測試裡看到 `TestPaths.FromWindowsPath(@"C:\config\app.config")`，就是「以這個 Windows 路徑為藍本，產生目前平台可用的測試路徑」的意思。

看看如何測試 ConfigurationService 的各個方法：

```csharp
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;

public class ConfigurationServiceTests
{
    [Fact]
    public async Task LoadConfigurationAsync_當檔案存在_應回傳檔案內容()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\app.config");
        var expectedContent = "database_connection=server123";

        mockFileSystem.AddFile(filePath, new MockFileData(expectedContent));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task SaveConfigurationAsync_當目標目錄不存在_應建立目錄並儲存檔案()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\subfolder\app.config");
        var content = "new_configuration";

        var service = new ConfigurationService(mockFileSystem);

        // Act
        await service.SaveConfigurationAsync(filePath, content);

        // Assert
        mockFileSystem.Directory.Exists(TestPaths.FromWindowsPath(@"C:\config\subfolder")).Should().BeTrue();
        mockFileSystem.File.Exists(filePath).Should().BeTrue();
        var savedContent = mockFileSystem.File.ReadAllText(filePath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task LoadConfigurationAsync_當檔案不存在_應回傳預設值()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\nonexistent.config");
        var defaultValue = "default_config";

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync(filePath, defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_當檔案存在且格式正確_應回傳物件()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\settings.json");
        var testConfig = new TestConfiguration
        {
            Name = "Test App",
            Version = "1.0.0",
            IsEnabled = true
        };
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(testConfig);

        mockFileSystem.AddFile(filePath, new MockFileData(jsonContent));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadJsonConfigurationAsync<TestConfiguration>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test App");
        result.Version.Should().Be("1.0.0");
        result.IsEnabled.Should().BeTrue();
    }

    /// <summary>
    /// 測試用的設定類別
    /// </summary>
    public class TestConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
```

---

## 目錄操作與路徑處理測試

### 實際專案中的檔案管理需求

基本的檔案讀寫只是開始，實際專案中經常要處理更複雜的情況。比如管理整個目錄結構、取得檔案資訊、建立備份檔案等。

來看看一個更實用的檔案管理服務，它涵蓋了常見的檔案操作需求：

- **目錄管理**：列出目錄中的檔案、建立目錄結構
- **檔案資訊查詢**：取得檔案大小、修改日期等資訊
- **檔案備份**：建立檔案副本，通常加上時間戳記
- **路徑處理**：跨平台的路徑組合和解析

這個 FileManagerService 說明了如何處理這些常見任務，重點是要做好防禦性程式設計和錯誤處理。

```csharp
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
```

### 目錄操作測試

除了單純的檔案讀寫，目錄操作也是常見需求。要列出目錄中的檔案、建立目錄結構、取得檔案大小等。

傳統測試中要建立複雜的目錄結構很麻煩，但用 MockFileSystem 可以快速模擬任何目錄結構，還能驗證程式是否正確處理各種邊界情況。

測試重點包括：目錄存在性檢查、檔案清單功能、目錄建立邏輯、檔案資訊取得等。

```csharp
using Microsoft.Extensions.Time.Testing;

public class FileManagerServiceTests
{
    [Fact]
    public void CopyFileToDirectory_當來源檔案存在_應成功複製到目標目錄()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var sourceFile = TestPaths.FromWindowsPath(@"C:\source\document.txt");
        var targetDirectory = TestPaths.FromWindowsPath(@"C:\target");
        var expectedTargetFile = TestPaths.FromWindowsPath(@"C:\target\document.txt");
        var fileContent = "測試文件內容";

        mockFileSystem.AddFile(sourceFile, new MockFileData(fileContent));

        var service = new FileManagerService(mockFileSystem);

        // Act
        var result = service.CopyFileToDirectory(sourceFile, targetDirectory);

        // Assert
        result.Should().Be(expectedTargetFile);
        mockFileSystem.File.Exists(expectedTargetFile).Should().BeTrue();
        mockFileSystem.File.ReadAllText(expectedTargetFile).Should().Be(fileContent);
        mockFileSystem.Directory.Exists(targetDirectory).Should().BeTrue();
    }

    [Fact]
    public void CopyFileToDirectory_當來源檔案不存在_應拋出FileNotFoundException()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var sourceFile = TestPaths.FromWindowsPath(@"C:\source\nonexistent.txt");
        var targetDirectory = TestPaths.FromWindowsPath(@"C:\target");

        var service = new FileManagerService(mockFileSystem);

        // Act & Assert
        var action = () => service.CopyFileToDirectory(sourceFile, targetDirectory);
        action.Should().Throw<FileNotFoundException>()
            .WithMessage($"來源檔案不存在: {sourceFile}");
    }

    [Fact]
    public void BackupFile_當檔案存在_應建立帶時間戳記的備份檔案()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var originalFile = TestPaths.FromWindowsPath(@"C:\data\important.txt");
        var fileContent = "重要資料";
        var fakeTimeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 7, 23, 12, 34, 56, TimeSpan.Zero));

        mockFileSystem.AddFile(originalFile, new MockFileData(fileContent));

        var service = new FileManagerService(mockFileSystem, fakeTimeProvider);

        // Act
        var backupPath = service.BackupFile(originalFile);

        // Assert
        backupPath.Should().Be(TestPaths.FromWindowsPath(@"C:\data\important_20260723_123456_000.txt"));
        backupPath.Should().NotBe(originalFile);

        mockFileSystem.File.Exists(backupPath).Should().BeTrue();
        mockFileSystem.File.ReadAllText(backupPath).Should().Be(fileContent);

        // 驗證原檔案仍然存在
        mockFileSystem.File.Exists(originalFile).Should().BeTrue();
    }

    [Fact]
    public void GetFileInfo_當檔案存在_應回傳正確的檔案資訊()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\data\test.txt");
        var fileContent = "測試檔案內容";
        var creationTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var lastWriteTime = new DateTime(2024, 1, 16, 14, 45, 0);

        mockFileSystem.AddFile(filePath, new MockFileData(fileContent)
        {
            CreationTime = creationTime,
            LastWriteTime = lastWriteTime
        });

        var service = new FileManagerService(mockFileSystem);

        // Act
        var fileInfo = service.GetFileInfo(filePath);

        // Assert
        fileInfo.Should().NotBeNull();
        fileInfo!.Name.Should().Be("test.txt");
        fileInfo.FullPath.Should().Be(filePath);
        fileInfo.Size.Should().Be(System.Text.Encoding.UTF8.GetByteCount(fileContent));
        fileInfo.CreationTime.Should().Be(creationTime);
        fileInfo.LastWriteTime.Should().Be(lastWriteTime);
        fileInfo.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void GetFileInfo_當檔案不存在_應回傳null()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var nonexistentFile = TestPaths.FromWindowsPath(@"C:\data\missing.txt");

        var service = new FileManagerService(mockFileSystem);

        // Act
        var fileInfo = service.GetFileInfo(nonexistentFile);

        // Assert
        fileInfo.Should().BeNull();
    }
}
```

把 `TimeProvider` 一併注入後，測試可以固定備份時間，直接驗證完整路徑，不必用容易漏掉錯誤的前綴與副檔名斷言。這裡把毫秒放進檔名以降低碰撞機率；若系統要求嚴格唯一，還要再加入 GUID 或序號。

---

## 進階檔案操作測試技巧

### 測試檔案權限與錯誤情境

正式環境中，檔案操作常常會碰到各種例外狀況：權限不足、檔案被鎖定、路徑無效、磁碟空間不足等等。

傳統測試很難模擬這些錯誤情況，因為要在檔案系統層級製造這些問題並不容易。但透過抽象化的檔案系統介面，可以直接測試例外處理邏輯。

下面的 FilePermissionService 說明了如何處理檔案操作中的各種錯誤情況，使用 try-catch 模式來安全地處理例外。

```csharp
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
```

### 使用 NSubstitute 模擬錯誤情境

MockFileSystem 主要設計用於正常流程的測試，對於例外情況的模擬支援有限。需要模擬特定錯誤（像是權限不足、檔案被鎖定等）時，可以配合 NSubstitute 來建立更靈活的錯誤情境測試。

這種方法可以精確控制何時、如何拋出例外，確保錯誤處理邏輯真正有效。

```csharp
public class FilePermissionServiceTests
{
    [Fact]
    public void CanReadFile_當檔案無法讀取_應回傳false()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystem>();
        var mockFile = Substitute.For<IFile>();
        mockFileSystem.File.Returns(mockFile);

        var filePath = TestPaths.FromWindowsPath(@"C:\data\protected.txt");

        mockFile.Exists(filePath).Returns(true);
        mockFile.OpenRead(filePath).Returns(x => { throw new UnauthorizedAccessException("存取被拒"); });

        var service = new FilePermissionService(mockFileSystem);

        // Act
        var result = service.CanReadFile(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanWriteToDirectory_當目錄存在但無寫入權限_應回傳false()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystem>();
        var mockDirectory = Substitute.For<IDirectory>();
        var mockFile = Substitute.For<IFile>();
        var mockPath = Substitute.For<IPath>();

        mockFileSystem.Directory.Returns(mockDirectory);
        mockFileSystem.File.Returns(mockFile);
        mockFileSystem.Path.Returns(mockPath);

        var directoryPath = TestPaths.FromWindowsPath(@"C:\protected");

        mockDirectory.Exists(directoryPath).Returns(true);
        mockPath.Combine(directoryPath, Arg.Any<string>()).Returns(TestPaths.FromWindowsPath(@"C:\protected\temp_test.tmp"));
        mockFile.When(x => x.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
               .Do(x => { throw new UnauthorizedAccessException("存取被拒"); });

        var service = new FilePermissionService(mockFileSystem);

        // Act
        var result = service.CanWriteToDirectory(directoryPath);

        // Assert
        result.Should().BeFalse();
    }
}
```

---

## 大檔案與串流操作測試

實際應用常要處理大型檔案，例如日誌分析、資料處理與串流轉換。直接把整個檔案載入記憶體會增加耗用量，檔案夠大時甚至可能耗盡記憶體，因此應改用串流逐步處理。

### 串流處理服務

串流處理的關鍵是要正確管理資源、處理非同步操作、做好錯誤處理，還要確保記憶體效率。MockFileSystem 支援串流操作，可以在測試中模擬各種大小的檔案，而不需要實際建立大檔案。

```csharp
/// <summary>
/// 檔案串流處理服務
/// </summary>
public class StreamProcessorService
{
    private readonly IFileSystem _fileSystem;

    public StreamProcessorService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// 處理文字檔案，對每一行套用轉換函數
    /// </summary>
    /// <param name="inputFilePath">輸入檔案路徑</param>
    /// <param name="outputFilePath">輸出檔案路徑</param>
    /// <param name="transform">轉換函數</param>
    public async Task ProcessTextFileAsync(string inputFilePath, string outputFilePath,
        Func<string, string> transform)
    {
        if (!_fileSystem.File.Exists(inputFilePath))
        {
            throw new FileNotFoundException($"輸入檔案不存在: {inputFilePath}");
        }

        var outputDirectory = _fileSystem.Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !_fileSystem.Directory.Exists(outputDirectory))
        {
            _fileSystem.Directory.CreateDirectory(outputDirectory);
        }

        using var inputStream = _fileSystem.File.OpenRead(inputFilePath);
        using var outputStream = _fileSystem.File.Create(outputFilePath);
        using var reader = new StreamReader(inputStream);
        using var writer = new StreamWriter(outputStream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var transformedLine = transform(line);
            await writer.WriteLineAsync(transformedLine);
        }
    }

    /// <summary>
    /// 計算檔案的雜湊值
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>MD5 雜湊值</returns>
    public async Task<string> CalculateFileHashAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            throw new FileNotFoundException($"檔案不存在: {filePath}");
        }

        using var stream = _fileSystem.File.OpenRead(filePath);
        using var md5 = System.Security.Cryptography.MD5.Create();

        var hashBytes = await Task.Run(() => md5.ComputeHash(stream));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 比較兩個檔案是否相同
    /// </summary>
    /// <param name="filePath1">第一個檔案路徑</param>
    /// <param name="filePath2">第二個檔案路徑</param>
    /// <returns>檔案是否相同</returns>
    public async Task<bool> CompareFilesAsync(string filePath1, string filePath2)
    {
        if (!_fileSystem.File.Exists(filePath1) || !_fileSystem.File.Exists(filePath2))
        {
            return false;
        }

        // 先比較檔案大小
        var fileInfo1 = _fileSystem.FileInfo.New(filePath1);
        var fileInfo2 = _fileSystem.FileInfo.New(filePath2);

        if (fileInfo1.Length != fileInfo2.Length)
        {
            return false;
        }

        // 比較雜湊值
        var hash1 = await CalculateFileHashAsync(filePath1);
        var hash2 = await CalculateFileHashAsync(filePath2);

        return hash1.Equals(hash2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 統計檔案行數和字數
    /// </summary>
    /// <param name="filePath">檔案路徑</param>
    /// <returns>檔案統計資訊</returns>
    public async Task<FileStatistics> GetFileStatisticsAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            throw new FileNotFoundException($"檔案不存在: {filePath}");
        }

        using var stream = _fileSystem.File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        var stats = new FileStatistics();
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            stats.LineCount++;
            stats.CharacterCount += line.Length;
            stats.WordCount += line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        return stats;
    }

    /// <summary>
    /// 檔案統計資訊
    /// </summary>
    public class FileStatistics
    {
        public int LineCount { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
    }
}
```

### 串流操作測試

測試串流操作時，要驗證處理結果是否正確、確保大檔案處理不會造成記憶體問題、驗證 Stream 物件被正確釋放、還要測試處理過程中的例外情況。

使用 MockFileSystem 的好處是可以建立任意大小和內容的模擬檔案，完全不用擔心磁碟空間或處理速度問題。

```csharp
public class StreamProcessorServiceTests
{
    [Fact]
    public async Task ProcessTextFileAsync_當輸入檔案存在_應正確轉換並輸出()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var inputPath = TestPaths.FromWindowsPath(@"C:\input\data.txt");
        var outputPath = TestPaths.FromWindowsPath(@"C:\output\processed.txt");
        var inputContent = "line1\nline2\nline3";

        mockFileSystem.AddFile(inputPath, new MockFileData(inputContent));

        var service = new StreamProcessorService(mockFileSystem);

        // Act
        await service.ProcessTextFileAsync(inputPath, outputPath, line => line.ToUpper());

        // Assert
        mockFileSystem.File.Exists(outputPath).Should().BeTrue();
        var outputContent = mockFileSystem.File.ReadAllText(outputPath);
        var lines = outputContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);
        lines[0].Should().Be("LINE1");
        lines[1].Should().Be("LINE2");
        lines[2].Should().Be("LINE3");
    }

    [Fact]
    public async Task GetFileStatisticsAsync_當檔案存在_應回傳正確統計資訊()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\data\stats.txt");
        var fileContent = "第一行 有 三個 字\n第二行 有 四個 中文 字\n第三行有五個英文word here";

        mockFileSystem.AddFile(filePath, new MockFileData(fileContent));

        var service = new StreamProcessorService(mockFileSystem);

        // Act
        var stats = await service.GetFileStatisticsAsync(filePath);

        // Assert
        stats.Should().NotBeNull();
        stats.LineCount.Should().Be(3);
        stats.CharacterCount.Should().BeGreaterThan(0);
        stats.WordCount.Should().BeGreaterThan(0);
    }
}
```

---

## 實務整合範例：設定檔管理器

建立一個完整的範例，說明 System.IO.Abstractions 在實際專案中的應用。這個設定檔管理服務包含了前面學到的各種技巧。

### 完整的設定檔管理服務

```csharp
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
```

### 完整測試範例

這個章節說明如何為完整的設定檔管理服務建立測試套件。ConfigManagerService 整合了前面學到的所有概念：設定檔載入（具備錯誤處理與預設值回退）、設定檔儲存（格式化 JSON 輸出、自動建立目錄）、備份功能（帶時間戳記的備份、備份清單查詢、從備份還原）。

測試要涵蓋正常流程、邊界條件（檔案不存在、格式錯誤等）、錯誤處理（JSON 解析失敗）、還有多個功能組合使用的整合情況。

這個範例說明了在真實專案中如何應用 System.IO.Abstractions 來建立完整、可靠的檔案操作測試。

```csharp
using Microsoft.Extensions.Time.Testing;

public class ConfigManagerServiceTests
{
    [Fact]
    public async Task LoadAppSettingsAsync_當設定檔案不存在_應建立預設設定並回傳()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var configDirectory = TestPaths.FromWindowsPath(@"C:\app\config");

        var service = new ConfigManagerService(mockFileSystem, configDirectory);

        // Act
        var settings = await service.LoadAppSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.ApplicationName.Should().Be("Day17 FileSystem Testing Demo");
        settings.Version.Should().Be("1.0.0");
        settings.Database.Should().NotBeNull();
        settings.Logging.Should().NotBeNull();

        // 驗證預設設定檔案已被建立
        var settingsPath = TestPaths.Combine(configDirectory, "appsettings.json");
        mockFileSystem.File.Exists(settingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAppSettingsAsync_應正確序列化並儲存設定()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var configDirectory = TestPaths.FromWindowsPath(@"C:\app\config");

        var settings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "自訂應用程式",
            Version = "3.0.0",
            Database = new ConfigManagerService.DatabaseSettings
            {
                ConnectionString = "Server=prod;Database=ProdDb;",
                TimeoutSeconds = 120
            },
            Logging = new ConfigManagerService.LoggingSettings
            {
                Level = "Warning",
                EnableFileLogging = true,
                LogDirectory = "production_logs"
            }
        };

        var service = new ConfigManagerService(mockFileSystem, configDirectory);

        // Act
        await service.SaveAppSettingsAsync(settings);

        // Assert
        var settingsPath = TestPaths.Combine(configDirectory, "appsettings.json");
        mockFileSystem.File.Exists(settingsPath).Should().BeTrue();
        mockFileSystem.Directory.Exists(configDirectory).Should().BeTrue();

        var savedContent = mockFileSystem.File.ReadAllText(settingsPath);
        var deserializedSettings = System.Text.Json.JsonSerializer.Deserialize<ConfigManagerService.AppSettings>(savedContent);

        deserializedSettings.Should().NotBeNull();
        deserializedSettings!.ApplicationName.Should().Be("自訂應用程式");
        deserializedSettings.Version.Should().Be("3.0.0");
        deserializedSettings.Database.ConnectionString.Should().Be("Server=prod;Database=ProdDb;");
        deserializedSettings.Database.TimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public async Task LoadAppSettingsAsync_當設定檔案存在_應載入設定()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var configDirectory = TestPaths.FromWindowsPath(@"C:\app\config");
        var settingsPath = TestPaths.Combine(configDirectory, "appsettings.json");

        var existingSettings = new ConfigManagerService.AppSettings
        {
            ApplicationName = "測試應用程式",
            Version = "2.0.0",
            Database = new ConfigManagerService.DatabaseSettings
            {
                ConnectionString = "Server=test;Database=TestDb;",
                TimeoutSeconds = 60
            },
            Logging = new ConfigManagerService.LoggingSettings
            {
                Level = "Debug",
                EnableFileLogging = false,
                LogDirectory = "custom_logs"
            }
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(existingSettings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        mockFileSystem.AddFile(settingsPath, new MockFileData(jsonContent));

        var service = new ConfigManagerService(mockFileSystem, configDirectory);

        // Act
        var loadedSettings = await service.LoadAppSettingsAsync();

        // Assert
        loadedSettings.Should().NotBeNull();
        loadedSettings.ApplicationName.Should().Be("測試應用程式");
        loadedSettings.Version.Should().Be("2.0.0");
        loadedSettings.Database.ConnectionString.Should().Be("Server=test;Database=TestDb;");
        loadedSettings.Database.TimeoutSeconds.Should().Be(60);
        loadedSettings.Logging.Level.Should().Be("Debug");
        loadedSettings.Logging.EnableFileLogging.Should().BeFalse();
        loadedSettings.Logging.LogDirectory.Should().Be("custom_logs");
    }

    [Fact]
    public void BackupConfiguration_當設定檔案存在_應建立備份檔案()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var configDirectory = TestPaths.FromWindowsPath(@"C:\app\config");
        var settingsPath = TestPaths.Combine(configDirectory, "appsettings.json");
        var originalContent = """{"ApplicationName":"Test App","Version":"1.0.0"}""";
        var fakeTimeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 7, 23, 12, 34, 56, TimeSpan.Zero));

        mockFileSystem.AddFile(settingsPath, new MockFileData(originalContent));

        var service = new ConfigManagerService(mockFileSystem, configDirectory, fakeTimeProvider);

        // Act
        var backupPath = service.BackupConfiguration();

        // Assert
        backupPath.Should().Be(TestPaths.Combine(
            configDirectory, "backup", "appsettings_20260723_123456_000.json"));

        mockFileSystem.File.Exists(backupPath).Should().BeTrue();
        mockFileSystem.File.ReadAllText(backupPath).Should().Be(originalContent);

        // 驗證備份目錄已建立
        mockFileSystem.Directory.Exists(TestPaths.Combine(configDirectory, "backup")).Should().BeTrue();
    }

    [Fact]
    public void BackupConfiguration_當設定檔案不存在_應拋出FileNotFoundException()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var configDirectory = TestPaths.FromWindowsPath(@"C:\app\config");

        var service = new ConfigManagerService(mockFileSystem, configDirectory);

        // Act & Assert
        var action = () => service.BackupConfiguration();
        action.Should().Throw<FileNotFoundException>()
            .WithMessage("找不到要備份的設定檔案");
    }
}
```

---

## 最佳實務與注意事項

### 1. 選擇適當的測試策略

```csharp
// O 好的做法：使用 MockFileSystem 進行單元測試
[Fact]
public void ProcessFile_檔案存在_應處理成功()
{
    var mockFileSystem = new MockFileSystem();
    // ...
}

// X 避免：在單元測試中使用真實檔案系統
[Fact]
public void ProcessFile_真實檔案_應處理成功() // ← 這不是單元測試
{
    var realFileSystem = new FileSystem();
    // ...
}
```

### 2. 路徑處理的跨平台考量

```csharp
// O 好的做法：使用 Path.Combine
var configPath = _fileSystem.Path.Combine("configs", "app.json");

// X 避免：硬編碼路徑分隔符號
var configPath = "configs\\app.json"; // 只符合 Windows 的慣例
var configPath = "configs/app.json";  // Windows 通常可接受，但無法表達平台意圖
```

### 3. 適當的錯誤處理

```csharp
public async Task<bool> TrySaveFileAsync(string path, string content)
{
    try
    {
        await _fileSystem.File.WriteAllTextAsync(path, content);
        return true;
    }
    catch (DirectoryNotFoundException)
    {
        // 嘗試建立目錄後重試
        var directory = _fileSystem.Path.GetDirectoryName(path);
        _fileSystem.Directory.CreateDirectory(directory);

        try
        {
            await _fileSystem.File.WriteAllTextAsync(path, content);
            return true;
        }
        catch
        {
            return false;
        }
    }
    catch (UnauthorizedAccessException)
    {
        return false;
    }
    catch (IOException)
    {
        return false;
    }
}
```

### 4. 測試資料的組織

```csharp
public class FileTestDataHelper
{
    public static Dictionary<string, MockFileData> CreateTestFileStructure()
    {
        return new Dictionary<string, MockFileData>
        {
            [@"C:\app\configs\app.json"] = new MockFileData("""
                {
                  "apiUrl": "https://api.test.com",
                  "timeout": 30
                }
                """),
            [@"C:\app\logs\app.log"] = new MockFileData("2024-01-01 10:00:00 INFO Application started"),
            [@"C:\app\data\users.csv"] = new MockFileData("Name,Age\nJohn,25\nJane,30"),
            [@"C:\temp\"] = new MockDirectoryData()
        };
    }
}

// 在測試中使用
var mockFileSystem = new MockFileSystem(FileTestDataHelper.CreateTestFileStructure());
```

---

## 效能考量與限制

### MockFileSystem vs 真實檔案系統的差異

雖然 MockFileSystem 在功能上完全相容於真實檔案系統，但在效能特性上有顯著差異。了解這些差異有助於我們：

1. **測試策略選擇**：何時使用 MockFileSystem，何時使用整合測試
2. **效能預期**：理解測試執行速度差異的原因
3. **測試設計**：設計更有效率的測試

**效能差異的原因**：

- **記憶體 vs 磁碟**：MockFileSystem 在記憶體中操作，真實檔案系統需要磁碟 IO
- **系統呼叫**：MockFileSystem 不需要作業系統層級的檔案操作
- **檔案系統開銷**：沒有檔案系統權限檢查、快取管理等開銷

**實務觀察**：

在一般情況下，MockFileSystem 比真實檔案系統操作快 10-100 倍，具體差異取決於：

- 檔案大小和數量
- 磁碟類型（SSD vs HDD）
- 系統負載情況
- 檔案系統類型

記憶體內檔案系統省掉實際磁碟 I/O，大量檔案操作也不會明顯拖慢測試。

### 記憶體使用注意事項

雖然 MockFileSystem 帶來了速度優勢，但我們也需要注意記憶體使用的問題：

**記憶體考量重點**：

1. **模擬檔案大小**：MockFileData 會將檔案內容儲存在記憶體中
2. **檔案數量**：大量檔案可能會消耗較多記憶體
3. **測試隔離**：每個測試都應該使用新的 MockFileSystem 執行個體
4. **資源釋放**：確保測試結束後釋放記憶體

**最佳實務**：

- **適度模擬**：只建立測試必需的檔案
- **控制大小**：避免在測試中模擬超大檔案
- **測試分割**：將大型測試分解為較小的測試單元
- **記憶體監控**：在 CI/CD 環境中監控測試的記憶體使用

**實際建議**：

對於一般的業務邏輯測試，MockFileSystem 的記憶體使用並不會造成問題。但如果需要測試大檔案處理邏輯，可以考慮：

```csharp
[Fact]
public async Task ProcessLargeFile_使用串流_記憶體效率測試()
{
    // Arrange
    var mockFileSystem = new MockFileSystem();

    // 建立一個適中大小的測試檔案，而不是真正的大檔案
    var testContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}"));
    mockFileSystem.AddFile("test.txt", new MockFileData(testContent));

    var processor = new StreamProcessorService(mockFileSystem);

    // Act
    var result = await processor.GetFileStatisticsAsync("test.txt");

    // Assert
    result.LineCount.Should().Be(1000);
    result.WordCount.Should().Be(2000); // "Line 1", "Line 2", etc.
}
```

---

## 與其他測試技術的整合

### 與其他測試技術整合的考量

System.IO.Abstractions 可以與其他測試技術整合，但需要根據實際需求來決定：

**整合考量**：

1. **測試複雜度**：避免為了整合而增加不必要的複雜度
2. **維護成本**：每增加一個測試工具都會增加維護負擔
3. **團隊技能**：確保團隊熟悉所使用的測試工具組合
4. **實際價值**：評估整合帶來的測試價值是否值得額外成本

**實際範例**：

以目前的 FileManagerService 為例，它已經涵蓋了檔案操作的主要測試需求：

```csharp
    [Fact]
    public void CopyFileToDirectory_各種檔案名稱_應正確處理()
    {
        // Arrange
        var testCases = new[]
        {
            "simple.txt",
            "file with spaces.txt",
            "file-with-hyphens.txt",
            "file_with_underscores.txt"
        };

        var mockFileSystem = new MockFileSystem();
        var service = new FileManagerService(mockFileSystem);

        foreach (var fileName in testCases)
        {
            // Arrange
            var sourceFile = TestPaths.FromWindowsPath($@"C:\source\{fileName}");
            mockFileSystem.AddFile(sourceFile, new MockFileData("test content"));

            // Act
            var result = service.CopyFileToDirectory(sourceFile, TestPaths.FromWindowsPath(@"C:\target"));

            // Assert
            result.Should().Be(TestPaths.FromWindowsPath($@"C:\target\{fileName}"));
            mockFileSystem.File.Exists(result).Should().BeTrue();
        }
    }
```

### 與 NSubstitute 的組合使用

在某些複雜的情境中，我們可能需要同時使用 NSubstitute 和 System.IO.Abstractions：

**適用情境**：

1. **複合服務測試**：服務同時依賴檔案系統和其他外部服務
2. **行為驗證**：需要驗證檔案操作的同時記錄日誌
3. **錯誤處理整合**：檔案錯誤和業務邏輯錯誤的整合測試
4. **複雜模擬**：需要精確控制檔案系統行為的測試

**技術重點**：

- **多重模擬**：同時模擬檔案系統和其他相依
- **行為驗證**：確認方法被正確呼叫
- **狀態與行為**：結合狀態檢查和行為驗證

以 FilePermissionService 為例，它展示了如何使用 NSubstitute 模擬特定的檔案系統錯誤：

```csharp
[Fact]
public void CanReadFile_當檔案無法讀取_應回傳false()
{
    // Arrange
    var mockFileSystem = Substitute.For<IFileSystem>();
    var mockFile = Substitute.For<IFile>();
    mockFileSystem.File.Returns(mockFile);

    var filePath = @"C:\data\protected.txt";

    mockFile.Exists(filePath).Returns(true);
    mockFile.OpenRead(filePath).Returns(x => { throw new UnauthorizedAccessException("存取被拒"); });

    var service = new FilePermissionService(mockFileSystem);

    // Act
    var result = service.CanReadFile(filePath);

    // Assert
    result.Should().BeFalse();
}
```

---

## 今日小結

### 核心概念

- **System.IO.Abstractions**：將檔案操作邏輯抽象為可注入的服務
- **測試可控性**：透過 MockFileSystem 完全控制測試中的檔案系統
- **環境隔離**：測試不再依賴實際的檔案系統狀態

### 實戰技能

- **基礎重構**：將檔案相依程式碼改為使用 IFileSystem
- **測試設計**：使用 MockFileSystem 進行檔案系統模擬測試
- **完整服務測試**：涵蓋檔案管理、設定管理、串流處理等實務情境
- **整合測試策略**：了解何時使用 MockFileSystem，何時考慮其他測試工具

### 關鍵收穫

1. **速度提升**：MockFileSystem 比真實檔案操作快 10-100 倍
2. **可靠性增強**：測試不再受檔案系統狀態影響
3. **完整涵蓋**：能夠測試所有檔案相關的邊界條件和例外情況
4. **實務應用**：掌握設定檔管理、檔案備份、串流處理等真實情境的測試方法

用 System.IO.Abstractions 之後，測試不會再因為檔案權限失敗，也不必為了跑測試在磁碟上建一堆檔案再清掉。整套技術怎麼放進實際專案，前面的範例都走過一遍了。

## 延伸閱讀

- [System.IO.Abstractions GitHub](https://github.com/TestableIO/System.IO.Abstractions)
- [System.IO.Abstractions NuGet 套件](https://www.nuget.org/packages/System.IO.Abstractions/)
- [System.IO.Abstractions.TestingHelpers NuGet 套件](https://www.nuget.org/packages/System.IO.Abstractions.TestingHelpers/)

明天談驗證測試，用 FluentValidation Test Extensions 處理輸入資料的驗證規則怎麼測。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day17>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十七天。明天會介紹 Day 18 - 驗證測試：FluentValidation Test Extensions。**
