---
day: 7
title: "Day 07 - 相依替代入門 - 使用 NSubstitute"
sample: samples/day07
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Microsoft.Data.SqlClient
  - Microsoft.Extensions.Logging
  - Microsoft.Extensions.Logging.Abstractions
  - Microsoft.Extensions.Logging.Console
  - NSubstitute
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Microsoft.Testing.Extensions.TrxReport
  - Microsoft.Testing.Extensions.CodeCoverage
---

# Day 07 - 相依替代入門 - 使用 NSubstitute

## 前言

前幾篇已經建立單元測試的基礎：

- Day 01 的測試金字塔了解測試策略
- Day 02 打造第一個測試專案
- Day 03 深入 AAA 模式與 xUnit 框架
- Day 04 掌握各種斷言技巧
- Day 05 探索進階斷言與集合驗證
- Day 06 學會程式碼涵蓋率的實務應用

實際專案裡的程式碼很少獨立運作，通常會依賴資料庫、檔案系統、網路服務或系統時間。這些外部相依性會拖慢測試，也容易讓結果受環境影響。今天用測試替身（Test Double）與 NSubstitute 拆開這些相依性。

## 本日學習目標

- 深度理解測試替身（Test Double）的五大類型與應用時機
- 掌握相依性注入在測試中的重要性與實作策略
- 熟練使用 NSubstitute 進行介面模擬與行為設定
- 學會識別與替代外部相依性（資料庫、檔案、網路、時間）
- 理解 Mock 與 Stub 的差異，並實作 ILogger 的行為驗證
- 建立正確的測試替身思維，避免常見陷阱

## 為什麼需要測試替身？

在真實世界的軟體開發中，我們的程式碼很少是完全獨立的。它們會依賴各種外部資源：

```csharp
// 教學範例：刻意違反 SRP 原則來展示問題
// 實際專案中請避免這樣的設計，應遵循 SOLID 原則
public class FileBackupService
{
    public bool BackupFile(string filePath, string backupPath)
    {
        // 檔案系統操作
        if (!File.Exists(filePath)) return false;
        
        // 資料庫記錄
        using var connection = new SqlConnection("connectionString");
        connection.Open();
        
        // 網路操作
        var client = new HttpClient();
        var content = new StringContent($"{{\"file\":\"{filePath}\"}}");
        var response = client.PostAsync("backup-api", content).Result;
        
        // 時間相依
        var timestamp = DateTime.Now;
        
        return true;
    }
}
```

**範例說明**：上面的程式碼刻意違反單一職責原則（SRP），讓一個類別同時處理檔案、資料庫、網路通訊與時間。正式程式碼不應採用這種設計；這裡把問題集中在同一個類別，方便觀察程式碼為何難以測試。稍後實戰案例會用 SOLID 原則重構——那邊的版本較完整，回傳 `BackupResult` 而非 `bool`。

這樣的程式碼在測試時會遇到什麼問題？

### 直接相依的問題

1. **測試緩慢**：每次測試都要實際操作檔案、資料庫、網路
2. **環境相依**：需要特定的檔案、資料庫連線、網路環境
3. **不可重複**：時間、隨機數導致每次執行結果不同
4. **測試脆弱**：外部服務例外會導致測試失敗
5. **開發阻塞**：必須等待外部系統準備就緒

以下逐項拆解這些問題：

#### 檔案系統相依問題

- **問題**：測試需要實際檔案存在，且檔案狀態可能被其他測試影響
- **影響**：測試結果不穩定，需要複雜的前置與清理作業
- **解決方案**：抽象檔案操作為 `IFileSystem` 介面

#### 時間相依問題

- **問題**：`DateTime.Now` 每次執行都不同，無法預測結果
- **影響**：無法驗證時間相關的業務邏輯
- **解決方案**：建立 `IDateTimeProvider` 來控制時間

#### 資料庫相依問題

- **問題**：需要實際資料庫連線，測試之間會互相影響資料狀態
- **影響**：測試緩慢且需要複雜的資料準備與清理
- **解決方案**：用 `IBackupRepository` 抽象資料存取

#### 記錄問題

- **問題**：直接使用 `Console.WriteLine` 無法在測試中驗證記錄行為
- **影響**：無法確保重要的記錄資訊有正確輸出
- **解決方案**：使用 `ILogger<T>` 讓記錄行為可以被測試

## 為什麼多數開發者無法跨越單元測試這道門檻？

許多程式開發者在學習單元測試時會卡關，主要原因是不理解**相依注入與直接相依的差異**。即使現在 .NET Core 預設整合了 DI（Dependency Injection），仍有許多人對這個概念模糊不清。

### 直接相依 vs 相依注入

```csharp
// 直接相依：難以測試
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        // 直接建立相依物件
        var repository = new OrderRepository();
        var emailService = new EmailService();
        
        repository.Save(order);
        emailService.SendConfirmation(order.Email);
    }
}

// 相依注入：容易測試
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;
    
    public OrderService(IOrderRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public void ProcessOrder(Order order)
    {
        _repository.Save(order);
        _emailService.SendConfirmation(order.Email);
    }
}
```

### SOLID 原則對單元測試的重要性

SOLID 原則不只是 OOP 的最佳實務，對單元測試也有直接且重要的影響：

- **S - 單一職責原則**：每個類別只有一個變更理由，讓測試聚焦於單一行為
- **O - 開放封閉原則**：對擴展開放，對修改封閉；介面讓測試可以替換實作
- **L - 里氏替換原則**：介面的實作可以互相替換，包括測試替身
- **I - 介面隔離原則**：小而專注的介面更容易模擬
- **D - 相依反轉原則**：相依於抽象而非具體實作，這是測試替身的基礎

沒有遵循 SOLID 原則的程式碼，通常也很難寫出好的單元測試。

### 隔離的目標

- **快速執行**：測試應該在毫秒級完成
- **獨立執行**：不依賴外部資源與環境
- **可重複執行**：相同輸入產生相同結果
- **專注邏輯**：只測試業務邏輯，排除外部干擾

## 相依替換工具介紹

要拆掉相依性，得有個工具幫你造出測試替身。這類工具有幾個名字：

### 技術術語說明

- **Mocking Framework（模擬框架）**：最常見的國際通用術語
- **Test Double Framework（測試替身框架）**：更精確的技術名稱，涵蓋所有測試替身類型
- **Mock Library/Tool（模擬程式庫/工具）**：實用導向的稱呼
- **相依替換工具**：直觀易懂的中文表達，清楚說明工具用途

本文中我們會交替使用這些術語，它們指的都是同一類工具。.NET 生態系統中有兩個主要的測試替身框架：Moq 和 NSubstitute。

### Moq 與 NSubstitute 比較

#### Moq

Moq 在 .NET 上出現得早，用的人也最多，功能齊全，社群龐大。

> 事實上，在 .NET 生態系統中：
>
> - Rhino Mocks 可能是更早期的模擬框架之一 (不過已經在很久之前就已經不再維護)
> - TypeMock Isolator 也是早期的商業解決方案
> - Moq 雖然不是最早的，但確實是後來最受歡迎和使用最廣泛的開源選擇

**優點：**

- 功能完整且強大
- 社群資源豐富，範例多
- 支援複雜的參數匹配
- 驗證功能詳細
- 效能表現較佳

**缺點：**

- 語法相對複雜
- 學習曲線較陡峭
- API 設計較為繁瑣

```csharp
// Moq 語法範例
var mock = new Mock<IUserRepository>();
mock.Setup(x => x.GetById(It.IsAny<int>()))
    .Returns(new User { Id = 1, Name = "John" });

// 驗證
mock.Verify(x => x.GetById(1), Times.Once);
```

#### NSubstitute

NSubstitute 是後起之秀的測試替身框架（Test Double Framework），專注於提供簡潔直觀的 API，讓測試程式碼更容易閱讀和維護。

> 一開始我所接觸到的相依替換工具就是 NSubstitute (通常簡稱 NSub)，是在 91 哥的的課程裡所學的，之後在 91 哥所推薦以及翻譯的書籍 `單元測試的藝術(第二版) - The Art of Unit Testing: with examples in C# Second Edition` 裡，作者也是介紹使用 NSubsititute，後來就一直慣用下去，雖然中途有短暫時間因為工作關係而用了 Moq，雖然說功能上差不多，但使用直覺上就真的差異很大。就是用得不習慣。

**優點：**

- 語法簡潔，接近自然語言
- 學習曲線平緩
- API 設計一致性佳
- 程式碼可讀性高

**缺點：**

- 功能相對較少
- 社群資源不如 Moq 豐富
- 某些進階功能支援有限

```csharp
// NSubstitute 語法範例
var substitute = Substitute.For<IUserRepository>();
substitute.GetById(Arg.Any<int>())
          .Returns(new User { Id = 1, Name = "John" });

// 驗證
substitute.Received(1).GetById(1);
```

### Moq 的爭議事件

2023 年 8 月，Moq 出了一件事，整個 .NET 社群吵翻了：

#### 事件經過

- Moq 4.20.0 版本引入了一個名為 `SponsorLink` 的相依套件
- 這個套件會在背景收集開發者的電子郵件和專案資訊
- 收集到的資料會被傳送到第三方服務
- 整個過程是默默地進行，沒有明確告知使用者

#### 社群反應

- 開發者社群強烈抗議這種未經同意的資料收集行為
- 許多開發者認為這違反了隱私權和信任原則
- GitHub 上出現大量的 Issue 和負面評論
- 企業和開源專案開始移除對 Moq 的相依

#### 後續處理

Moq 的維護者後來移除了爭議套件，也發布了修正版本，但傷害已經造成：

- **信任危機**：許多開發者對 Moq 失去信心
- **替代方案興起**：更多團隊開始評估 NSubstitute、FakeItEasy 等框架
- **企業政策**：部分公司明確禁止使用 Moq
- **長期影響**：即使問題已修正，仍有質疑聲浪持續

### 為什麼我們選擇 NSubstitute？

基於上述分析，本教學選擇 NSubstitute 的原因：

1. **語法簡潔**：更容易學習和閱讀
2. **信任度高**：沒有隱私爭議的包袱
3. **功能足夠**：滿足大部分單元測試需求
4. **社群健康**：開放透明的開發模式

### NSubstitute 詳細介紹

NSubstitute 是為 .NET 設計的測試替身框架（Test Double Framework），也屬於 Mocking Framework，差別在於它把 API 壓到最少，讀起來像在講白話。

### 主要功能特色

- **簡潔語法**：`Substitute.For<T>()` 快速建立介面替身
- **直觀設定**：`method.Returns(value)` 設定回傳值
- **行為驗證**：`Received()` 驗證方法是否被正確呼叫
- **引數匹配**：`Arg.Any<T>()` 彈性的參數匹配
- **例外模擬**：`Throws()` 模擬例外情況

### 相關資源

- **官方網站**：<https://nsubstitute.github.io/>
- **GitHub 專案**：<https://github.com/nsubstitute/NSubstitute>
- **NuGet 套件**：<https://www.nuget.org/packages/NSubstitute/>
- **官方文件**：<https://nsubstitute.github.io/help/>
- **範例教學**：<https://nsubstitute.github.io/help/getting-started/>
- **Moq 爭議參考**：
  - <https://www.bleepingcomputer.com/news/security/popular-open-source-project-moq-criticized-for-quietly-collecting-data/>
  - <https://github.com/moq/moq/issues/1372>

## Test Double 類型解析

測試替身（Test Double）是 Gerard Meszaros 在《xUnit Test Patterns》中提出的概念，就像電影中的替身演員一樣，在測試中替代真實的相依物件。

- [XUnitPatterns.com](http://xunitpatterns.com/index.html)
  - [Test Double at XUnitPatterns.com](http://xunitpatterns.com/Test%20Double.html)
- [xUnit Test Patterns: Refactoring Test Code by Gerard Meszaros (2007) | martinFowler.com](https://martinfowler.com/books/meszaros.html)
- [xUnit Test Patterns: Refactoring Test Code (Hardcover) - 暫譯: xUnit 測試模式：重構測試程式碼 (精裝版)](https://www.tenlong.com.tw/products/9780131495050)

### 1. Dummy - 填充物件

僅用於滿足方法簽章，不會被實際使用：

```csharp
public interface IEmailService
{
    void SendEmail(string to, string subject, string body, ILogger logger);
}

[Fact]
public void ProcessOrder_不使用Email_應成功處理訂單()
{
    // Dummy：只是為了滿足參數要求，不會被呼叫
    var dummyLogger = Substitute.For<ILogger>();
    var order = new Order { Id = 1, Email = "john@example.com" };
    var service = new OrderService(Substitute.For<IOrderRepository>(),
                                   Substitute.For<IEmailService>());

    service.ProcessOrder(order, dummyLogger);

    // 不關心 logger 是否被呼叫
}
```

### 2. Stub - 預設回傳值 (預設常式)

提供預先定義的回傳值，用於測試特定情境：

```csharp
[Fact]
public void GetUser_有效的使用者ID_應回傳使用者資料()
{
    // Stub：預設回傳值
    var stubRepository = Substitute.For<IUserRepository>();
    stubRepository.GetById(123).Returns(new User { Id = 123, Name = "John" });
    
    var service = new UserService(stubRepository);
    var actual = service.GetUser(123);
    
    Assert.Equal("John", actual.Name);
    // 不關心 GetById 被呼叫了幾次
}
```

### 3. Fake - 簡化實作

有實際功能但簡化的實作，通常用於整合測試：

```csharp
public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();
    
    public User? GetById(int id) => _users.TryGetValue(id, out var user) ? user : null;
    public void Save(User user) => _users[user.Id] = user;
}

[Fact]
public void CreateUser_建立使用者_應儲存並回傳使用者資料()
{
    // Fake：有真實邏輯的簡化實作
    var fakeRepository = new FakeUserRepository();
    var service = new UserService(fakeRepository);
    
    service.CreateUser(new User { Id = 1, Name = "John" });
    var actual = service.GetUser(1);
    
    Assert.Equal("John", actual.Name);
}
```

### 4. Spy - 記錄呼叫

記錄被如何呼叫，可以事後驗證：

```csharp
[Fact]
public void CreateUser_建立使用者_應記錄使用者建立資訊()
{
    var spyLogger = Substitute.For<ILogger>();
    var repository = Substitute.For<IUserRepository>();
    
    var service = new UserService(repository, spyLogger);
    service.CreateUser(new User { Name = "John" });
    
    // Spy：驗證是否被正確呼叫
    spyLogger.Received(1).LogInformation("User created: John");
}
```

> 注意：這段只在示意 Spy 的概念。`LogInformation` 是擴充方法，NSubstitute 攔截不到，照這樣寫實際執行會失敗；正確做法是驗證底層的 `Log` 方法，後面「ILogger 特殊處理」一節會示範。

### 5. Mock - 行為驗證

預設期望的互動行為，測試失敗如果期望沒有滿足：

```csharp
[Fact]
public void RegisterUser_註冊使用者_應發送歡迎郵件()
{
    var mockEmailService = Substitute.For<IEmailService>();
    var repository = Substitute.For<IUserRepository>();
    
    var service = new UserService(repository, mockEmailService);
    service.RegisterUser("john@example.com", "John");
    
    // Mock：驗證特定的互動行為
    mockEmailService.Received(1).SendWelcomeEmail("john@example.com", "John");
}
```

## 實戰案例：檔案備份服務重構

以下用檔案備份服務示範如何把難以測試的程式碼改成可測試的設計。

### 原始程式碼（不可測試）

```csharp
public class FileBackupService
{
    public BackupResult BackupFile(string sourcePath, string destinationPath)
    {
        try
        {
            // 直接依賴檔案系統
            if (!File.Exists(sourcePath))
            {
                return new BackupResult { Success = false, Message = "Source file not found" };
            }
            
            var fileInfo = new FileInfo(sourcePath);
            if (fileInfo.Length > 100 * 1024 * 1024) // 100MB
            {
                return new BackupResult { Success = false, Message = "File too large" };
            }
            
            // 直接依賴時間
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{timestamp}{Path.GetExtension(sourcePath)}";
            var fullBackupPath = Path.Combine(destinationPath, backupFileName);
            
            // 執行備份
            File.Copy(sourcePath, fullBackupPath);
            
            // 直接依賴資料庫
            using var connection = new SqlConnection("Data Source=.;Initial Catalog=BackupDB;Integrated Security=true");
            connection.Open();
            var command = new SqlCommand(
                "INSERT INTO BackupHistory (SourcePath, BackupPath, BackupTime) VALUES (@source, @backup, @time)", 
                connection);
            command.Parameters.AddWithValue("@source", sourcePath);
            command.Parameters.AddWithValue("@backup", fullBackupPath);
            command.Parameters.AddWithValue("@time", DateTime.Now);
            command.ExecuteNonQuery();
            
            return new BackupResult { Success = true, BackupPath = fullBackupPath };
        }
        catch (Exception ex)
        {
            // 直接使用 Console.WriteLine（無法測試記錄行為）
            Console.WriteLine($"Backup failed: {ex.Message}");
            return new BackupResult { Success = false, Message = ex.Message };
        }
    }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? BackupPath { get; set; }
}
```

### 問題分析

這段程式碼有以下測試問題：

1. **檔案系統相依**：需要實際檔案存在，測試環境難以控制
2. **時間相依**：每次執行產生不同的檔案名稱，結果無法預測
3. **資料庫相依**：需要 SQL Server 連線，測試緩慢且環境複雜
4. **記錄問題**：無法驗證錯誤記錄行為，缺乏可觀測性

要讓 FileBackupService 變得可測試，我們需要將這些外部相依抽象化，遵循相依反轉原則：

**解決策略**：

- 將檔案操作抽象為 `IFileSystem`
- 將時間取得抽象為 `IDateTimeProvider`  
- 將資料存取抽象為 `IBackupRepository`
- 使用 `ILogger<T>` 進行結構化記錄

### 重構步驟

接著套用 SOLID 原則，尤其是相依性反轉，重構這段難以測試的程式碼。

#### 步驟 1：抽取介面（介面隔離原則）

先為每個外部相依性建立職責單純的小介面：

```csharp
public interface IFileSystem
{
    IPath Path { get; }
    bool FileExists(string path);
    IFileInfo GetFileInfo(string path);
    void CopyFile(string sourcePath, string destinationPath);
}

public interface IPath
{
    string GetFileNameWithoutExtension(string path);
    string GetExtension(string path);
    string Combine(string path1, string path2);
}

public interface IFileInfo
{
    long Length { get; }
    string Name { get; }
    string FullName { get; }
}

public interface IDateTimeProvider
{
    DateTime Now { get; }
}

public interface IBackupRepository
{
    Task SaveBackupHistoryAsync(string sourcePath, string backupPath, DateTime backupTime);
}
```

#### 步驟 2：相依性注入重構（相依反轉原則）

接著，我們重構 FileBackupService，讓它相依於抽象而非具體實作，同時遵循單一職責原則：

```csharp
/// <summary>
/// class FileBackupService - 檔案備份服務
/// </summary>
public class FileBackupService
{
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<FileBackupService> _logger;

    /// <summary>
    /// 檔案備份服務建構子
    /// </summary>
    /// <param name="fileSystem">檔案系統</param>
    /// <param name="dateTimeProvider">日期時間提供者</param>
    /// <param name="backupRepository">備份資料庫</param>
    /// <param name="logger">日誌記錄器</param>
    public FileBackupService(IFileSystem fileSystem,
                             IDateTimeProvider dateTimeProvider,
                             IBackupRepository backupRepository,
                             ILogger<FileBackupService> logger)
    {
        this._fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this._dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        this._backupRepository = backupRepository ?? throw new ArgumentNullException(nameof(backupRepository));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 備份檔案到指定路徑
    /// </summary>
    /// <param name="sourcePath">來源路徑</param>
    /// <param name="destinationPath">目標路徑</param>
    /// <returns></returns>
    public async Task<BackupResult> BackupFileAsync(string sourcePath, string destinationPath)
    {
        try
        {
            this._logger.LogInformation("Starting backup from {SourcePath} to {DestinationPath}",
                                        sourcePath, destinationPath);

            if (!this._fileSystem.FileExists(sourcePath))
            {
                const string message = "Source file not found";
                this._logger.LogWarning("Backup failed: {Message}. Source: {SourcePath}", message, sourcePath);
                return new BackupResult { Success = false, Message = message };
            }

            var fileInfo = this._fileSystem.GetFileInfo(sourcePath);
            if (fileInfo.Length > 100 * 1024 * 1024) // 100MB
            {
                const string message = "File too large";
                this._logger.LogWarning("Backup failed: {Message}. File size: {Size} bytes", message, fileInfo.Length);
                return new BackupResult { Success = false, Message = message };
            }

            var timestamp = this._dateTimeProvider.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{this._fileSystem.Path.GetFileNameWithoutExtension(sourcePath)}_{timestamp}{this._fileSystem.Path.GetExtension(sourcePath)}";
            var fullBackupPath = this._fileSystem.Path.Combine(destinationPath, backupFileName);

            this._fileSystem.CopyFile(sourcePath, fullBackupPath);

            await this._backupRepository.SaveBackupHistoryAsync(sourcePath, fullBackupPath, this._dateTimeProvider.Now);

            this._logger.LogInformation("Backup completed successfully. Backup path: {BackupPath}", fullBackupPath);

            return new BackupResult { Success = true, BackupPath = fullBackupPath };
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Backup failed for {SourcePath}", sourcePath);
            return new BackupResult { Success = false, Message = ex.Message };
        }
    }
}
```

#### 步驟 3：具體實作（正式環境用）

抽象介面在正式環境仍要有真實實作。範例專案的 `src/Day07.Refactored/Implementations/` 提供對應的包裝器：

```csharp
public class FileSystemWrapper : IFileSystem
{
    public IPath Path { get; } = new PathWrapper();

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public IFileInfo GetFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);
        return new FileInfoWrapper(fileInfo);
    }

    public void CopyFile(string sourcePath, string destinationPath)
    {
        File.Copy(sourcePath, destinationPath);
    }
}

public class PathWrapper : IPath
{
    public string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

    public string GetExtension(string path) => System.IO.Path.GetExtension(path);

    public string Combine(string path1, string path2) => System.IO.Path.Combine(path1, path2);
}

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.Now;
}
```

`FileInfoWrapper`（包裝 `FileInfo`）與 `SqlBackupRepository`（實作 `IBackupRepository`）的完整程式碼在範例專案同一個目錄。這層包裝器薄到幾乎沒有邏輯，這是刻意的——邏輯留在服務裡讓測試涵蓋，包裝器只負責轉接真實 API。

## NSubstitute 實戰應用

現在我們可以使用 NSubstitute 為重構後的程式碼撰寫測試：

### 安裝 NSubstitute

```bash
dotnet add package NSubstitute
dotnet add package AwesomeAssertions
dotnet add package Microsoft.Extensions.Logging
```

測試專案本身還需要 xUnit v3 與測試平台套件：`xunit.v3.mtp-v2`、`Microsoft.NET.Test.Sdk`、`xunit.runner.visualstudio`（IDE 測試總管用）與 `Microsoft.Testing.Extensions.TrxReport`。範例專案採中央套件版本管理（CPM），版本統一寫在 `samples/day07/Directory.Packages.props`，csproj 不寫版本號。

### 基本測試設定

```csharp
public class FileBackupServiceTests
{
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<FileBackupService> _logger;
    private readonly FileBackupService _sut; // System Under Test
    
    public FileBackupServiceTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        var path = Substitute.For<IPath>();
        path.GetFileNameWithoutExtension(Arg.Any<string>()).Returns(callInfo =>
            callInfo.Arg<string>().Split('\\').Last().Split('.').First());
        path.GetExtension(Arg.Any<string>()).Returns(callInfo =>
            $".{callInfo.Arg<string>().Split('.').Last()}");
        path.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(callInfo =>
            $"{callInfo.ArgAt<string>(0)}\\{callInfo.ArgAt<string>(1)}");
        _fileSystem.Path.Returns(path);
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _backupRepository = Substitute.For<IBackupRepository>();
        _logger = Substitute.For<ILogger<FileBackupService>>();

        _sut = new FileBackupService(_fileSystem, _dateTimeProvider, _backupRepository, _logger);
    }
}
```

留意 `IPath` 那一段設定：`IFileSystem.Path` 是巢狀替身，NSubstitute 對沒設定的字串方法一律回傳空字串。少了這段，服務組出來的 `BackupPath` 會是空字串，成功情境的斷言就對不上——這是巢狀替身最容易踩到的坑。

### Stub 測試：預設回傳值

```csharp
[Fact]
public async Task BackupFileAsync_來源檔案存在且大小合理_應回傳成功結果()
{
    // Arrange - 設定 Stub 行為
    var sourcePath = @"C:\source\test.txt";
    var destinationPath = @"C:\backup";
    var testTime = new DateTime(2024, 1, 1, 12, 0, 0);
    var expectedBackupPath = @"C:\backup\test_20240101_120000.txt";

    _fileSystem.FileExists(sourcePath).Returns(true);

    var fileInfo = Substitute.For<IFileInfo>();
    fileInfo.Length.Returns(1024);
    _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

    _dateTimeProvider.Now.Returns(testTime);

    // Act
    var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

    // Assert
    actual.Success.Should().BeTrue();
    actual.BackupPath.Should().Be(expectedBackupPath);
}
```

### Mock 測試：行為驗證

```csharp
[Fact]
public async Task BackupFileAsync_來源檔案不存在_應記錄警告並回傳失敗()
{
    // Arrange
    var sourcePath = @"C:\nonexistent\test.txt";
    var destinationPath = @"C:\backup";

    _fileSystem.FileExists(sourcePath).Returns(false);

    // Act
    var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

    // Assert - 驗證狀態
    actual.Success.Should().BeFalse();
    actual.Message.Should().Be("Source file not found");
    actual.BackupPath.Should().BeNull();

    // Assert - 驗證行為（Mock）
    _logger.Received(1).Log(
        LogLevel.Warning,
        Arg.Any<EventId>(),
        Arg.Is<object>(v => (v.ToString() ?? "").Contains("Source file not found")),
        null,
        Arg.Any<Func<object, Exception?, string>>());

    // 確保沒有進行實際的檔案操作
    _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>());
}
```

## NSubstitute 核心功能詳解

### 1. 基本替代語法

```csharp
// 建立介面替代
var substitute = Substitute.For<IService>();

// 建立類別替代（需要虛擬成員）
var classSubstitute = Substitute.For<BaseService>();

// 建立多重介面替代
var multiSubstitute = Substitute.For<IService, IDisposable>();
```

### 2. 回傳值設定

```csharp
// 基本回傳值
_repository.GetById(1).Returns(new User { Id = 1, Name = "John" });

// 條件回傳值
_calculator.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(x => (int)x[0] + (int)x[1]);

// 針對任何參數回傳
_service.Process(Arg.Any<string>()).Returns("processed");

// 回傳序列值
_generator.GetNext().Returns(1, 2, 3, 4, 5);

// 拋出例外（需要 using NSubstitute.ExceptionExtensions;）
_service.RiskyOperation().Throws(new InvalidOperationException("Something went wrong"));
```

### 3. 引數匹配

```csharp
// 精確匹配
_service.Process("exact").Returns("result");

// 任意值匹配
_service.Process(Arg.Any<string>()).Returns("result");

// 條件匹配
_service.Process(Arg.Is<string>(x => x.StartsWith("test"))).Returns("result");

// 引數擷取
string? capturedArg = null;
_service.Process(Arg.Do<string>(x => capturedArg = x)).Returns("result");
```

### 4. 呼叫驗證

```csharp
// 驗證被呼叫
_service.Received().Process("test");

// 驗證呼叫次數
_service.Received(2).Process(Arg.Any<string>());

// 驗證未被呼叫
_service.DidNotReceive().Delete(Arg.Any<int>());

// 驗證任意參數呼叫
_service.ReceivedWithAnyArgs().Process(default);
```

### 5. ILogger 特殊處理

由於 ILogger 的擴充方法特性，需要特殊的驗證方式。在 .NET 的 ILogger\<T\> 中，LogInformation、LogWarning、LogError 等都是擴充方法，無法直接用 NSubstitute 模擬。我們需要驗證底層的 Log 方法：

```csharp
[Fact]
public async Task BackupFileAsync_檔案不存在_應記錄警告資訊()
{
    // Arrange
    var sourcePath = @"C:\nonexistent\test.txt";
    var destinationPath = @"C:\backup";

    _fileSystem.FileExists(sourcePath).Returns(false);

    // Act
    var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

    // Assert
    actual.Success.Should().BeFalse();

    // 驗證 ILogger 的 Log 方法被正確呼叫
    _logger.Received(1).Log(
        LogLevel.Warning,
        Arg.Any<EventId>(),
        Arg.Is<object>(v => (v.ToString() ?? "").Contains("Source file not found")),
        null,
        Arg.Any<Func<object, Exception?, string>>());
}

[Fact]
public async Task BackupFileAsync_發生例外時_應記錄錯誤並回傳失敗()
{
    // Arrange
    var sourcePath = @"C:\source\test.txt";
    var destinationPath = @"C:\backup";
    var expectedException = new IOException("Disk full");

    _fileSystem.FileExists(sourcePath).Returns(true);

    var fileInfo = Substitute.For<IFileInfo>();
    fileInfo.Length.Returns(1024);
    _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

    _fileSystem.When(x => x.CopyFile(Arg.Any<string>(), Arg.Any<string>()))
               .Do(x => throw expectedException);

    // Act
    var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

    // Assert
    actual.Success.Should().BeFalse();
    actual.Message.Should().Be("Disk full");

    // 驗證錯誤記錄
    _logger.Received(1).Log(
        LogLevel.Error,
        Arg.Any<EventId>(),
        Arg.Is<object>(v => (v.ToString() ?? "").Contains("Backup failed")),
        expectedException,
        Arg.Any<Func<object, Exception?, string>>());
}
```

**注意**：ILogger 的擴充方法驗證比較複雜，實務上也可以考慮使用 Microsoft.Extensions.Logging.Testing 套件來簡化測試。

## 完整測試案例

```csharp
/// <summary>
/// class FileBackupServiceTests - 檔案備份服務測試
/// </summary>
public class FileBackupServiceTests
{
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackupRepository _backupRepository;
    private readonly ILogger<FileBackupService> _logger;
    private readonly FileBackupService _sut; // System Under Test

    /// <summary>
    /// 檔案備份服務測試建構子
    /// </summary>
    public FileBackupServiceTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        var path = Substitute.For<IPath>();
        path.GetFileNameWithoutExtension(Arg.Any<string>()).Returns(callInfo =>
            callInfo.Arg<string>().Split('\\').Last().Split('.').First());
        path.GetExtension(Arg.Any<string>()).Returns(callInfo =>
            $".{callInfo.Arg<string>().Split('.').Last()}");
        path.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(callInfo =>
            $"{callInfo.ArgAt<string>(0)}\\{callInfo.ArgAt<string>(1)}");
        _fileSystem.Path.Returns(path);
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _backupRepository = Substitute.For<IBackupRepository>();
        _logger = Substitute.For<ILogger<FileBackupService>>();

        _sut = new FileBackupService(_fileSystem, _dateTimeProvider, _backupRepository, _logger);
    }

    [Fact]
    public async Task BackupFileAsync_來源檔案存在且大小合理_應回傳成功結果()
    {
        // Arrange - 設定 Stub 行為
        var sourcePath = @"C:\source\test.txt";
        var destinationPath = @"C:\backup";
        var testTime = new DateTime(2024, 1, 1, 12, 0, 0);
        var expectedBackupPath = @"C:\backup\test_20240101_120000.txt";

        _fileSystem.FileExists(sourcePath).Returns(true);

        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Length.Returns(1024);
        _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

        _dateTimeProvider.Now.Returns(testTime);

        // Act
        var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert
        actual.Success.Should().BeTrue();
        actual.BackupPath.Should().Be(expectedBackupPath);
    }

    [Fact]
    public async Task BackupFileAsync_來源檔案不存在_應記錄警告並回傳失敗()
    {
        // Arrange
        var sourcePath = @"C:\nonexistent\test.txt";
        var destinationPath = @"C:\backup";

        _fileSystem.FileExists(sourcePath).Returns(false);

        // Act
        var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert - 驗證狀態
        actual.Success.Should().BeFalse();
        actual.Message.Should().Be("Source file not found");
        actual.BackupPath.Should().BeNull();

        // Assert - 驗證行為（Mock）
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => (v.ToString() ?? "").Contains("Source file not found")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        // 確保沒有進行實際的檔案操作
        _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BackupFileAsync_來源檔案存在且備份成功_應記錄資訊並回傳成功結果()
    {
        // Arrange
        var sourcePath = @"C:\source\document.pdf";
        var destinationPath = @"C:\backup";
        var testTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var expectedBackupPath = @"C:\backup\document_20240115_143000.pdf";

        _fileSystem.FileExists(sourcePath).Returns(true);

        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Length.Returns(1024 * 1024); // 1MB
        _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

        _dateTimeProvider.Now.Returns(testTime);

        // Act
        var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert - 狀態驗證
        actual.Success.Should().BeTrue();
        actual.BackupPath.Should().Be(expectedBackupPath);

        // Assert - 行為驗證
        _fileSystem.Received(1).CopyFile(sourcePath, expectedBackupPath);
        await _backupRepository.Received(1).SaveBackupHistoryAsync(sourcePath, expectedBackupPath, testTime);

        // Assert - 記錄行為驗證
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => (v.ToString() ?? "").Contains("Starting backup")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => (v.ToString() ?? "").Contains("Backup completed successfully")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task BackupFileAsync_來源檔案大小超過限制_應記錄警告並回傳失敗()
    {
        // Arrange
        var sourcePath = @"C:\source\largefile.zip";
        var destinationPath = @"C:\backup";
        var largeFileSize = 200 * 1024 * 1024; // 200MB

        _fileSystem.FileExists(sourcePath).Returns(true);

        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Length.Returns(largeFileSize);
        _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

        // Act
        var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert
        actual.Success.Should().BeFalse();
        actual.Message.Should().Be("File too large");

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => (v.ToString() ?? "").Contains("File too large")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        // 確保沒有執行備份操作
        _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>());
        await _backupRepository.DidNotReceive().SaveBackupHistoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task BackupFileAsync_資料庫儲存歷史記錄時拋出例外_應記錄錯誤並回傳失敗()
    {
        // Arrange
        var sourcePath = @"C:\source\test.txt";
        var destinationPath = @"C:\backup";
        var expectedException = new InvalidOperationException("Database connection failed");

        _fileSystem.FileExists(sourcePath).Returns(true);

        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Length.Returns(1024);
        _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

        _dateTimeProvider.Now.Returns(new DateTime(2024, 1, 1));
        _backupRepository.When(x => x.SaveBackupHistoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()))
                         .Do(x => throw expectedException);

        // Act
        var actual = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert
        actual.Success.Should().BeFalse();
        actual.Message.Should().Be("Database connection failed");

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => (v.ToString() ?? "").Contains("Backup failed")),
            expectedException,
            Arg.Any<Func<object, Exception?, string>>());

        // 確保檔案複製仍然執行了（在例外之前）
        _fileSystem.Received(1).CopyFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BackupFileAsync_多次呼叫時_應產生唯一備份路徑()
    {
        // Arrange
        var sourcePath = @"C:\source\test.txt";
        var destinationPath = @"C:\backup";
        var firstTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var secondTime = new DateTime(2024, 1, 1, 10, 0, 1);

        _fileSystem.FileExists(sourcePath).Returns(true);

        var fileInfo = Substitute.For<IFileInfo>();
        fileInfo.Length.Returns(1024);
        _fileSystem.GetFileInfo(sourcePath).Returns(fileInfo);

        _dateTimeProvider.Now.Returns(firstTime, secondTime);

        // Act
        var firstResult = await _sut.BackupFileAsync(sourcePath, destinationPath);
        var secondResult = await _sut.BackupFileAsync(sourcePath, destinationPath);

        // Assert - 驗證時間戳功能
        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        firstResult.BackupPath.Should().NotBe(secondResult.BackupPath);
        firstResult.BackupPath.Should().Be(@"C:\backup\test_20240101_100000.txt");
        secondResult.BackupPath.Should().Be(@"C:\backup\test_20240101_100001.txt");
    }
}
```

## 組裝驗證：composition smoke test

單元測試全程使用替身，最後還是要確認真實實作能組裝起來。範例專案有一個 composition smoke test：

```csharp
public class FileBackupServiceCompositionTests
{
    [Fact]
    public void FileBackupService_以真實相依性組裝_應能成功建立實例()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FileBackupService>();

        var fileSystem = new FileSystemWrapper();
        var dateTimeProvider = new DateTimeProvider();
        var backupRepository = new SqlBackupRepository("dummy connection string");

        // Act - 以真實相依性組裝服務
        var service = new FileBackupService(fileSystem, dateTimeProvider, backupRepository, logger);

        // Assert - 這是 composition smoke test，只確認服務能被組裝出來。
        // 真正的備份行為（BackupFileAsync）需要實際檔案與資料庫，應在整合測試專案中驗證。
        service.Should().NotBeNull();
    }
}
```

它只驗證「用真實相依性可以成功建構服務」。真正執行備份行為的整合測試需要實際檔案系統與資料庫，那是後面整合測試主題的範圍。

## 執行測試

範例專案用 xUnit v3 的 Microsoft Testing Platform（MTP）模式，在 `samples/day07` 目錄內執行：

```bash
dotnet test --solution Day07.DependencyReplacement.sln --report-trx --report-trx-filename day07.trx
```

MTP 模式不使用 VSTest 的 `--logger`、`--collect:"XPlat Code Coverage"` 參數；覆蓋率改用 `Microsoft.Testing.Extensions.CodeCoverage` 搭配 `--coverage` 系列選項，完整指令見範例專案的 README。

## 常見陷阱與最佳實務

### 1. 避免過度模擬

```csharp
// X 錯誤：模擬值物件
var badDate = Substitute.For<DateTime>(); // DateTime 是值類型

// O 正確：模擬抽象概念
var dateProvider = Substitute.For<IDateTimeProvider>();
```

### 2. 避免測試與實作耦合

```csharp
// X 錯誤：測試實作細節
[Fact]
public void ProcessOrders_處理訂單_應呼叫Repository三次()
{
    _service.ProcessOrders(orders);
    _repository.Received(3).Save(Arg.Any<Order>());
}

// O 正確：測試行為結果
[Fact]
public void ProcessOrders_處理訂單_應儲存所有有效訂單()
{
    var actual = _service.ProcessOrders(orders);
    Assert.Equal(2, actual.SavedCount);
}
```

### 3. 選擇適當的驗證策略

```csharp
// 狀態驗證 vs 行為驗證
[Fact]
public void ProcessOrder_處理訂單_應設定訂單狀態為已處理()
{
    // 狀態驗證：檢查結果
    var actual = _service.ProcessOrder(order);
    Assert.Equal(OrderStatus.Processed, actual.Status);
    
    // 行為驗證：檢查互動（僅在必要時使用）
    _emailService.Received(1).SendConfirmation(order.CustomerEmail);
}
```

### 4. 管理複雜的設定

當測試案例變多時，重複的 Substitute 設定會讓測試程式碼變得冗長且難以維護。這時候我們可以建立基底測試類別來管理共用的設定：

```csharp
public class OrderServiceTestsBase
{
    protected readonly IOrderRepository Repository;
    protected readonly IEmailService EmailService;
    protected readonly ILogger<OrderService> Logger;
    protected readonly OrderService Sut;
    
    protected OrderServiceTestsBase()
    {
        Repository = Substitute.For<IOrderRepository>();
        EmailService = Substitute.For<IEmailService>();
        Logger = Substitute.For<ILogger<OrderService>>();
        Sut = new OrderService(Repository, EmailService, Logger);
    }
    
    // 有效訂單設定
    protected void SetupValidOrder()
    {
        Repository.GetById(Arg.Any<int>()).Returns(new Order { Id = 1, Status = OrderStatus.Pending });
    }
    
    // Email 服務成功設定
    protected void SetupEmailServiceSuccess()
    {
        EmailService.SendConfirmation(Arg.Any<string>()).Returns(true);
    }
}

// 繼承基底類別，避免重複設定
public class OrderServiceTests : OrderServiceTestsBase
{
    [Fact]
    public void ProcessOrder_有效訂單_應回傳成功結果()
    {
        // Arrange
        SetupValidOrder();
        SetupEmailServiceSuccess();
        
        // Act
        var actual = Sut.ProcessOrder(1);
        
        // Assert
        actual.Success.Should().BeTrue();
    }
}
```

這一節的 `OrderService` 是獨立示意（三個相依、`ProcessOrder(int)` 有回傳值），簽章與前文兩處示意不同，重點在基底類別的共用設定模式，不在服務本身。

**使用基底類別的優點：**

- **減少重複程式碼**：共用的 Substitute 設定只需要寫一次
- **提高維護性**：修改相依設定時只需要在一個地方改
- **增加可讀性**：測試方法專注於特定情境的設定
- **便於重構**：當 SUT 的相依改變時，只需要修改基底類別

**注意事項：**

- 避免在基底類別中設定過於具體的行為，保持彈性
- 提供輔助方法來設定常用的情境
- 確保每個測試仍然保持獨立性

## Mock vs Stub 的實戰差異

Mock 和 Stub 很容易混在一起，直接看一組例子：

### Stub：驗證狀態

```csharp
[Fact]
public void CalculateDiscount_高級會員_應回傳20%折扣()
{
    // Stub：只關心回傳值，用於設定測試情境
    var stubCustomerService = Substitute.For<ICustomerService>();
    stubCustomerService.GetCustomerType(123).Returns(CustomerType.Premium);
    
    var service = new PricingService(stubCustomerService);
    var discount = service.CalculateDiscount(123, 1000);
    
    // 只驗證結果狀態
    Assert.Equal(200, discount); // 20% of 1000
}
```

### Mock：驗證行為

驗證 `相依物件` 的行為互動

```csharp
[Fact]
public void ProcessPayment_成功付款_應記錄交易資訊()
{
    // Mock：關心 ILogger<PaymentService> 是否正確互動
    var mockLogger = Substitute.For<ILogger<PaymentService>>();
    var stubPaymentGateway = Substitute.For<IPaymentGateway>();
    stubPaymentGateway.ProcessPayment(Arg.Any<decimal>()).Returns(PaymentResult.Success);
    
    var service = new PaymentService(stubPaymentGateway, mockLogger);
    service.ProcessPayment(100);
    
    // 驗證正確的互動行為
    mockLogger.Received(1).LogInformation(
        "Payment processed: {Amount} - Result: {Result}", 
        100, 
        PaymentResult.Success);
}
```

> 同樣提醒：`LogInformation` 是擴充方法，這裡是為了聚焦 Mock 意圖的簡化寫法，實際驗證 `ILogger` 要改攔底層 `Log` 方法（見前面「ILogger 特殊處理」一節）。

## 設計品質指標

### 識別需要替代的相依性

**應該替代的：**

- 外部 API 呼叫
- 資料庫操作
- 檔案系統操作
- 網路通訊
- 時間相依
- 隨機數產生
- 昂貴的計算

**不應該替代的：**

- 值物件（DateTime、string、int）
- 簡單的資料傳輸物件（DTO）
- 純函式工具（如 AutoMapper 的 IMapper）

### 改善可測試性的設計原則

1. **相依反轉原則**：相依於抽象而非具體實作
2. **單一職責原則**：每個類別只有一個變更理由
3. **介面隔離原則**：介面應該小而專注
4. **避免靜態相依**：使用可注入的服務替代靜態呼叫

## 實務建議與進階技巧

### 1. 如何判斷何時使用 Mock vs Stub

**使用 Stub 的時機：**

- 需要設定測試情境的回傳值
- 著重業務邏輯的結果狀態
- 模擬外部服務的回應

**使用 Mock 的時機：**

- 需要驗證方法是否被正確呼叫
- 著重物件之間的互動行為
- 驗證記錄、通知等副作用

### 2. 常見的設計怪味道與解決方案

#### 怪味道：測試設定過於複雜

```csharp
// 壞味道：設定過多的 Substitute
var sub1 = Substitute.For<IService1>();
var sub2 = Substitute.For<IService2>();
var sub3 = Substitute.For<IService3>();
// ... 更多設定

// 解決：重新思考類別職責，可能違反了 SRP
```

#### 怪味道：測試與實作強耦合

```csharp
// 壞味道：測試實作細節
_repository.Received(1).Save(Arg.Any<User>());
_repository.Received(1).Update(Arg.Any<User>());
_repository.Received(1).Delete(Arg.Any<int>());

// 解決：關注行為結果而非實作步驟
Assert.Equal(expectedUsers, actualUsers);
```

### 3. 效能考量

- **避免過度使用 Mock**：每個 Substitute 都有建立成本
- **重用測試設定**：使用基底類別或 Factory 模式
- **簡化測試情境**：專注於核心測試目標

### 4. 團隊協作建議

- **統一命名慣例**：團隊一致的測試命名與結構
- **分享測試工具類別**：共用的 TestDouble 建立邏輯
- **程式碼審查重點**：確保測試的意圖清晰明確

## 本日小結

單元測試少不了測試替身，NSubstitute 的語法讓這件事寫起來很輕。今天的重點：

1. **理解替身類型**：根據需求選擇適當的 Test Double
2. **掌握 NSubstitute 語法**：熟練使用 Returns、Received 等核心功能
3. **區分 Mock 與 Stub**：狀態驗證 vs 行為驗證
4. **建立良好設計**：用相依性注入提升可測試性
5. **避免常見陷阱**：不過度模擬、不與實作耦合

## 明日預告

明天會延續今天的 ILogger 模擬練習，處理以下內容：

- **xUnit ITestOutputHelper**：如何在測試中輸出診斷資訊
- **ILogger 整合測試**：驗證記錄行為與內容
- **測試診斷技巧**：除錯複雜測試案例的實用方法

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day07>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第七天。明天會介紹 Day 08：測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger。**
