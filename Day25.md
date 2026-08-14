---
day: 25
title: "Day 25 - .NET Aspire 整合測試實戰：從 Testcontainers 到 .NET Aspire Testing"
sample: samples/day25
target_framework: net10.0
packages:
  - Aspire.Hosting.PostgreSQL
  - Aspire.Hosting.Redis
  - Aspire.Hosting.Testing
  - Aspire.Npgsql
  - Aspire.StackExchange.Redis
  - Dapper
  - FluentValidation
  - FluentValidation.DependencyInjectionExtensions
  - Microsoft.AspNetCore.OpenApi
  - Microsoft.Extensions.DependencyInjection.Abstractions
  - Microsoft.Extensions.Logging.Abstractions
  - Microsoft.OpenApi
  - Npgsql
  - StackExchange.Redis
  - AwesomeAssertions
  - AwesomeAssertions.Web
  - Flurl.Http
  - Microsoft.Testing.Extensions.TrxReport
  - Respawn
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 25 - .NET Aspire 整合測試實戰：從 Testcontainers 到 .NET Aspire Testing

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [遷移前先保留 baseline](#遷移前先保留-baseline)
- [專案架構](#專案架構)
- [使用 per-day CPM](#使用-per-day-cpm)
- [Aspire 13 AppHost 專案格式](#aspire-13-apphost-專案格式)
- [在 AppHost 編排三個服務](#在-apphost-編排三個服務)
- [API 使用 Aspire client integrations](#api-使用-aspire-client-integrations)
- [xUnit v3 與 Microsoft Testing Platform](#xunit-v3-與-microsoft-testing-platform)
- [Fixture：只啟動一次完整 AppHost](#fixture只啟動一次完整-apphost)
- [Aspire 13.4 的 HTTPS-first 行為](#aspire-134-的-https-first-行為)
- [資料庫只初始化一次](#資料庫只初始化一次)
- [每個測試前同時重設 PostgreSQL 與 Redis](#每個測試前同時重設-postgresql-與-redis)
- [Web API 測試](#web-api-測試)
- [直接驗證 Aspire 資源](#直接驗證-aspire-資源)
- [執行方式](#執行方式)
- [實測結果](#實測結果)
- [NuGet 套件稽核](#nuget-套件稽核)
- [Portability 驗證](#portability-驗證)
- [Testcontainers 還是 Aspire Testing？](#testcontainers-還是-aspire-testing)
- [常見問題](#常見問題)
- [小結](#小結)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

Day23 已經用 Testcontainers 啟動 PostgreSQL 與 Redis，再透過測試用的 WebApplicationFactory 驗證 API。Day25 的問題不同：當應用程式本身已採用 Aspire，能不能直接重用 AppHost 描述的整套資源模型，不再在測試專案維護另一份容器設定？

可以。Aspire Testing 能在測試處理程序裡啟動 AppHost，取得資源連線字串與 endpoint，並在測試結束後清掉整個 session。本篇保留 Day25 原本的 PostgreSQL、Redis、Web API 與 16 個測試，只更新到 .NET 10、Aspire 13.4.6 與 xUnit v3 + Microsoft Testing Platform。

先說清楚 Day23 在這裡扮演的角色：它是 Testcontainers 作法的比較基準，不是這次要改寫的物件。Day25 不會回頭修改 Day23，也不會把 Day23 後續遷移時新增的功能搬進來。專案設定與驗證流程則沿用 Day19～24 已經驗證過的 per-day CPM、MTP、連續測試、TRX 與 portability 作法。

## 本篇內容

- 將 AppHost 從 Aspire 9.x 格式升級到 Aspire 13.4.6
- 用 AppHost 編排 PostgreSQL 18.3、Redis 與 Web API
- 將 xUnit v2 遷移到 xUnit v3 + MTP
- 用 health state、實際連線與 HTTP request 判斷服務就緒
- 讓資料庫只初始化一次，修正 `CREATE DATABASE` 競爭條件
- 每個測試前同時清除 PostgreSQL 與 Redis
- 檢查 Aspire 13.4 HTTPS-first 與 PostgreSQL image 變更
- 用連續測試、TRX、package audit 與 repo 外複製驗證遷移結果

## 遷移前先保留 baseline

Day25 原本已經 target `net10.0`，但仍依賴根目錄 CPM。遷移前執行 restore 會遇到：

```text
NU1605: Microsoft.Extensions.DependencyInjection.Abstractions
從 10.0.9 降到 10.0.5
```

舊 package graph 還包含有已知弱點的 MessagePack 2.5.192、Microsoft.OpenApi 2.0.0 與 OpenTelemetry.Api 1.14.0。

舊建置產物可以列出 16 個測試。完整執行結果是 15 passed、1 failed，約 1 分 27 秒。失敗發生在 `DatabaseManager.EnsureDatabaseExistsAsync`：多個測試類別同時判斷 `productdb` 不存在，接著一起執行 `CREATE DATABASE`，其中一個收到 PostgreSQL `23505 duplicate key`。

這些資訊很重要。若只看遷移後綠燈，我們無法分辨是框架升級造成的問題，還是原本就存在的 race condition。

## 專案架構

```text
samples/day25/
├── Day25.AspireIntegration.sln
├── Directory.Packages.props
├── global.json
├── Day25.AppHost/
│   ├── Day25.AppHost.csproj
│   └── Program.cs
├── src/
│   ├── Day25.Domain/
│   ├── Day25.Application/
│   ├── Day25.Infrastructure/
│   └── Day25.Api/
└── tests/
    └── Day25.Tests.Integration/
        ├── Controllers/
        │   ├── HealthControllerTests.cs
        │   └── ProductsControllerTests.cs
        ├── Infrastructure/
        │   ├── AspireAppFixture.cs
        │   ├── DatabaseManager.cs
        │   ├── IntegrationTestBase.cs
        │   └── IntegrationTestCollection.cs
        └── VerifyAspireContainers.cs
```

執行測試時的資源關係如下：

```text
PostgreSQL 18.3 ── productdb ─┐
                              ├── day25-api ── HttpClient ── xUnit v3 tests
Redis ────────────────────────┘
```

Testcontainers 與 Aspire Testing 都會啟動真實容器。差別不在「真不真」，而在資源模型放在哪裡：Testcontainers 通常由測試 fixture 直接描述容器；Aspire Testing 則啟動既有 AppHost，讓開發環境與測試共用同一份編排定義。

## 使用 per-day CPM

Day25 新增自己的 `Directory.Packages.props`，因此複製到 repo 外也不會誤用根目錄的版本。以下節錄這次遷移最關鍵的版本設定：

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup Label="Aspire">
    <PackageVersion Include="Aspire.Hosting.PostgreSQL" Version="13.4.6" />
    <PackageVersion Include="Aspire.Hosting.Redis" Version="13.4.6" />
    <PackageVersion Include="Aspire.Hosting.Testing" Version="13.4.6" />
    <PackageVersion Include="Aspire.Npgsql" Version="13.4.6" />
    <PackageVersion Include="Aspire.StackExchange.Redis" Version="13.4.6" />
  </ItemGroup>

  <ItemGroup Label="Application">
    <PackageVersion Include="Dapper" Version="2.1.79" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
    <PackageVersion Include="Microsoft.OpenApi" Version="2.11.0" />
    <PackageVersion Include="Npgsql" Version="10.0.3" />
    <PackageVersion Include="StackExchange.Redis" Version="3.1.13" />
  </ItemGroup>
</Project>
```

Aspire SDK、Hosting、Testing 與 client integrations 使用同一個 13.4.6 patch。`Microsoft.Extensions.*` 也對齊到 10.0.10，排除遷移前的 `NU1605`。

這次有一個刻意的 transitive pin。`Microsoft.AspNetCore.OpenApi` 10.0.10 會帶入具有高嚴重性公告的 `Microsoft.OpenApi` 2.0.0。直接跳到 3.x 會多承擔一次 major upgrade，因此先釘選相容的 2.x 最新穩定版 2.11.0，再用完整 API 測試與 vulnerable audit 確認結果。

另外移除幾個範例沒有直接使用的套件：

- `Microsoft.Bcl.TimeProvider`：`net10.0` 已內建 `TimeProvider`
- `Microsoft.AspNetCore.Mvc.Testing`：Aspire Testing 直接啟動 AppHost，沒有使用 WebApplicationFactory
- 測試專案的 `FluentValidation`：測試沒有直接使用其型別
- `Serilog.AspNetCore`：API 沒有設定或使用 Serilog
- `Microsoft.Extensions.Caching.Abstractions`：快取實作直接使用 StackExchange.Redis

停止維護的 `FluentValidation.AspNetCore` 則改為 `FluentValidation.DependencyInjectionExtensions`，`AddValidatorsFromAssemblyContaining` 的 DI 掃描仍可照常使用。

## Aspire 13 AppHost 專案格式

Aspire 9.x 的 AppHost 使用雙 SDK 格式：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="9.3.0" />
  <PropertyGroup>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>
  <PackageReference Include="Aspire.Hosting.AppHost" />
</Project>
```

Aspire 13 改由 AppHost SDK 當作專案 SDK：

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.4.6">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.PostgreSQL" />
    <PackageReference Include="Aspire.Hosting.Redis" />
  </ItemGroup>
</Project>
```

`IsAspireHost` 與 `Aspire.Hosting.AppHost` 直接參考都不再需要。保留舊設定不一定立刻編譯失敗，卻會讓 SDK 自動提供的內容與手動參考重複，之後更難判斷版本來源。

## 在 AppHost 編排三個服務

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
                      .WithLifetime(ContainerLifetime.Session);

var postgresDb = postgres.AddDatabase("productdb");

var redis = builder.AddRedis("redis")
                   .WithLifetime(ContainerLifetime.Session);

builder.AddProject<Day25_Api>("day25-api")
       .WithReference(postgresDb)
       .WithReference(redis)
       .WaitFor(postgresDb)
       .WaitFor(redis);

builder.Build().Run();
```

`WithReference` 把連線資訊注入 API；`WaitFor` 表達啟動相依性；`ContainerLifetime.Session` 確保測試 session 結束後清理容器。這三件事解決的是不同問題，不能互相取代。

Day25 沒有 persistent volume。Aspire 13.4 的 PostgreSQL 預設 image 已升到 18.3，若既有專案從 PostgreSQL 17 升級且使用 volume，還要另外處理資料目錄格式。這個範例每次建立乾淨 session，因此可以直接測試新預設值。測試會執行 `SHOW server_version` 並確認實際版本以 `18.3` 開頭，不只相信文件宣稱。

## API 使用 Aspire client integrations

API 從設定中的連線名稱取得 client：

```csharp
builder.AddNpgsqlDataSource("productdb");
builder.AddRedisClient("redis");
```

AppHost 的 `WithReference(postgresDb)` 與 `WithReference(redis)` 會提供對應 connection string。Repository 直接注入 `NpgsqlDataSource`，cache service 則注入 `IConnectionMultiplexer`。測試不需要再用 in-memory configuration 手動塞入 host、port、username 與 password。

## xUnit v3 與 Microsoft Testing Platform

per-day `global.json` 使用與 Day19～24 相同的格式：

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`latestFeature` 會在相同 major/minor（10.0）中，選擇不低於 10.0.300 的最高已安裝 feature band 與 patch；本次環境選到 10.0.302。

測試專案的核心設定是：

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Aspire.Hosting.Testing" />
  <PackageReference Include="AwesomeAssertions" />
  <PackageReference Include="AwesomeAssertions.Web" />
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
  <PackageReference Include="xunit.v3.mtp-v2" />
</ItemGroup>
```

這裡只節錄測試框架與 runner 的核心套件。完整測試專案另外引用 `Flurl.Http`、`Npgsql`、`Respawn` 與 `StackExchange.Redis`；下文搜尋測試使用的 `SetQueryParam` 就是 `Flurl.Http` 提供的擴充方法。

這個 repo 採用 .NET 10 原生 MTP，所以移除：

- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`

xUnit v3 的 `IAsyncLifetime` 使用 `ValueTask`，所有清理都放在 `DisposeAsync`。測試內可取消的 HTTP 與 Npgsql 呼叫則使用 `TestContext.Current.CancellationToken`，建置結果沒有 xUnit1051 warning。API request cancellation 會經過 FluentValidation、service、Redis cache 與 repository；Dapper command 使用 `CommandDefinition` 把 token 傳到實際 SQL command，Redis command 則以 `WaitAsync(cancellationToken)` 約束等待，而且不會用一般例外處理吞掉取消例外。

## Fixture：只啟動一次完整 AppHost

Day25 的 collection fixture 同時管理 AppHost、HttpClient、PostgreSQL manager 與 Redis connection。初始化的核心流程如下：

```csharp
public async ValueTask InitializeAsync()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var cancellationToken = timeout.Token;

    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Day25_AppHost>(cancellationToken);

    _app = await appHost.BuildAsync(cancellationToken);
    await _app.StartAsync(cancellationToken);

    await _app.ResourceNotifications
        .WaitForResourceHealthyAsync("postgres", cancellationToken);
    await _app.ResourceNotifications
        .WaitForResourceHealthyAsync("redis", cancellationToken);

    _postgresConnectionString = await _app
        .GetConnectionStringAsync("productdb", cancellationToken)
        ?? throw new InvalidOperationException("無法取得 productdb 連線字串");
    _redisConnectionString = await _app
        .GetConnectionStringAsync("redis", cancellationToken)
        ?? throw new InvalidOperationException("無法取得 Redis 連線字串");

    _databaseManager = new DatabaseManager(_postgresConnectionString);
    await _databaseManager.InitializeAsync(cancellationToken);

    var redisOptions = ConfigurationOptions.Parse(_redisConnectionString);
    redisOptions.AllowAdmin = true;
    redisOptions.ConnectTimeout = 10_000;
    redisOptions.AsyncTimeout = 10_000;
    _redisConnection = await ConnectionMultiplexer
        .ConnectAsync(redisOptions)
        .WaitAsync(cancellationToken);
    await RedisDatabase.PingAsync().WaitAsync(cancellationToken);

    await _app.ResourceNotifications
        .WaitForResourceHealthyAsync("day25-api", cancellationToken);

    _httpClient = _app.CreateHttpClient("day25-api", "http");

    using var response = await _httpClient.GetAsync("/health", cancellationToken);
    response.EnsureSuccessStatusCode();
}
```

這裡有三層 readiness：

1. Aspire resource state：等待 PostgreSQL、Redis、API healthy。
2. Protocol probe：Redis 執行 `PING`。
3. Application probe：API 實際呼叫 `/health`。

過去的固定 `Task.Delay` 無法回答服務是否真的 ready，只能賭啟動時間。兩分鐘 cancellation timeout 則讓失敗有明確上限，不會讓 CI 無限等待。

## Aspire 13.4 的 HTTPS-first 行為

Aspire 13.4 調整了未指定 endpoint 時的選擇順序：`CreateHttpClient` 與 `GetEndpointUriString` 會優先使用 HTTPS。Day25 的測試目標很明確，就是 AppHost 公開的 HTTP endpoint，因此直接寫：

```csharp
_httpClient = _app.CreateHttpClient("day25-api", "http");
```

明確指定 endpoint 也避開本機 HTTPS 開發憑證是否受信任的差異。`aspire doctor` 在本次環境偵測到兩張未受信任的 HTTPS 開發憑證，但 16 個 HTTP integration tests 不受影響。

## 資料庫只初始化一次

舊版 `IntegrationTestBase.InitializeAsync` 會為每個測試建立一個 `DatabaseManager`，再呼叫 `EnsureDatabaseExistsAsync`。不同測試類別可平行初始化，於是「先查不存在，再建立」不是原子操作。

新的 `DatabaseManager` 由 fixture 建立一次：

```csharp
public async Task InitializeAsync(CancellationToken cancellationToken)
{
    await EnsureDatabaseExistsAsync(cancellationToken);

    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync(cancellationToken);
    await EnsureTablesExistAsync(connection, cancellationToken);

    _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
    {
        TablesToIgnore = new Table[] { "__EFMigrationsHistory" },
        SchemasToInclude = new[] { "public" },
        DbAdapter = DbAdapter.Postgres
    });
}
```

資料庫存在檢查使用參數，不把 database name 直接串進查詢；建立資料庫時則把 identifier 正確 quote。Respawn 也明確使用 `DbAdapter.Postgres`，不依賴自動推斷。

## 每個測試前同時重設 PostgreSQL 與 Redis

容器共用可以縮短測試時間，但資料不能共用。只清 PostgreSQL 還不夠：產品查詢可能命中前一個案例留在 Redis 的 cache。

Fixture 提供一個一致的清理入口：

```csharp
public async Task CleanStateAsync(CancellationToken cancellationToken)
{
    await DatabaseManager.CleanDatabaseAsync(cancellationToken);

    var redisConnection = _redisConnection
        ?? throw new InvalidOperationException("Redis 尚未初始化");

    foreach (var endpoint in redisConnection.GetEndPoints())
    {
        await redisConnection
            .GetServer(endpoint)
            .FlushDatabaseAsync()
            .WaitAsync(cancellationToken);
    }
}
```

測試基底在每個案例開始前執行清理：

```csharp
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly AspireAppFixture Fixture;
    protected readonly HttpClient HttpClient;

    protected DatabaseManager DatabaseManager => Fixture.DatabaseManager;

    protected IntegrationTestBase(AspireAppFixture fixture)
    {
        Fixture = fixture;
        HttpClient = fixture.HttpClient;
    }

    public ValueTask InitializeAsync()
    {
        return new ValueTask(
            Fixture.CleanStateAsync(TestContext.Current.CancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
```

清理放在案例開始前，有兩個好處：失敗案例即使中途停止，下一個案例仍會先取得乾淨狀態；同時保留失敗當下的資料，除錯時比較容易重現。

## Web API 測試

產品測試保留 CRUD、分頁、搜尋、驗證與 ProblemDetails。以搜尋案例為例：

```csharp
[Fact]
public async Task GetProducts_使用搜尋參數_應回傳符合條件的產品()
{
    var cancellationToken = TestContext.Current.CancellationToken;

    await TestHelpers.SeedProductsAsync(DatabaseManager, 5, cancellationToken);
    await TestHelpers.SeedSpecificProductAsync(
        DatabaseManager,
        "特殊產品",
        199.99m,
        cancellationToken);

    var url = "/products"
              .SetQueryParam("keyword", "特殊")
              .SetQueryParam("pageSize", 10);

    var response = await HttpClient.GetAsync(url, cancellationToken);

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

FluentValidation 的錯誤仍透過 `ValidationProblemDetails` 驗證，找不到產品則驗證 `ProblemDetails` 的 title、status、type 與 detail。升級測試框架不代表要改變 API contract。

## 直接驗證 Aspire 資源

原本有一份 solution 外的重複 `VerifyAspireContainers.cs`，它自行啟動第二個 AppHost，使用固定 delay，而且根本沒有被任何 project 編譯。這次將它移除，只保留測試專案內、共用 collection fixture 的版本。

保留的兩個測試會驗證：

- PostgreSQL 與 Redis 使用動態 host port
- Redis 可以實際 `PING`
- `productdb` 可以連線
- `SHOW server_version` 回傳 18.3

這些檢查不是新增測試；原本的 16 個測試仍然是 16 個，只是把既有的資源驗證改成可執行且不重複啟動 AppHost。

## 執行方式

先確認環境：

```powershell
dotnet --info
aspire --version
aspire doctor
docker version
```

在 `samples/day25` 執行：

```powershell
dotnet clean Day25.AspireIntegration.sln
dotnet restore Day25.AspireIntegration.sln
dotnet build Day25.AspireIntegration.sln --no-restore --no-incremental
dotnet test --solution Day25.AspireIntegration.sln --no-build
```

列出測試：

```powershell
dotnet test --solution Day25.AspireIntegration.sln --no-build --list-tests
```

產生 TRX：

```powershell
dotnet test --solution Day25.AspireIntegration.sln `
  --no-build `
  --report-trx `
  --report-trx-filename day25-run1.trx
```

.NET 10 的 MTP 模式直接接受 `--report-trx`，不需要 VSTest 時代額外的 `--`。

## 實測結果

驗證環境：

- .NET SDK 10.0.302
- .NET runtime 10.0.10
- Aspire CLI／AppHost 13.4.6
- Docker client／server 29.6.2
- PostgreSQL 18.3
- Redis 8.6

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

| 執行 | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| 遷移前 xUnit v2 baseline | 16 | 15 | 1 | 0 | 約 1 分 27 秒 |
| Run 1 | 16 | 16 | 0 | 0 | 28.048 秒 |
| Run 2 | 16 | 16 | 0 | 0 | 25.472 秒 |
| repo 外 portability | 16 | 16 | 0 | 0 | 28.820 秒 |

正式 TRX：

- `samples/day25/TestResults/day25-run1.trx`
- `samples/day25/TestResults/day25-run2.trx`

測試結束後沒有 Day25 Aspire container 或 DCP process 殘留。環境中原有的 `C:\docker\mssql` 與 `C:\docker\redis` Compose container 不屬於本範例，也沒有被測試修改。

## NuGet 套件稽核

```powershell
dotnet list Day25.AspireIntegration.sln package --outdated
dotnet list Day25.AspireIntegration.sln package --deprecated --include-transitive
dotnet list Day25.AspireIntegration.sln package --vulnerable --include-transitive
```

結果：

- Direct outdated：0
- Deprecated（含 transitive）：0
- Vulnerable（含 transitive）：0

遷移前看到的 MessagePack、Microsoft.OpenApi 與 OpenTelemetry advisories 都已排除。這比「restore 沒失敗」更接近可交付的套件狀態。

## Portability 驗證

Day25 完整複製到 repo 外的乾淨目錄，遞迴排除 `bin`、`obj` 與 `TestResults`，再重新執行 restore、build、test：

```text
Restore: passed
Build: 0 warnings, 0 errors
Test: 16 passed, 0 failed, 28.820 秒
```

這證明 Day25 沒有偷偷依賴 repo 根目錄的 CPM、global.json 或舊 build artifact。

## Testcontainers 還是 Aspire Testing？

這不是「新框架一定比較好」的選擇題。

適合保留 Testcontainers 的情況：

- 專案沒有 Aspire AppHost
- 測試只需要一兩個容器
- 需要直接控制 container command、network 或特殊啟動參數
- 測試架構不應依賴 production orchestration model

適合 Aspire Testing 的情況：

- 應用本來就用 Aspire 描述多服務關係
- 測試要驗證 project、container、connection reference 與 endpoint 的組合
- 希望開發與測試共用 AppHost 資源模型
- 需要從單一入口觀察整個分散式應用程式

Day25 同時有 PostgreSQL、Redis 與 API，而且 production 專案已使用 Aspire client integrations，因此重用 AppHost 是合理選擇。若只有 Repository 對單一 PostgreSQL 的測試，直接用 Testcontainers 反而可能更單純。

## 常見問題

### restore 還是出現 NU1605

確認目前目錄是 `samples/day25`。NuGet 需要讀到 per-day `Directory.Packages.props`，才能使用已對齊的 Microsoft.Extensions 10.0.10。

### API health 等不到

先分開看三層狀態：PostgreSQL、Redis、API。查看 Aspire resource event 與 container log，不要直接增加 sleep。Fixture 的總 timeout 是兩分鐘，若 image 首次下載超過上限，應先在環境層完成 image pull，再重跑測試。

### HTTP request 出現 redirect 問題

確認使用 `CreateHttpClient("day25-api", "http")`。不要依賴 Aspire 13.4 的預設 endpoint 順序，也不要在測試裡猜測動態 port。

### 測試整批執行才出現 duplicate database

資料庫建立必須由 collection fixture 做一次，不要在每個 test case 的 `InitializeAsync` 重複執行。每個案例只重設資料，不重建 database 或 container。

### 搜尋測試偶發多一筆資料

檢查 PostgreSQL 與 Redis 是否都在案例開始前清理。只重設資料表、卻保留上一個案例的 cache，也會造成跨案例污染。

## 小結

Day25 更新 NuGet 版本之外，也重新整理 AppHost、測試生命週期與資料隔離，讓多服務整合測試有可重現的執行條件。

完成後的狀態是：.NET 10、Aspire 13.4.6、xUnit v3 + MTP、PostgreSQL 18.3、Redis 與 API 實際探測、16 個測試連續通過、套件稽核無 outdated／deprecated／vulnerable，並通過 repo 外 portability。

Day24 處理單一 SQL Server resource；Day25 再把相同原則擴展到 PostgreSQL、Redis 與 Web API。兩篇的主題沒有混在一起，但遷移方法與證據格式現在一致，可以放在同一次人工審查中比較。

## 參考資料

- [Upgrade to Aspire 13](https://learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13)
- [What's new in Aspire 13.4](https://aspire.dev/whats-new/aspire-13-4/)
- [Aspire Testing overview](https://aspire.dev/testing/overview/)
- [Access resources in Aspire tests](https://aspire.dev/testing/accessing-resources/)
- [Aspire PostgreSQL integration](https://aspire.dev/integrations/databases/postgres/postgres-host/)
- [Aspire Redis integration](https://aspire.dev/integrations/caching/stackexchange-redis/)
- [xUnit v2 to v3 migration](https://xunit.net/docs/getting-started/v3/migration)
- [.NET 10 dotnet test with MTP](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
- [Respawn](https://github.com/jbogard/Respawn)
