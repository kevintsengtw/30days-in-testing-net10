---
day: 23
title: "Day 23 - 整合測試實戰：WebApi 服務的整合測試"
sample: samples/day23
target_framework: net10.0
packages:
  - AwesomeAssertions
  - AwesomeAssertions.Web
  - Dapper
  - Microsoft.AspNet.WebApi.Client
  - Flurl.Http
  - FluentValidation
  - FluentValidation.DependencyInjectionExtensions
  - Microsoft.AspNetCore.Mvc.Testing
  - Microsoft.AspNetCore.OpenApi
  - Microsoft.OpenApi
  - Microsoft.Bcl.TimeProvider
  - Microsoft.Extensions.Configuration.Abstractions
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.TrxReport
  - Npgsql
  - Respawn
  - StackExchange.Redis
  - SSH.NET
  - Testcontainers
  - Testcontainers.PostgreSql
  - Testcontainers.Redis
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 23 - 整合測試實戰：WebApi 服務的整合測試

<!-- toc -->

- [前言](#前言)
- [本篇學習內容](#本篇學習內容)
- [範例專案架構](#範例專案架構)
- [核心實體與服務介面](#核心實體與服務介面)
- [ExceptionHandler 整合測試 - 現代錯誤處理機制](#exceptionhandler-整合測試---現代錯誤處理機制)
- [整合測試基礎設施](#整合測試基礎設施)
- [整合測試核心要素](#整合測試核心要素)
- [整合測試的關鍵實戰技巧](#整合測試的關鍵實戰技巧)
- [整合測試最佳實務總結](#整合測試最佳實務總結)
- [實務開發建議](#實務開發建議)
- [TestWebApplicationFactory 實作詳解](#testwebapplicationfactory-實作詳解)
- [Flurl 整合應用](#flurl-整合應用)
- [ExceptionHandler 整合測試策略](#exceptionhandler-整合測試策略)
- [實際整合測試案例分析](#實際整合測試案例分析)
- [整合測試實務經驗總結](#整合測試實務經驗總結)
- [整合測試最佳實務建議](#整合測試最佳實務建議)
- [今日總結](#今日總結)
- [在本機執行測試（MTP）](#在本機執行測試mtp)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

前 22 天已經處理單元測試、xUnit、測試替身與 Mock。今天把這些零件接起來，實作 Web API 整合測試。

整合測試驗證系統元件的協作，包括資料庫、快取與 HTTP pipeline。它比單元測試更接近實際執行路徑，但仍不等同完整的正式環境。

## 本篇學習內容

本篇將示範完整的 WebApi 整合測試實作，內容涵蓋：

- **Clean Architecture 專案的整合測試架構**：從 Domain 到 API 層的完整測試策略
- **Testcontainers 多容器管理**：PostgreSQL + Redis 的完整整合
- **ExceptionHandler 與 ValidationProblemDetails**：ASP.NET Core 10 的現代例外處理模式
- **Flurl 與 AwesomeAssertions**：簡化 HTTP 測試的工具組合

## 範例專案架構

本次範例是一個簡單的產品管理 WebApi 服務，使用 Clean Architecture 設計：

```text
Day23/
├── src/
│   ├── Day23.Api/                          # WebApi 層
│   ├── Day23.Application/                  # 應用服務層  
│   ├── Day23.Domain/                       # 領域模型
│   └── Day23.Infrastructure/               # 基礎設施層
└── tests/
    └── Day23.Tests.Integration/            # 整合測試
```

### 技術堆疊

- **API**: ASP.NET Core 10 WebApi (Controllers)
- **資料庫**: PostgreSQL + Dapper
- **快取**: Redis
- **驗證**: FluentValidation
- **測試**: xUnit v3 (MTP) + Testcontainers + Flurl + AwesomeAssertions

## 核心實體與服務介面

### 領域模型設計

我們的 Product 實體很簡潔，符合整潔架構的設計原則：

```csharp
/// <summary>
/// 產品實體
/// </summary>
public class Product
{
    /// <summary>
    /// 產品唯一識別碼
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 產品名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 產品價格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### 產品服務介面

應用層定義的服務介面設計得很實用：

```csharp
/// <summary>
/// 產品服務介面
/// </summary>
public interface IProductService
{
    /// <summary>
    /// 建立產品
    /// </summary>
    Task<ProductResponse> CreateAsync(ProductCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 ID 取得產品；找不到時擲出 <see cref="KeyNotFoundException"/>，
    /// 統一交由 GlobalExceptionHandler 對應為 404（回傳型別為 non-nullable）。
    /// </summary>
    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢產品列表
    /// </summary>
    Task<PagedResult<ProductResponse>> QueryAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        string sort = "createdAt",
        string direction = "desc",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新產品
    /// </summary>
    Task UpdateAsync(Guid id, ProductUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除產品
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

## ExceptionHandler 整合測試 - 現代錯誤處理機制

### 為什麼選擇 IExceptionHandler

ASP.NET Core 8 引入的 `IExceptionHandler` 介面提供了比傳統 middleware 更優雅的錯誤處理方式：

**統一的錯誤處理介面**：所有例外處理器都實作同一個介面，提供一致的處理模式。

**更好的可測試性**：每個處理器都是獨立的服務，可以單獨進行單元測試。

**型別安全**：透過強型別的介面，避免了 middleware 中的型別轉換問題。

**標準化回應格式**：內建支援 ProblemDetails 標準，確保 API 錯誤回應的一致性。

### 與傳統 ExceptionHandlingMiddleware 的差異

傳統的例外處理 middleware 往往在同一個方法處理所有例外，分支一多就難以維護。`IExceptionHandler` 可以依例外類型拆成不同處理器：

```csharp
// 傳統 middleware 方式 - 所有邏輯集中在一處
public async Task InvokeAsync(HttpContext context, RequestDelegate next)
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        // 處理驗證異常
    }
    catch (KeyNotFoundException ex)
    {
        // 處理找不到資源異常
    }
    // ... 更多異常處理
}

// IExceptionHandler 方式 - 職責分離，更清晰
public class FluentValidationExceptionHandler : IExceptionHandler { }
public class GlobalExceptionHandler : IExceptionHandler { }
```

### 全域例外處理器實作

我們的 `GlobalExceptionHandler` 負責處理所有未被特定處理器處理的例外：

```csharp
/// <summary>
/// 全域異常處理器
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "發生未處理的異常: {Message}", exception.Message);

        var problemDetails = CreateProblemDetails(exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(Exception exception)
    {
        return exception switch
        {
            ArgumentException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "參數錯誤",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = exception.Message
            },
            KeyNotFoundException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/404",
                Title = "資源不存在",
                Status = (int)HttpStatusCode.NotFound,
                Detail = exception.Message
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/401",
                Title = "未授權",
                Status = (int)HttpStatusCode.Unauthorized,
                Detail = "您沒有權限執行此操作"
            },
            TimeoutException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/408",
                Title = "請求超時",
                Status = (int)HttpStatusCode.RequestTimeout,
                Detail = "操作執行超時，請稍後再試"
            },
            InvalidOperationException => new ProblemDetails
            {
                Type = "https://httpstatuses.com/422",
                Title = "操作無效",
                Status = (int)HttpStatusCode.UnprocessableEntity,
                Detail = exception.Message
            },
            _ => new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "內部伺服器錯誤",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "發生未預期的錯誤，請聯絡系統管理員"
            }
        };
    }
}
```

### ProblemDetails 標準格式

在實作例外處理器之前，需要了解 [ProblemDetails](https://learn.microsoft.com/zh-tw/dotnet/api/microsoft.aspnetcore.mvc.problemdetails?view=aspnetcore-10.0) 類別。Problem Details 最初由 [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) 定義，現行規範為 [RFC 9457](https://datatracker.ietf.org/doc/html/rfc9457)（已取代 RFC 7807）。這個類別提供了統一的錯誤回應格式：

- **Type**：問題類型的 URI，用於識別錯誤類別
- **Title**：簡短的錯誤描述，通常是人類可讀的摘要
- **Status**：HTTP 狀態碼
- **Detail**：詳細的錯誤說明
- **Instance**：發生問題的特定執行個體 URI

格式統一之後，呼叫 API 的那一方可以用同一套邏輯處理所有錯誤，不必為每個端點寫特例。

### FluentValidationExceptionHandler - FluentValidation 專用例外處理器

以下處理器專門接住 FluentValidation 的驗證錯誤：

```csharp
/// <summary>
/// FluentValidation 專用異常處理器
/// </summary>
public class FluentValidationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<FluentValidationExceptionHandler> _logger;

    public FluentValidationExceptionHandler(ILogger<FluentValidationExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        _logger.LogWarning(validationException, "驗證失敗: {Message}", validationException.Message);

        var problemDetails = new ValidationProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more validation errors occurred.",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = "輸入的資料包含驗證錯誤，請檢查後重新提交。",
            Instance = httpContext.Request.Path
        };

        foreach (var error in validationException.Errors)
        {
            var propertyName = error.PropertyName;
            var errorMessage = error.ErrorMessage;

            if (problemDetails.Errors.ContainsKey(propertyName))
            {
                var existingErrors = problemDetails.Errors[propertyName].ToList();
                existingErrors.Add(errorMessage);
                problemDetails.Errors[propertyName] = existingErrors.ToArray();
            }
            else
            {
                problemDetails.Errors.Add(propertyName, new[] { errorMessage });
            }
        }

        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
```

### 註冊設定與順序重要性

在 `Program.cs` 中的正確設定方式，**註冊順序決定了處理的優先順序**：

```csharp
// 註冊 FluentValidation 的 validator（核心套件 + DI 擴充）
// 注意：這裡「不」使用已停止維護的 FluentValidation.AspNetCore auto-validation，
// 而是把 validator 註冊進 DI，交由 service 層明確呼叫。
builder.Services.AddValidatorsFromAssemblyContaining<ProductCreateValidator>();

// 關閉 [ApiController] 的自動 ModelState 400 短路，讓所有輸入驗證都走
// service 層的 FluentValidation → ValidationException → FluentValidationExceptionHandler。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Add ProblemDetails
builder.Services.AddProblemDetails();

// Add Exception Handler - 順序很重要！
builder.Services.AddExceptionHandler<FluentValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
```

**為什麼 FluentValidationExceptionHandler 必須先註冊？**

例外處理器會按照註冊順序依次嘗試處理例外。`FluentValidationExceptionHandler` 只處理 `ValidationException`，如果無法處理就回傳 `false`，讓下一個處理器接手。`GlobalExceptionHandler` 會處理所有例外並回傳 `true`，因此必須放在最後作為後備處理器。

middleware pipeline 中的正確位置：

```csharp
// Use Exception Handler
app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
```

### 例外從哪裡來：讓測試真的穿越 IExceptionHandler

`IExceptionHandler` 只有在「例外真的離開 controller、進入 `UseExceptionHandler` middleware」時才會被呼叫。如果 controller 自己 `catch` 例外或直接 `return NotFound(...)`，錯誤回應就是 controller 產生的，與 handler 無關。即使測試通過，也沒有驗證到 handler。

因此本專案刻意把「例外的來源」設計清楚，讓每一種目標例外都能穿越 handler：

- **驗證錯誤**：service 層以 `ValidateAndThrowAsync` 明確驗證，失敗時擲出 `ValidationException`，由 `FluentValidationExceptionHandler` 處理。

    ```csharp
    public async Task<ProductResponse> CreateAsync(
        ProductCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // 明確、可非同步、可取消的驗證；例外會離開 service 進入 handler
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        // ...建立產品
    }
    ```

- **資源不存在**：service 統一擲出 `KeyNotFoundException`（controller 不再自行 `catch` 或回傳 `NotFound`），由 `GlobalExceptionHandler` 對應為 404。
- **參數錯誤**：查詢的排序欄位非法時，service 擲出 `ArgumentException`，由 `GlobalExceptionHandler` 對應為 400。

controller 因此變得很薄，只負責轉呼叫 service、不攔截這些目標例外：

```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
{
    // 找不到時 service 擲出 KeyNotFoundException → GlobalExceptionHandler → 404
    var result = await _productService.GetByIdAsync(id, cancellationToken);
    return Ok(result);
}
```

這樣一來，測試對 handler 專屬回應（例如 `GlobalExceptionHandler` 的 Title「資源不存在」）的斷言，就真的證明了 handler 有被執行；只要移除或破壞 handler 註冊，這些測試就會失敗。

> **為什麼不用 `FluentValidation.AspNetCore` 的 auto-validation？**
> 官方已將 `FluentValidation.AspNetCore` 標示為停止維護，且不建議新專案採用 auto-validation。更重要的是，auto-validation 會把錯誤填進 `ModelState`，由 `[ApiController]` 直接產生 400，**不會**擲出 `ValidationException` 給 `FluentValidationExceptionHandler`。這會讓 handler 變成死碼。改用核心 `FluentValidation` + 手動 `ValidateAndThrowAsync`，驗證錯誤才會真的走到 handler。

### ValidationProblemDetails 的結構化優勢

`ValidationProblemDetails` 是 ASP.NET Core 專門為驗證錯誤設計的標準化回應格式：

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "輸入的資料包含驗證錯誤，請檢查後重新提交。",
  "instance": "/products",
  "errors": {
    "Name": ["產品名稱不能為空"],
    "Price": ["產品價格必須大於 0"]
  }
}
```

這種格式的優勢：

- **結構化錯誤**：每個欄位的錯誤獨立列出，方便前端針對特定欄位顯示錯誤
- **標準化**：遵循 Problem Details 標準（現行規範 RFC 9457，取代早期的 RFC 7807），確保 API 的一致性
- **易於解析**：前端可以輕易處理並顯示錯誤，提升使用者體驗
- **支援多重錯誤**：同一個欄位可以有多個驗證錯誤，全部都會回傳

## 整合測試基礎設施

### Collection Fixture 模式

延續 Day21、Day22 的經驗，我們使用 Collection Fixture 來共享昂貴的容器資源：

```csharp
/// <summary>
/// 整合測試集合定義
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    /// <summary>
    /// 集合名稱常數
    /// </summary>
    public const string Name = "Integration Tests";

    // 這個類別不需要任何實作
    // 它只是用來定義 Collection Fixture
}
```

### 測試基底類別

設計一個實用的基底類別，提供所有整合測試需要的功能：

```csharp
/// <summary>
/// 整合測試基底類別 - 使用 Collection Fixture 共享容器
/// </summary>
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // 設定 Flurl 使用者端
        FlurlClient = new FlurlClient(HttpClient);
    }

    public virtual async ValueTask InitializeAsync()
    {
        // 初始化資料庫結構
        await DatabaseManager.InitializeDatabaseAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        // 清理資料庫資料
        await DatabaseManager.CleanDatabaseAsync();

        FlurlClient.Dispose();
    }

}
```

### xunit v3 與 MTP 對整合測試的影響

本系列樣本已從 xUnit v2 遷到 v3（走 Microsoft.Testing.Platform）。整合測試專案的 `.csproj` 拿掉了 `xunit`、`xunit.runner.visualstudio`、`Microsoft.NET.Test.Sdk`、`coverlet.collector`，改由 `xunit.v3.mtp-v2` 一次涵蓋，並加 `Microsoft.Testing.Extensions.TrxReport` 產生 TRX；測試專案本身是可執行檔，要加 `<OutputType>Exe</OutputType>`，版本統一集中在 per-day 的 `Directory.Packages.props`（CPM）。

`IAsyncLifetime` 的回傳型別在 v3 改了：`InitializeAsync`／`DisposeAsync` 從 `Task` 改成 `ValueTask`（`DisposeAsync` 現在來自 `IAsyncDisposable`），方法體不用動。上面的 `IntegrationTestBase` 已是 v3 寫法。要特別留意 `TestWebApplicationFactory`：它繼承 `WebApplicationFactory<Program>`（本身就有 `virtual ValueTask DisposeAsync()`），v3 下 `IAsyncLifetime.DisposeAsync` 與它同簽章，覆寫要用 `public override async ValueTask DisposeAsync()`（v2 時代那個 `new async Task DisposeAsync()` 的寫法要改）。`ICollectionFixture`／`[CollectionDefinition]`／`[Collection]` 在 v3 同構，維持原樣。

#### xUnit1051：真實呼叫要傳 CancellationToken

xUnit v3 內建分析規則 xUnit1051：測試方法裡呼叫「有 `CancellationToken` 多載」的非同步方法卻沒傳 token，就會跳警告。整合測試幾乎全是真實呼叫——`HttpClient.GetAsync`／`PostAsJsonAsync`／`PutAsJsonAsync`／`DeleteAsync`、`response.Content.ReadAsStringAsync()` 等都算，一律補上 `TestContext.Current.CancellationToken`（這些多載的 token 都是最後一個參數，用位置參數即可）。至於 `TestHelpers` 與 `DatabaseManager` 的對外方法，目前並未提供 `CancellationToken` 參數，因此測試方法呼叫它們時不會觸發 xUnit1051；其內部是否要繼續往下傳遞 token，則是另一項可取消性設計議題。要釐清的是：xUnit1051 只針對測試方法中「可傳入 `CancellationToken` 卻未傳入」的直接呼叫提出警告；analyzer 沒有標記 helper 內部呼叫，並不代表那些底層 API（如 `OpenAsync`、`ExecuteNonQueryAsync`）沒有 `CancellationToken` 多載。

**一個要當心的副作用：CS0121 撞名**。本專案測試同時用到 `System.Net.Http.Json` 的 `PostAsJsonAsync` 與 `AwesomeAssertions.Web`，而 `AwesomeAssertions.Web` 會由 `Microsoft.AspNet.WebApi.Client`（`System.Net.Http.Formatting`）帶進另一組同名的 `PostAsJsonAsync`／`PutAsJsonAsync` 擴充。v2 時是兩參數呼叫，只有 BCL 版符合；一旦依 xUnit1051 補上 `CancellationToken` 變成三參數，BCL 與舊版帶 token 的多載完全同形，編譯器無從選擇，就會回報 CS0121。解法是在測試專案顯式引用 `Microsoft.AspNet.WebApi.Client`，並加上 `ExcludeAssets="compile"`。編譯期隱藏舊版擴充，讓 BCL 多載成為唯一候選；runtime 資產仍保留，不影響執行。

### SQL 指令碼外部化

跟隨 Day21 的最佳實務，DatabaseManager 將 SQL 指令碼外部化管理：

```csharp
/// <summary>
/// 確保資料表存在，使用外部 SQL 指令碼建立
/// 實作第 Day 21 介紹的 SQL 指令碼外部化策略
/// </summary>
private async Task EnsureTablesExistAsync(NpgsqlConnection connection)
{
    var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
    if (!Directory.Exists(scriptDirectory))
    {
        throw new DirectoryNotFoundException($"SQL 指令碼目錄不存在: {scriptDirectory}");
    }

    // 按照相依順序執行表格建立腳本
    var orderedScripts = new[]
    {
        "Tables/CreateProductsTable.sql"
    };

    foreach (var scriptPath in orderedScripts)
    {
        var fullPath = Path.Combine(scriptDirectory, scriptPath);
        if (File.Exists(fullPath))
        {
            var script = await File.ReadAllTextAsync(fullPath);
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            throw new FileNotFoundException($"SQL 指令碼檔案不存在: {fullPath}");
        }
    }
}
```

對應的 SQL 檔案會建立資料表與索引。特別注意：`created_at`／`updated_at` 一律由 application 的 `TimeProvider` 提供，**刻意不設 `DEFAULT NOW()`、也不建立「更新時間」觸發器**：

```sql
-- Products 資料表
-- 注意：created_at / updated_at 由 production code（透過注入的 TimeProvider）決定，
-- 刻意不使用 DB 觸發器覆寫 updated_at，才能讓 FakeTimeProvider 在整合測試中完整控制時間戳記。
CREATE TABLE IF NOT EXISTS products
(
    id         UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    name       VARCHAR(200)   NOT NULL,
    price      DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMPTZ    NOT NULL,
    updated_at TIMESTAMPTZ    NOT NULL
);

-- 建立索引以提升查詢效能
CREATE INDEX IF NOT EXISTS idx_products_name ON products (name);
CREATE INDEX IF NOT EXISTS idx_products_price ON products (price);
CREATE INDEX IF NOT EXISTS idx_products_created_at ON products (created_at);
CREATE INDEX IF NOT EXISTS idx_products_updated_at ON products (updated_at);
```

> **為什麼不放 `BEFORE UPDATE` 觸發器？** 常見寫法會用觸發器在每次 UPDATE 時把 `updated_at` 設成 `NOW()`（DB 的實際時間），但這會蓋掉 service 端由 `TimeProvider` 寫入的值。測試以 `FakeTimeProvider` 推進時間並斷言 `updated_at` 等於受控時刻時，觸發器會把它改回真實時間，讓時間控制失效。因此這裡讓 application 完全掌握時間戳記，`updated_at` 一律由 `ProductService` 以 `TimeProvider.GetUtcNow()` 提供。

### 服務層架構重點分析

在 Clean Architecture 中，Application 層的服務是整合測試的核心焦點。我們的 `IProductService` 定義了標準的 CRUD 操作，但整合測試的價值在於驗證這些操作在真實環境下的運作方式。

整合測試會著重以下項目：

1. **跨層級協作**：Controller → Service → Repository → Database 的完整資料流
2. **快取整合**：Cache-Aside 模式下，資料庫與 Redis 的一致性維護
3. **例外處理**：各層級的例外如何正確傳遞和處理
4. **效能表現**：真實資料庫和快取的回應時間

## 整合測試核心要素

### 測試環境設定

在整合測試中，我們需要確保測試環境能夠模擬真實的執行環境。專案使用 Testcontainers 來提供獨立的 PostgreSQL 和 Redis 執行個體：

```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer PostgreSqlContainer;
    protected readonly RedisContainer RedisContainer;
    protected HttpClient HttpClient = null!;
    protected WebApplicationFactory<Program> Factory = null!;

    public IntegrationTestBase()
    {
        PostgreSqlContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("test")
            .WithPassword("test123")
            .Build();

        RedisContainer = new RedisBuilder("redis:7-alpine")
            .Build();
    }
}
```

#### 建立產品測試 - 驗證完整業務流程

```csharp
[Fact]
public async Task CreateProduct_使用有效資料_應成功建立產品()
{
    // Arrange
    var request = TestHelpers.CreateProductRequest("新產品", 299.99m);

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", request, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be201Created()
            .And.Satisfy<ProductResponse>(product =>
            {
                product.Id.Should().NotBeEmpty();
                product.Name.Should().Be("新產品");
                product.Price.Should().Be(299.99m);
                product.CreatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
                product.UpdatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
            });
}
```

這個測試展現了整合測試的幾個重要特點：

1. **端對端流程**：從 HTTP 請求到回應的完整路徑
2. **業務邏輯驗證**：確認產品建立的核心邏輯

#### 驗證失敗測試 - ValidationProblemDetails 整合

```csharp
[Fact]
public async Task CreateProduct_當產品名稱為空_應回傳400BadRequest()
{
    // Arrange
    var invalidRequest = new ProductCreateRequest
    {
        Name = "",
        Price = 100.00m
    };

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                problem.Title.Should().Be("One or more validation errors occurred.");
                problem.Status.Should().Be(400);
                problem.Errors.Should().ContainKey("Name");
                problem.Errors["Name"].Should().Contain("產品名稱不能為空");
            });
}
```

#### 分頁查詢測試 - 使用 Flurl 建構複雜 URL

```csharp
[Fact]
    public async Task GetProducts_使用分頁參數_應回傳正確的分頁結果()
    {
        // Arrange
        await TestHelpers.SeedProductsAsync(DatabaseManager, 15);

        // Act - 使用 Flurl 建構 QueryString
        var url = "/products"
                  .SetQueryParam("pageSize", 5)
                  .SetQueryParam("page", 2);

        var response = await HttpClient.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(15);
                    result.PageSize.Should().Be(5);
                    result.Page.Should().Be(2);
                    result.PageCount.Should().Be(3);
                    result.Items.Should().HaveCount(5);
                    result.Items.Should().AllSatisfy(product =>
                    {
                        product.Id.Should().NotBeEmpty();
                        product.Name.Should().NotBeNullOrEmpty();
                        product.Price.Should().BeGreaterThan(0);
                    });
                });
    }
```

### 測試基礎設施 - 共享容器與資料管理

整合測試好不好用，多半取決於測試基礎設施夠不夠穩、夠不夠快。專案用 Collection Fixture 模式共享 Testcontainers：

```csharp
/// <summary>
/// 整合測試基底類別 - 使用 Collection Fixture 共享容器
/// </summary>
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // 設定 Flurl 使用者端
        FlurlClient = new FlurlClient(HttpClient);
    }

    public virtual async ValueTask InitializeAsync()
    {
        // 初始化資料庫結構
        await DatabaseManager.InitializeDatabaseAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        // 清理資料庫資料
        await DatabaseManager.CleanDatabaseAsync();
        FlurlClient.Dispose();
    }
}
```

## 整合測試的關鍵實戰技巧

### 1. 使用 AwesomeAssertions 進行精確驗證

整合測試可以用 `AwesomeAssertions` 直接驗證 HTTP 回應：

```csharp
// 驗證成功回應和資料結構
response.Should().Be200Ok()
        .And.Satisfy<PagedResult<ProductResponse>>(result =>
        {
            result.Total.Should().Be(15);
            result.Items.Should().HaveCount(5);
            result.Items.Should().AllSatisfy(product =>
            {
                product.Id.Should().NotBeEmpty();
                product.Name.Should().NotBeNullOrEmpty();
                product.Price.Should().BeGreaterThan(0);
            });
        });

// 驗證錯誤回應的詳細結構
response.Should().Be400BadRequest()
        .And.Satisfy<ValidationProblemDetails>(problem =>
        {
            problem.Status.Should().Be(400);
            problem.Errors.Should().ContainKey("Name");
            problem.Errors["Name"].Should().Contain("產品名稱不能為空");
        });
```

### 2. 測試資料管理策略

整合測試需要有效的測試資料管理。專案使用 `TestHelpers` 提供一致的測試資料：

```csharp
public static class TestHelpers
{
    public static ProductCreateRequest CreateProductRequest(
        string name = "測試產品",
        decimal price = 100.00m)
    {
        return new ProductCreateRequest
        {
            Name = name,
            Price = price
        };
    }

    public static async Task SeedProductsAsync(DatabaseManager dbManager, int count)
    {
        var tasks = new List<Task>();
        for (var i = 1; i <= count; i++)
        {
            tasks.Add(SeedSpecificProductAsync(dbManager, $"產品 {i:D2}", i * 10.0m));
        }

        await Task.WhenAll(tasks);
    }
}
```

### 3. 容器生命週期管理

使用 Collection Fixture 模式有效管理 Testcontainers 的生命週期：

```csharp
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    public const string Name = "Integration Tests";
}
```

這種設計確保：

- **容器重用**：所有測試共享同一組容器
- **成本控制**：避免為每個測試類別建立新容器
- **測試隔離**：透過資料清理確保測試間的獨立性

```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // 設定 Flurl 使用者端
        FlurlClient = new FlurlClient(HttpClient);
    }
}
```

## 整合測試最佳實務總結

### 1. 測試結構設計原則

- **單一職責**：每個測試專注於一個特定的業務情境
- **3A 模式**：清楚區分 Arrange、Act、Assert 三個階段
- **可讀性優先**：測試方法名稱要能清楚表達測試意圖

### 2. 資料管理策略

```csharp
// 每個測試前初始化資料庫結構
public async ValueTask InitializeAsync()
{
    await DatabaseManager.InitializeDatabaseAsync();
}

// 使用 TestHelpers 建立一致的測試資料
var request = TestHelpers.CreateProductRequest("測試產品", 199.99m);
```

### 3. 錯誤處理驗證重點

整合測試必須驗證應用程式的錯誤處理機制：

- **ValidationProblemDetails**：模型驗證失敗的回應格式
- **ExceptionHandler**：全域例外處理的行為
- **HTTP 狀態碼**：正確的狀態碼回傳

### 4. 效能考量

- **容器共享**：使用 Collection Fixture 避免重複建立容器
- **資料清理**：每次測試後只清理資料，不重建容器
- **並行執行**：確保測試間的獨立性，支援並行執行

## 實務開發建議

### 測試涵蓋重點

1. **API 端點**：所有 HTTP 方法和路由
2. **資料驗證**：模型驗證和業務規則
3. **錯誤情境**：各種例外狀況的處理
4. **整合流程**：跨層級的資料流

### 技術債務避免

1. **避免測試相依性**：每個測試都應該能獨立執行
2. **避免硬編碼**：使用設定或常數管理測試資料
3. **避免過度測試**：專注於業務邏輯，不要測試框架本身

這樣既能在接近真實的環境驗證行為，測試本身也還維持得動、跑得快。

## TestWebApplicationFactory 實作詳解

專案的 `TestWebApplicationFactory` 負責管理測試環境的容器和服務設定：

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private RedisContainer? _redisContainer;
    private FakeTimeProvider? _timeProvider;

    public PostgreSqlContainer PostgresContainer => _postgresContainer
                                                    ?? throw new InvalidOperationException("PostgreSQL container 尚未初始化");

    public RedisContainer RedisContainer => _redisContainer
                                            ?? throw new InvalidOperationException("Redis container 尚未初始化");

    public FakeTimeProvider TimeProvider => _timeProvider
                                            ?? throw new InvalidOperationException("TimeProvider 尚未初始化");

    public async ValueTask InitializeAsync()
    {
        // 建立 PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
                             .WithDatabase("day23_test")
                             .WithUsername("testuser")
                             .WithPassword("testpass")
                             .WithCleanUp(true)
                             .Build();

        // 建立 Redis container
        _redisContainer = new RedisBuilder("redis:7-alpine")
                          .WithCleanUp(true)
                          .Build();

        // 建立 FakeTimeProvider
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // 啟動容器
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            // 移除現有的設定來源
            config.Sources.Clear();

            // 添加測試專用設定
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = PostgresContainer.GetConnectionString(),
                ["ConnectionStrings:Redis"] = RedisContainer.GetConnectionString(),
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:System"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            // 替換 TimeProvider 為 FakeTimeProvider
            services.Remove(services.Single(d => d.ServiceType == typeof(TimeProvider)));
            services.AddSingleton<TimeProvider>(TimeProvider);
        });

        builder.UseEnvironment("Testing");
    }

    public override async ValueTask DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
```

這個 `TestWebApplicationFactory` 的設計重點：

1. **容器管理**：使用屬性公開容器，提供更好的類型安全
2. **FakeTimeProvider 整合**：讓時間相關的測試變得可預測
3. **設定覆寫**：在 `ConfigureWebHost` 中完全控制測試環境的設定
4. **環境隔離**：每次測試執行都使用獨立的容器執行個體

### 進階測試基底類別

增強的測試基底類別提供更多功能：

```csharp
/// <summary>
/// 整合測試基底類別 - 使用 Collection Fixture 共享容器
/// </summary>
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // 設定 Flurl 使用者端
        FlurlClient = new FlurlClient(HttpClient);
    }

    public virtual async ValueTask InitializeAsync()
    {
        // 初始化資料庫結構
        await DatabaseManager.InitializeDatabaseAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        // 清理資料庫資料
        await DatabaseManager.CleanDatabaseAsync();

        FlurlClient.Dispose();
    }

}
```

### SQL 指令碼外部化策略

跟隨 Day21 建立的最佳實務，將 SQL 指令碼外部化：

```text
tests/Day23.Tests.Integration/
└── SqlScripts/
    └── Tables/
        └── CreateProductsTable.sql
```

```sql
-- CreateProductsTable.sql
-- 注意：created_at / updated_at 由 production code（透過注入的 TimeProvider）決定，
-- 刻意不使用 DEFAULT NOW()、也不建立「更新時間」觸發器，才能讓 FakeTimeProvider 在整合測試中完整控制時間戳記。
CREATE TABLE IF NOT EXISTS products
(
    id         UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    name       VARCHAR(200)   NOT NULL,
    price      DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMPTZ    NOT NULL,
    updated_at TIMESTAMPTZ    NOT NULL
);

-- 建立索引以提升查詢效能
CREATE INDEX IF NOT EXISTS idx_products_name ON products (name);
CREATE INDEX IF NOT EXISTS idx_products_price ON products (price);
CREATE INDEX IF NOT EXISTS idx_products_created_at ON products (created_at);
CREATE INDEX IF NOT EXISTS idx_products_updated_at ON products (updated_at);
```

> 如果在這裡加上 `BEFORE UPDATE` 觸發器，把 `updated_at` 設成 `NOW()`，就會蓋掉 service 端由 `TimeProvider` 寫入的值，導致以 `FakeTimeProvider` 推進時間的 `updated_at` 斷言失效。這也是本表刻意不放觸發器的原因。

DatabaseManager 會自動載入這些指令碼：

```csharp
/// <summary>
/// 確保資料表存在，使用外部 SQL 指令碼建立
/// 實作第 Day 21 介紹的 SQL 指令碼外部化策略
/// </summary>
private async Task EnsureTablesExistAsync(NpgsqlConnection connection)
{
    var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
    if (!Directory.Exists(scriptDirectory))
    {
        throw new DirectoryNotFoundException($"SQL 指令碼目錄不存在: {scriptDirectory}");
    }

    // 按照相依順序執行表格建立腳本
    var orderedScripts = new[]
    {
        "Tables/CreateProductsTable.sql"
    };

    foreach (var scriptPath in orderedScripts)
    {
        var fullPath = Path.Combine(scriptDirectory, scriptPath);
        if (File.Exists(fullPath))
        {
            var script = await File.ReadAllTextAsync(fullPath);
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            throw new FileNotFoundException($"SQL 指令碼檔案不存在: {fullPath}");
        }
    }
}
```

## Flurl 整合應用

Flurl 提供流暢 API，可在整合測試中組合 URL 與查詢參數，減少手動串接字串的程式碼。

### 簡化 QueryString 建立

傳統的查詢參數建構方式容易出錯，Flurl 提供了型別安全的方式：

```csharp
[Fact]
    public async Task GetProducts_使用分頁參數_應回傳正確的分頁結果()
    {
        // Arrange
        await TestHelpers.SeedProductsAsync(DatabaseManager, 15);

        // Act - 使用 Flurl 建構 QueryString
        var url = "/products"
                  .SetQueryParam("pageSize", 5)
                  .SetQueryParam("page", 2);

        var response = await HttpClient.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(15);
                    result.PageSize.Should().Be(5);
                    result.Page.Should().Be(2);
                    result.PageCount.Should().Be(3);
                    result.Items.Should().HaveCount(5);
                    result.Items.Should().AllSatisfy(product =>
                    {
                        product.Id.Should().NotBeEmpty();
                        product.Name.Should().NotBeNullOrEmpty();
                        product.Price.Should().BeGreaterThan(0);
                    });
                });
    }
```

### 搜尋功能測試

```csharp
[Fact]
public async Task GetProducts_使用搜尋參數_應回傳符合條件的產品()
{
    // Arrange
    await TestHelpers.SeedProductsAsync(DatabaseManager, 5);
    await TestHelpers.SeedSpecificProductAsync(DatabaseManager, "特殊產品", 199.99m);

    // Act - 使用 Flurl 建構複雜查詢
    var url = "/products"
              .SetQueryParam("keyword", "特殊")
              .SetQueryParam("pageSize", 10);

    var response = await HttpClient.GetAsync(url, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be200Ok()
            .And.Satisfy<PagedResult<ProductResponse>>(result =>
            {
                result.Total.Should().Be(1);
                result.Items.Should().HaveCount(1);
                
                var product = result.Items.First();
                product.Name.Should().Be("特殊產品");
                product.Price.Should().Be(199.99m);
            });
}
```

## ExceptionHandler 整合測試策略

### 測試設計原則

整合測試的例外處理驗證需要涵蓋完整的錯誤處理流程，從例外拋出到最終的 HTTP 回應格式。我們的測試策略包括：

**不同類型錯誤回應的驗證**：確保各種例外都能正確轉換為對應的 HTTP 狀態碼和 ProblemDetails 結構。

**ProblemDetails 結構完整性測試**：驗證錯誤回應包含所有必要的標準欄位。

**ValidationProblemDetails 多欄位錯誤驗證**：測試複雜的驗證情境，確保所有驗證錯誤都能正確回傳。

### 全域例外處理器測試

找不到產品時，service 會擲出 `KeyNotFoundException`，穿越 controller 進入 `GlobalExceptionHandler`。這裡斷言的 Title「資源不存在」是 handler 專屬的對應（controller 已不再自行回傳 404），因此測試通過就代表 handler 真的被執行：

```csharp
[Fact]
public async Task GetById_當產品不存在_由GlobalExceptionHandler對應為404()
{
    // Arrange
    var nonExistentId = Guid.NewGuid();

    // Act：service 找不到產品會擲出 KeyNotFoundException，穿越 controller 進入 handler
    var response = await HttpClient.GetAsync($"/Products/{nonExistentId}", TestContext.Current.CancellationToken);

    // Assert：Title「資源不存在」為 GlobalExceptionHandler 專屬
    response.Should().Be404NotFound()
            .And.Satisfy<ProblemDetails>(problem =>
            {
                problem.Type.Should().Be("https://httpstatuses.com/404");
                problem.Title.Should().Be("資源不存在");
                problem.Status.Should().Be(404);
                problem.Detail.Should().Contain($"找不到 ID 為 {nonExistentId} 的產品");
            });
}
```

未預期例外則會落到 `GlobalExceptionHandler` 的 fallback 500。我們用 `WithWebHostBuilder` 把 repository 換成會擲出例外的假實作，模擬基礎設施失敗：

```csharp
[Fact]
    public async Task 未預期例外_由GlobalExceptionHandler對應為500()
    {
        // Arrange：以會擲出未預期例外的 repository 覆寫 DI，模擬基礎設施失敗。
        // 衍生 factory 與 client 都以 using 明確釋放，不依賴父 factory 的最終清理。
        await using var faultyFactory = Factory
                                       .WithWebHostBuilder(b => b.ConfigureServices(services =>
                                       {
                                           services.RemoveAll<IProductRepository>();
                                           services.AddScoped<IProductRepository, ThrowingProductRepository>();
                                       }));
        using var faultyClient = faultyFactory.CreateClient();

        // Act
        var response = await faultyClient.GetAsync($"/Products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert：落到 switch 的 fallback 分支
        response.Should().Be500InternalServerError()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/500");
                    problem.Title.Should().Be("內部伺服器錯誤");
                    problem.Status.Should().Be(500);
                });
    }
```

最關鍵的是**反向驗證**：把所有 `IExceptionHandler` 註冊移除後，同一個 `KeyNotFoundException` 不再得到 handler 的 404，而是預設中介軟體的 500。這證明前面那些 404 確實來自 handler，而不是 controller：

```csharp
[Fact]
public async Task 移除ExceptionHandler註冊後_KeyNotFound不再對應為404()
{
    // Arrange：移除所有 IExceptionHandler 註冊（衍生 factory 與 client 以 using 明確釋放）
    await using var noHandlerFactory = Factory
        .WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<IExceptionHandler>();
        }));
    using var noHandlerClient = noHandlerFactory.CreateClient();

    // Act
    var response = await noHandlerClient.DeleteAsync($"/Products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

    // Assert：不再是 handler 的 404，而是預設中介軟體的 500
    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
}
```

搭配「排序欄位非法 → `ArgumentException` → 400」與「請求無效 → `ValidationException` → `FluentValidationExceptionHandler`」的案例，`GlobalExceptionHandler` 的 KeyNotFound、Argument、fallback 500 三條必要路徑，以及 `FluentValidationExceptionHandler` 的驗證路徑，都有真正穿越 middleware 的整合測試涵蓋。

### ValidationProblemDetails 測試策略

#### 單一欄位驗證錯誤測試

驗證單一欄位的驗證失敗情境：

```csharp
[Fact]
public async Task CreateProduct_當產品名稱為空_應回傳400BadRequest()
{
    // Arrange
    var invalidRequest = new ProductCreateRequest
    {
        Name = "",
        Price = 100.00m
    };

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                problem.Title.Should().Be("One or more validation errors occurred.");
                problem.Status.Should().Be(400);
                problem.Errors.Should().ContainKey("Name");
                problem.Errors["Name"].Should().Contain("產品名稱不能為空");
            });
}
```

#### 多欄位同時驗證錯誤測試

測試多個欄位同時發生驗證錯誤的情境：

```csharp
[Fact]
public async Task CreateProduct_當產品名稱和價格都無效_應回傳400BadRequest()
{
    // Arrange
    var invalidRequest = new ProductCreateRequest
    {
        Name = "",
        Price = -10.00m
    };

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Status.Should().Be(400);
                problem.Errors.Should().ContainKey("Name");
                problem.Errors.Should().ContainKey("Price");
                problem.Errors["Name"].Should().Contain("產品名稱不能為空");
                problem.Errors["Price"].Should().Contain("產品價格必須大於 0");
            });
}
```

#### 複雜驗證規則的測試涵蓋

測試產品名稱長度限制的情境：

```csharp
[Fact]
    public async Task CreateProduct_當產品名稱超過長度限制_應回傳400BadRequest()
    {
        // Arrange
        var invalidRequest = new ProductCreateRequest
        {
            Name = new string('A', 101), // 超過 100 字元限制
            Price = 100.00m
        };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest, TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be400BadRequest()
                .And.Satisfy<ValidationProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                    problem.Title.Should().Be("One or more validation errors occurred.");
                    problem.Status.Should().Be(400);
                    problem.Errors.Should().ContainKey("Name");
                    problem.Errors["Name"].Should().Contain("產品名稱不能超過 100 個字元");
                });
    }
```

### 測試最佳實務

#### 錯誤情境的完整涵蓋策略

1. **正常流程測試**：確保正確的請求能正常處理
2. **驗證錯誤測試**：涵蓋所有可能的驗證失敗情境
3. **業務邏輯錯誤測試**：測試業務規則違反的情況
4. **系統例外測試**：模擬系統層級的例外情況

#### 測試案例的可讀性設計

**清楚的測試命名**：測試方法名稱要能清楚表達測試情境和期望結果

```csharp
CreateProduct_當產品名稱為空_應回傳400BadRequest()
GetById_當產品不存在_由GlobalExceptionHandler對應為404()
```

**3A 模式的嚴格遵循**：每個測試都要有清楚的 Arrange、Act、Assert 區段

```csharp
// Arrange - 準備測試資料
var invalidRequest = new ProductCreateRequest { Name = "", Price = 100.00m };

// Act - 執行被測試的動作
var response = await HttpClient.PostAsJsonAsync(
    "/products",
    invalidRequest,
    TestContext.Current.CancellationToken);

// Assert - 驗證結果
response.Should().Be400BadRequest();
```

#### 例外處理的效能考量

**避免過度詳細的錯誤訊息**：在 GlobalExceptionHandler 中要平衡資訊完整性和安全性

**日誌記錄策略**：確保例外處理器會記錄適當的日誌等級

```csharp
_logger.LogError(exception, "發生未處理的異常: {Message}", exception.Message);
_logger.LogWarning(validationException, "驗證失敗: {Message}", validationException.Message);
```

**回應時間監控**：整合測試中要確保錯誤處理不會顯著影響回應時間

這組測試確認不同例外會轉成一致的 ProblemDetails 格式。若 handler、狀態碼或欄位對應改壞，測試會直接指出差異。

## 實際整合測試案例分析

以下檢視專案實際執行的整合測試：

### ProductsController 實際測試案例

以下是專案中的真實測試程式碼案例：

#### 1. 產品建立測試

```csharp
[Fact]
public async Task CreateProduct_使用有效資料_應成功建立產品()
{
    // Arrange
    var request = TestHelpers.CreateProductRequest("新產品", 299.99m);

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", request, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be201Created()
            .And.Satisfy<ProductResponse>(product =>
            {
                product.Id.Should().NotBeEmpty();
                product.Name.Should().Be("新產品");
                product.Price.Should().Be(299.99m);
                product.CreatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
                product.UpdatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
            });
}
```

#### 2. 驗證錯誤測試

```csharp
[Fact]
public async Task CreateProduct_當產品名稱為空_應回傳400BadRequest()
{
    // Arrange
    var invalidRequest = new ProductCreateRequest
    {
        Name = "",
        Price = 100.00m
    };

    // Act
    var response = await HttpClient.PostAsJsonAsync("/products", invalidRequest, TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be400BadRequest()
            .And.Satisfy<ValidationProblemDetails>(problem =>
            {
                problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                problem.Title.Should().Be("One or more validation errors occurred.");
                problem.Status.Should().Be(400);
                problem.Errors.Should().ContainKey("Name");
                problem.Errors["Name"].Should().Contain("產品名稱不能為空");
            });
}
```

#### 3. 分頁查詢測試

```csharp
[Fact]
    public async Task GetProducts_使用分頁參數_應回傳正確的分頁結果()
    {
        // Arrange
        await TestHelpers.SeedProductsAsync(DatabaseManager, 15);

        // Act - 使用 Flurl 建構 QueryString
        var url = "/products"
                  .SetQueryParam("pageSize", 5)
                  .SetQueryParam("page", 2);

        var response = await HttpClient.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be200Ok()
                .And.Satisfy<PagedResult<ProductResponse>>(result =>
                {
                    result.Total.Should().Be(15);
                    result.PageSize.Should().Be(5);
                    result.Page.Should().Be(2);
                    result.PageCount.Should().Be(3);
                    result.Items.Should().HaveCount(5);
                    result.Items.Should().AllSatisfy(product =>
                    {
                        product.Id.Should().NotBeEmpty();
                        product.Name.Should().NotBeNullOrEmpty();
                        product.Price.Should().BeGreaterThan(0);
                    });
                });
    }
```

#### 4. 錯誤處理測試

```csharp
[Fact]
public async Task GetById_當產品不存在_由GlobalExceptionHandler對應為404()
{
    // Arrange
    var nonExistentId = Guid.NewGuid();

    // Act：service 擲出 KeyNotFoundException，穿越 controller 進入 GlobalExceptionHandler
    var response = await HttpClient.GetAsync($"/Products/{nonExistentId}", TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be404NotFound();

    var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    var problemDetails = JsonSerializer.Deserialize<JsonElement>(content);

    // 檢查 ProblemDetails 結構（Title「資源不存在」為 GlobalExceptionHandler 專屬）
    problemDetails.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/404");
    problemDetails.GetProperty("title").GetString().Should().Be("資源不存在");
    problemDetails.GetProperty("status").GetInt32().Should().Be(404);
    problemDetails.GetProperty("detail").GetString().Should().Contain($"找不到 ID 為 {nonExistentId} 的產品");
}
```

### HealthController 測試案例

```csharp
[Fact]
public async Task GetHealth_應回傳200OK()
{
    // Act
    var response = await HttpClient.GetAsync("/health", TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be200Ok();
}
```

### 實務經驗與踩雷心得

#### 容器啟動順序很重要

在設定 Testcontainers 時，要確保容器按正確順序啟動：

```csharp
// 確保容器完全啟動
await _postgresContainer.StartAsync();
await _redisContainer.StartAsync();
```

#### 測試隔離的重要性

每個測試都應該有乾淨的起始狀態：

```csharp
public async ValueTask DisposeAsync()
{
    // 清理資料庫資料
    await DatabaseManager.CleanDatabaseAsync();
    FlurlClient.Dispose();
}
```

## 整合測試實務經驗總結

### 容器啟動順序最佳實務

專案的 TestWebApplicationFactory 已經處理了容器的正確啟動順序：

```csharp
public async ValueTask InitializeAsync()
{
    // 建立並啟動容器
    await _postgresContainer.StartAsync();
    await _redisContainer.StartAsync();
}
```

### 測試資料隔離是關鍵

專案使用 Respawner 來確保測試資料隔離：

```csharp
/// <summary>
/// 清理資料庫資料
/// </summary>
public async Task CleanDatabaseAsync()
{
    if (_respawner == null)
    {
        throw new InvalidOperationException("Respawner 尚未初始化，請先呼叫 InitializeDatabaseAsync");
    }

    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();
    await _respawner.ResetAsync(connection);
}
```

### 記憶體洩漏防範

專案中的 IntegrationTestBase 已經正確處理資源釋放：

```csharp
public virtual async ValueTask DisposeAsync()
{
    // 清理資料庫資料
    await DatabaseManager.CleanDatabaseAsync();

    FlurlClient.Dispose();
}
```

### 測試輔助工具設計

使用實際專案中的 TestHelpers 類別：

```csharp
/// <summary>
/// 驗證產品回應
/// </summary>
public static void AssertProductResponse(
    ProductResponse response,
    string expectedName,
    decimal expectedPrice,
    Guid? expectedId = null)
{
    response.Should().NotBeNull();
    response.Name.Should().Be(expectedName);
    response.Price.Should().Be(expectedPrice);
    response.CreatedAt.Should().BeAfter(DateTimeOffset.MinValue);
    response.UpdatedAt.Should().BeAfter(DateTimeOffset.MinValue);

    if (expectedId.HasValue)
    {
        response.Id.Should().Be(expectedId.Value);
    }
    else
    {
        response.Id.Should().NotBe(Guid.Empty);
    }
}
```

## 整合測試最佳實務建議

### 測試環境設定管理

專案使用 TestWebApplicationFactory 統一管理測試環境設定，確保測試環境的一致性和可重複性。

### 測試失敗診斷策略

在測試失敗時，有效的診斷策略能幫助快速定位問題：

1. **日誌分析**：檢查應用程式日誌和容器日誌
2. **資料庫狀態**：檢查測試執行前後的資料庫狀態
3. **網路狀態**：確認容器間的網路連線正常
4. **環境變數**：驗證測試環境的設定變數
5. **時間因素**：檢查時間相關的邏輯是否正確

### 測試效能監控

建立測試效能的監控機制：

在整合測試中，監控測試執行效能對於確保測試品質很重要：

1. **執行時間監控**：使用 `Stopwatch` 測量關鍵操作的執行時間
2. **記憶體使用監控**：透過 `GC.GetTotalMemory()` 檢查記憶體使用狀況
3. **容器健康檢查**：定期檢查 Testcontainers 的執行狀態
4. **資料庫效能**：監控 SQL 查詢的執行時間
5. **API 回應時間**：確保 HTTP 請求在合理時間內完成

### 除錯技巧與故障排除

專案中實際的健康檢查測試範例：

```csharp
[Fact]
public async Task Get_Health_應回傳200狀態碼()
{
    // Arrange
    // (無需特別準備)

    // Act
    var response = await HttpClient.GetAsync("/health", TestContext.Current.CancellationToken);

    // Assert
    response.Should().Be200Ok();
}
```

### AwesomeAssertions 讓斷言更清晰

使用 AwesomeAssertions 的 `Satisfy` 方法可以讓複雜物件的驗證更加清晰：

```csharp
response.Should().Be200Ok()
        .And.Satisfy<PagedResult<ProductResponse>>(result =>
        {
            result.Total.Should().Be(15);
            result.Items.Should().AllSatisfy(product =>
            {
                product.Id.Should().NotBeEmpty();
                product.Name.Should().NotBeNullOrEmpty();
                product.Price.Should().BeGreaterThan(0);
            });
        });
```

## 今日總結

本篇完成 Clean Architecture 的整合測試，範圍從 ASP.NET Core Web API 延伸到全域錯誤處理：

### ExceptionHandler 與 ValidationProblemDetails 整合

**統一錯誤處理**：ASP.NET Core 10 的 `IExceptionHandler` 整合測試會檢查全域例外能否轉成結構化的 `ProblemDetails` 回應。

**模型驗證整合**：`ValidationProblemDetails` 的整合測試涵蓋了各種驗證失敗情境，從空值檢查到複雜的業務規則驗證，確保 API 能提供清楚的錯誤訊息。

### 整合測試基礎設施最佳實務

**Testcontainers 與 Clean Architecture**：我們建立了支援 PostgreSQL 和 Redis 的完整測試環境，使用 Collection Fixture 模式確保測試效率。測試涵蓋了從 Controller 到 Repository 的完整資料流，驗證了各層級間的正確互動。

**AwesomeAssertions 的實戰應用**：斷言同時檢查 HTTP 狀態碼、標頭與回應物件，失敗時也能留下較清楚的差異。

### 測試資料管理策略

**資料隔離與清理**：`DatabaseManager` 在測試之間清除資料，讓每個案例從可預期的狀態開始，避免前一個測試留下的資料干擾結果。

**TestHelpers 設計模式**：測試資料的建立方法統一收在一處，Arrange 段落就短了下來，整組測試也比較好讀。

### 時間控制與可預測性

**TimeProvider 整合**：production code（`ProductService`）以注入的 `TimeProvider` 取得時間，測試端用 `FakeTimeProvider` 取代它，時間戳記就變得可預測、可推進。

由於本專案的 `TimeProvider` 註冊在共享的 collection fixture，且 `FakeTimeProvider.SetUtcNow` 不允許把時鐘往回撥，因此需要控制時間的測試各自以 `WithWebHostBuilder` 覆寫一個獨立時鐘，避免污染其他測試的絕對時間斷言。共用的覆寫邏輯收在 `CreateFactoryWithClock` 輔助方法：

```csharp
    // 以指定時鐘覆寫 DI，回傳衍生的 WebApplicationFactory；由呼叫端以 await using 明確釋放，
    // 不依賴父 factory 的最終清理。
    private WebApplicationFactory<Program> CreateFactoryWithClock(FakeTimeProvider clock)
    {
        return Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
    }

[Fact]
    public async Task 更新產品_UpdatedAt應隨Advance前進而CreatedAt不變()
    {
        // Arrange：在基準時間建立產品
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        await using var factory = CreateFactoryWithClock(clock);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/products",
            TestHelpers.CreateProductRequest("可推進時間的產品", 200m),
            TestContext.Current.CancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken);
        created.Should().NotBeNull();

        // Act：把時鐘往前推 2 小時，再更新
        clock.Advance(TimeSpan.FromHours(2));
        var updateResponse = await client.PutAsJsonAsync(
            $"/products/{created!.Id}",
            new ProductUpdateRequest { Name = "已更新", Price = 250m },
            TestContext.Current.CancellationToken);
        updateResponse.Should().Be204NoContent();

        // Assert：UpdatedAt 應剛好是 CreatedAt + 2 小時，CreatedAt 保持不變
        var getResponse = await client.GetAsync($"/products/{created.Id}", TestContext.Current.CancellationToken);
        getResponse.Should().Be200Ok()
                   .And.Satisfy<ProductResponse>(product =>
                   {
                       product.CreatedAt.Should().Be(start);
                       product.UpdatedAt.Should().Be(start.AddHours(2));
                   });
    }
```

> 這也牽動一個容易被忽略的細節：如果測試資料表用 `BEFORE UPDATE` 觸發器把 `updated_at` 覆寫成 `NOW()`，就會蓋掉 `TimeProvider` 的值，讓時間控制失效。因此本專案的建表 SQL 刻意不放這個觸發器（見前面「SQL 指令碼外部化策略」的建表範例），讓時間戳記真的由應用程式（`TimeProvider`）決定。

### 實務開發心得

1. **從簡單到複雜**：先建立基本的 CRUD 測試，再加入錯誤處理、分頁查詢等進階功能
2. **重視錯誤情境**：好的 API 不只要處理正常情況，更要優雅地處理各種例外
3. **測試要有實際意義**：測試案例要反映真實的使用情境，不只是為了涵蓋率
4. **基礎設施是關鍵**：投資時間建立好的測試基礎設施，後續的測試開發會更有效率

整合測試比單元測試麻煩，換來的是最接近真實環境的驗證。單元測試給不了這種信心。

## 在本機執行測試（MTP）

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 sample 的 `global.json` 指定。**務必先切換到該 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗；也不要從 repository root 直接指定子目錄 solution：

```powershell
Set-Location samples/day23
dotnet test --solution Day23.ProductApi.sln -c Release
```

## 參考資料

- [ASP.NET Core 整合測試](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [AwesomeAssertions 官方文件](https://awesomeassertions.org/)
- [Clean Architecture 實作指南](https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [xUnit Collection Fixtures](https://xunit.net/docs/shared-context#collection-fixture)

### ExceptionHandler 相關資料

- [處理 ASP.NET Core 中的錯誤 | Microsoft Learn](https://learn.microsoft.com/zh-tw/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [ProblemDetails 類別 | Microsoft Learn](https://learn.microsoft.com/zh-tw/dotnet/api/microsoft.aspnetcore.mvc.problemdetails?view=aspnetcore-10.0)
- [ValidationProblemDetails 類別 | Microsoft Learn](https://learn.microsoft.com/zh-tw/dotnet/api/microsoft.aspnetcore.mvc.validationproblemdetails?view=aspnetcore-10.0)
- [ASP.NET 8 新增的錯誤處理 - Yoko's Note](https://blog.yowko.com/aspnetcore-8-error-handling/)
- [Problem Details RFC 9457（現行規範，取代 RFC 7807）](https://datatracker.ietf.org/doc/html/rfc9457)

### Flurl

- [整合測試 - 使用 Flurl 簡化建立 QueryString | mrkt](https://www.dotblogs.com.tw/mrkt/2023/11/02/182326)

### Respawner

- [Respawner - Github](https://github.com/jbogard/Respawn)
- [Faster .NET Database Integration Tests with Respawn and xUnit | Khalid Abuhakmeh](https://khalidabuhakmeh.com/faster-dotnet-database-integration-tests-with-respawn-and-xunit)

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day23>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十三天。明天會介紹 Day 24 - .NET Aspire Testing 入門基礎介紹。**
