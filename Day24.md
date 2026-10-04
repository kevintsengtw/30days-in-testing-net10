---
day: 24
title: "Day 24 - .NET Aspire Testing 入門基礎介紹"
sample: samples/day24
target_framework: net10.0
packages:
  - Aspire.Hosting.SqlServer
  - Aspire.Hosting.Testing
  - AwesomeAssertions
  - Microsoft.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.SqlServer
  - Microsoft.Testing.Extensions.TrxReport
  - xunit.v3.mtp-v2
---

# Day 24 - .NET Aspire Testing 入門基礎介紹

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [環境準備](#環境準備)
- [Aspire Testing 是什麼](#aspire-testing-是什麼)
- [範例專案結構](#範例專案結構)
- [使用 per-day CPM 管理版本](#使用-per-day-cpm-管理版本)
- [Aspire 13 AppHost 專案格式](#aspire-13-apphost-專案格式)
- [在 AppHost 定義 SQL Server](#在-apphost-定義-sql-server)
- [將測試專案遷移到 xUnit v3 + MTP](#將測試專案遷移到-xunit-v3--mtp)
- [建立 AspireAppFixture](#建立-aspireappfixture)
- [Collection Fixture 與每個測試的資料隔離](#collection-fixture-與每個測試的資料隔離)
- [Repository 整合測試](#repository-整合測試)
- [Service 業務邏輯測試](#service-業務邏輯測試)
- [交易測試不要捕捉自己的 assertion failure](#交易測試不要捕捉自己的-assertion-failure)
- [並行測試驗證什麼](#並行測試驗證什麼)
- [執行測試](#執行測試)
- [實際遷移結果](#實際遷移結果)
- [NuGet 套件稽核](#nuget-套件稽核)
- [Portability 驗證](#portability-驗證)
- [Aspire 13.4 的行為變更](#aspire-134-的行為變更)
- [常見問題](#常見問題)
- [附錄：Windows 上的 SQL Server 登入前交握逾時](#附錄windows-上的-sql-server-登入前交握逾時)
- [小結](#小結)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

前幾天談到 ASP.NET Core 與 Testcontainers 的整合測試。今天換一個角度：如果應用程式本來就由 .NET Aspire 編排，測試是否也能直接重用 AppHost 裡的資源定義？

可以。`Aspire.Hosting.Testing` 會在測試處理程序裡啟動 AppHost，測試可以取得 Aspire 管理的 connection string、endpoint 與資源狀態。範例實際啟動 SQL Server container，再由 EF Core 執行查詢，沒有改用記憶體資料庫。

本篇範例已更新為以下版本：

- .NET 10
- Aspire 13.5.3
- xUnit v3 4.0.0
- Microsoft Testing Platform（MTP）
- Entity Framework Core 10.0.12
- SQL Server container

Day19～23 的遷移經驗會用在專案設定與驗證流程，但 Day24 保留自己的主題與測試範圍，不會把前一天的 Web API 範例搬進來。

## 本篇內容

這篇會處理五件事：

1. Aspire Testing 與 Testcontainers 的定位差異
2. Aspire 13 AppHost 的專案格式
3. 使用 `DistributedApplicationTestingBuilder` 管理測試環境
4. 正確判斷 SQL Server 是否 ready
5. 在 xUnit v3 下處理 fixture、資料隔離與 MTP 測試指令

## 環境準備

執行範例前需要：

- .NET 10 SDK
- Docker Desktop 或其他可用的 Docker daemon
- 可選：Aspire CLI 13.4，用來執行 `aspire doctor`

先確認 .NET 與 Docker：

```powershell
dotnet --version
docker version
```

`docker version` 必須同時顯示 Client 與 Server。只有 Client 版本通常表示 Docker daemon 尚未啟動。

如果已安裝 Aspire CLI，也可以先跑：

```powershell
aspire doctor
```

Aspire 13.4 的 `aspire doctor` 會檢查 CLI、AppHost SDK、.NET SDK、container runtime 與 HTTPS development certificate，也能協助找出同一台機器上互相遮蔽的 Aspire CLI。

## Aspire Testing 是什麼

一般整合測試會在測試程式裡自行建立 container、設定 port、組 connection string，再負責清理資源。Aspire Testing 的做法不同：應用程式拓撲已經寫在 AppHost，測試直接載入同一份 AppHost。

```text
BookStore.Tests
       │
       │ DistributedApplicationTestingBuilder
       ▼
BookStore.AppHost
       │
       └── SQL Server ── bookstore-db
                              │
                              ▼
                       BookStoreDbContext
```

這種方式的價值不在「幫忙啟動 container」，而在開發環境與測試環境共用同一套資源名稱、相依關係與服務探索模型。

### 與 Testcontainers 的差異

兩者都會啟動真實 container，但控制邊界不同。

| 比較項目 | Aspire Testing | Testcontainers |
| --- | --- | --- |
| 資源定義 | 重用 AppHost | 寫在測試 fixture |
| 服務編排 | 由 Aspire app model 管理 | 測試自行管理 |
| connection string | 由 Aspire 提供 | 測試自行組合 |
| 適合情境 | 專案已採用 Aspire | 任何需要 container 的測試專案 |
| 控制粒度 | 偏向整體應用拓撲 | 偏向個別 container |

如果專案沒有 AppHost，只為了單一資料庫測試而引入 Aspire，成本未必划算。反過來說，專案本來就使用 Aspire 時，測試再維護另一套 container 定義也容易產生落差。

## 範例專案結構

```text
day24/
├── Directory.Packages.props
├── global.json
├── Day24.AspireTesting.sln
├── src/
│   ├── BookStore.Core/
│   │   ├── Data/
│   │   ├── Models/
│   │   ├── Repositories/
│   │   └── Services/
│   └── BookStore.AppHost/
│       ├── BookStore.AppHost.csproj
│       └── Program.cs
└── tests/
    └── BookStore.Tests/
        ├── Helpers/
        ├── Infrastructure/
        ├── Integration/
        └── BookStore.Tests.csproj
```

`BookStore.Core` 放資料模型、EF Core DbContext、Repository 與 Service。`BookStore.AppHost` 只負責資源編排；`BookStore.Tests` 則透過 AppHost 啟動 SQL Server。

## 使用 per-day CPM 管理版本

這個 repo 有多天範例。為了讓 Day24 複製到 repo 外仍可建置，套件版本放在 `samples/day24/Directory.Packages.props`，不意外依賴根目錄設定。

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup Label="Aspire">
    <PackageVersion Include="Aspire.Hosting.SqlServer" Version="13.5.3" />
    <PackageVersion Include="Aspire.Hosting.Testing" Version="13.5.3" />
  </ItemGroup>

  <ItemGroup Label="Assertions">
    <PackageVersion Include="AwesomeAssertions" Version="9.6.0" />
  </ItemGroup>

  <ItemGroup Label="EntityFrameworkCore">
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
  </ItemGroup>

  <ItemGroup Label="Testing Frameworks">
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.4.0" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="4.0.0" />
  </ItemGroup>
</Project>
```

Aspire 套件要使用同一個 patch 版本，EF Core 套件也應對齊。

這份 CPM 裡沒有 `Microsoft.Data.SqlClient`。遷移當時曾把它釘在 7.0.2，因為 EF Core 10.0.10 帶入的 SqlClient 6.1.1 還拖著已淘汰的身分驗證相依鏈。升到 EF Core 10.0.12 之後，下限變成 6.1.6。拿掉釘選重跑 `--deprecated --include-transitive`，三個專案都是 0，44 個測試也照過，釘選就移除了。現在 `BookStore.Core` 解析到 6.1.6；測試專案同時參考 AppHost，用的是 `Aspire.Hosting.SqlServer` 帶入的 7.0.1。

## Aspire 13 AppHost 專案格式

Aspire 9 的 AppHost 常見寫法是：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="9.3.0" />
  <PackageReference Include="Aspire.Hosting.AppHost" />
</Project>
```

Aspire 13 已簡化 AppHost SDK。更新後的 `BookStore.AppHost.csproj` 如下：

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);ASPIRE010</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.SqlServer" />
  </ItemGroup>
</Project>
```

這裡有三個差異：

1. SDK 版本直接寫在 `<Project Sdk="...">`
2. 不再需要獨立的 `<Sdk Name="..." />`
3. 不再直接引用 `Aspire.Hosting.AppHost`，因為 Aspire 13 SDK 已自動提供

`NoWarn` 那一行是 Aspire 13.5 之後才需要的。13.5.0 起，AppHost 沒有啟用 Aspire CLI bundle 就會發出 `ASPIRE010`。照提示把 `AspireUseCliBundle` 設成 `true` 也不行：沒裝 Aspire CLI 的機器會改以 `ASPIRE009` 建置失敗。這個範例不要求安裝 Aspire CLI，所以抑制這則提示。

原本 AppHost 還參考了 `BookStore.Core`，但 AppHost 並沒有把它當作可執行 project resource。這會產生 `ASPIRE004`，所以本次移除那個未使用的 project reference。測試專案仍會直接參考 Core 與 AppHost。

## 在 AppHost 定義 SQL Server

`Program.cs` 保持簡單：

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddSqlServer("sql")
                      .WithLifetime(ContainerLifetime.Session)
                      .AddDatabase("bookstore-db");

builder.Build().Run();
```

這裡有兩個資源名稱：

- `sql`：SQL Server container resource
- `bookstore-db`：SQL Server 裡的 database resource

測試會等待 `sql` 健康，再向 Aspire 取得 `bookstore-db` 的 connection string。把兩者混為一談，容易出現「resource 已 Running，但資料庫還連不上」的問題。

## 將測試專案遷移到 xUnit v3 + MTP

Day24 使用 .NET 10 原生 MTP 模式。`global.json` 放在 solution 同一層：

```json
{
  "sdk": { "version": "10.0.300", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

`latestFeature` 會在相同 major/minor（10.0）中，選擇不低於 10.0.300 的最高已安裝 feature band 與 patch；遷移當時的環境選到 10.0.302。

測試專案的重點設定如下：

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Aspire.Hosting.Testing" />
  <PackageReference Include="xunit.v3.mtp-v2" />
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
  <PackageReference Include="AwesomeAssertions" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
</ItemGroup>
```

xUnit v3 測試專案會產生可執行檔，所以要設定 `OutputType=Exe`。這個 repo 採用 `xunit.v3.mtp-v2` 與 .NET 10 原生 MTP，不再保留下列 VSTest 路徑套件：

- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`

## 建立 AspireAppFixture

Collection Fixture 負責啟動一次 AppHost，所有 Day24 整合測試重用同一個 SQL Server container。

```csharp
public class AspireAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellationToken = cancellationTokenSource.Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BookStore_AppHost>(cancellationToken);

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("sql", cancellationToken);

        _connectionString = await _app.GetConnectionStringAsync(
            "bookstore-db",
            cancellationToken)
            ?? throw new InvalidOperationException("無法取得 bookstore-db 連線字串");

        await using var context = CreateDbContext(enableRetryOnFailure: true);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public BookStoreDbContext GetDbContext()
    {
        return CreateDbContext(enableRetryOnFailure: true);
    }

    public ValueTask<BookStoreDbContext> GetDbContextWithoutRetryAsync()
    {
        return ValueTask.FromResult(CreateDbContext(enableRetryOnFailure: false));
    }

    public async Task CleanDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext(enableRetryOnFailure: true);
        await context.Books.ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private BookStoreDbContext CreateDbContext(bool enableRetryOnFailure)
    {
        if (_connectionString == null)
            throw new InvalidOperationException("應用程式尚未初始化");

        var optionsBuilder = new DbContextOptionsBuilder<BookStoreDbContext>();
        optionsBuilder.UseSqlServer(_connectionString, sqlServerOptions =>
        {
            if (enableRetryOnFailure)
                sqlServerOptions.EnableRetryOnFailure();
        });

        return new BookStoreDbContext(optionsBuilder.Options);
    }
}
```

### 為什麼不再使用 Running + Delay

舊版 fixture 的流程是：

```csharp
await resource.WaitForResourceAsync(
    "bookstore-db",
    KnownResourceStates.Running,
    CancellationToken.None);

await Task.Delay(TimeSpan.FromSeconds(5));
```

這裡有兩個問題。

第一，`Running` 只表示處理程序已開始執行，不表示 SQL Server 已完成初始化。第二，`CancellationToken.None` 沒有上限；資源永遠無法 ready 時，測試也可能一直等下去。後面的五秒固定 delay 只是猜測，在較慢或較快的機器上都不可靠。

新版改等 `sql` resource 的 health check，並設兩分鐘 timeout。實際啟動時 health check 可以先回報 Unhealthy，等 SQL Server 真正接受連線後才轉成 Healthy。這才是我們需要的條件。

### 為什麼 EnsureCreated 只做一次

舊版每次呼叫 `GetDbContextAsync` 都執行 `EnsureCreatedAsync`。並行測試一次建立十個 DbContext 時，十個工作可能同時判斷資料庫不存在，再一起送出建立資料庫的命令，最後得到：

```text
Database 'bookstore-db' already exists.
```

資料庫建立屬於 fixture 初始化責任，不是 DbContext factory 的責任。移到 `InitializeAsync` 後，每次取得 DbContext 只建立 options 與 context，不會再競爭 schema 初始化。

## Collection Fixture 與每個測試的資料隔離

Collection Fixture 可以避免每個測試都重新啟動 SQL Server：

```csharp
[CollectionDefinition("AspireApp")]
public class AspireAppCollectionDefinition : ICollectionFixture<AspireAppFixture>
{
}
```

但共享 container 也代表資料會留下來。舊版只有少數測試主動清理，造成搜尋測試預期兩筆資料，卻讀到前一個測試留下的第三筆。

本次新增測試基底類別：

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly AspireAppFixture _fixture;

    protected IntegrationTestBase(AspireAppFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async ValueTask InitializeAsync()
    {
        await _fixture.CleanDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
```

xUnit 會為每個測試案例建立 test class instance，因此 `InitializeAsync` 會在每個案例開始前清除 `Books`。container 仍只啟動一次，但資料不再跨案例污染。

`IAsyncLifetime` 在 xUnit v3 使用 `ValueTask`。清理與 EF Core async API 也會傳入 `TestContext.Current.CancellationToken`，讓測試取消時不必等待資料庫操作自行逾時。

## Repository 整合測試

Repository 測試直接使用 SQL Server，不用 EF Core InMemory provider。以下案例可以驗證 identity key、decimal mapping 與真正的 SQL 寫入：

```csharp
    [Fact]
    public async Task AddAsync_有效書籍_應成功儲存並回傳含ID的書籍()
    {
        // Arrange
        using var dbContext = _fixture.GetDbContext();
        var repository = new EfCoreBookRepository(dbContext);

        var book = new Book
        {
            Title = "測試書籍",
            Author = "測試作者",
            Price = 299.99m,
            PublishedDate = DateTime.UtcNow
        };

        // Act
        var savedBook = await repository.AddAsync(book, TestContext.Current.CancellationToken);

        // Assert
        savedBook.Should().NotBeNull();
        savedBook.Id.Should().BeGreaterThan(0, "應該有自動產生的 ID");
        savedBook.Title.Should().Be("測試書籍");
        savedBook.Author.Should().Be("測試作者");
        savedBook.Price.Should().Be(299.99m);
        savedBook.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // 驗證資料確實存在於資料庫中
        var retrievedBook = await repository.GetByIdAsync(
            savedBook.Id,
            TestContext.Current.CancellationToken);
        retrievedBook.Should().NotBeNull();
        retrievedBook!.Title.Should().Be("測試書籍");
    }
```

這類測試驗證 mapping、Repository 查詢與 SQL Server 實際行為是否符合需求；EF Core 本身不是測試目標。

## Service 業務邏輯測試

Service 測試會走過 Repository 與真實資料庫，同時驗證業務規則：

```csharp
[Theory]
[InlineData("", "作者", 100, "書籍標題不可為空")]
[InlineData("標題", "", 100, "作者不可為空")]
[InlineData("標題", "作者", 0, "價格必須大於零")]
[InlineData("標題", "作者", 15000, "價格不可超過 10,000 元")]
public async Task CreateBookAsync_無效資料_應拋出ArgumentException(
    string title,
    string author,
    decimal price,
    string expectedMessage)
{
    var bookService = CreateBookService();

    var exception = await Assert.ThrowsAsync<ArgumentException>(
        async () => await bookService.CreateBookAsync(
            title,
            author,
            price,
            TestContext.Current.CancellationToken));

    exception.Message.Should().Contain(expectedMessage);
}
```

`BookServiceTests` 會保留該測試案例建立的 `BookStoreDbContext`，並在覆寫的 `DisposeAsync` 中非同步釋放；直接建立 context 的簡化測試則使用 `using`。共用 container 不代表每個測試建立的 EF Core context 也可以省略清理。

搜尋條件測試包含 `null`，因此 production contract 也應反映實際支援的輸入：

```csharp
Task<IEnumerable<Book>> SearchBooksByAuthorAsync(
    string? author,
    CancellationToken cancellationToken = default);
```

只把測試參數改成 nullable、卻繼續把它傳給宣告為 non-null 的 production API，會留下 `CS8604`。這次同步修正 Service 與 Repository 介面和實作，讓型別契約與既有行為一致；production 呼叫鏈也會把 cancellation token 一路傳到 EF Core。

## 交易測試不要捕捉自己的 assertion failure

舊版交易測試使用 `catch (Exception)` 包住整個 Arrange、Act 與 Assert。當資料庫沒有拒絕所謂的無效資料時，`Assert.Fail` 自己拋出的例外也會被 catch，接著測試仍然通過。這是典型 false positive。

新版明確要求資料庫拋出 `DbUpdateException`，再執行 rollback：

```csharp
[Fact]
public async Task CreateBooks_使用交易_失敗時應完整復原()
{
    using var dbContext = await _fixture.GetDbContextWithoutRetryAsync();
    using var transaction = await dbContext.Database.BeginTransactionAsync(
        TestContext.Current.CancellationToken);

    var validBook = new Book { Title = "有效書籍", Author = "作者", Price = 100m };
    var invalidBook = new Book { Title = null!, Author = "作者", Price = 100m };

    dbContext.Books.Add(validBook);
    await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

    dbContext.Books.Add(invalidBook);
    var action = async () => await dbContext.SaveChangesAsync(
        TestContext.Current.CancellationToken);

    await action.Should().ThrowAsync<DbUpdateException>();
    await transaction.RollbackAsync(TestContext.Current.CancellationToken);

    using var verifyContext = _fixture.GetDbContext();
    var bookCount = await verifyContext.Books.CountAsync(
        b => b.Title == "有效書籍",
        TestContext.Current.CancellationToken);

    bookCount.Should().Be(0);
}
```

這裡刻意使用 non-retry DbContext。啟用 `EnableRetryOnFailure` 的 execution strategy 不支援直接建立 user-initiated transaction；若業務需要 retry 與 transaction 同時存在，應使用 `CreateExecutionStrategy()` 包住整個交易單元。

## 並行測試驗證什麼

並行案例同時建立十個 DbContext 與十本書：

```csharp
var tasks = new List<Task<int>>();

for (var i = 0; i < 10; i++)
{
    var title = $"並行測試書籍 {i:D2}";

    tasks.Add(Task.Run(async () =>
    {
        using var dbContext = _fixture.GetDbContext();
        var book = new Book
        {
            Title = title,
            Author = "並行作者",
            Price = 99.99m
        };

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return book.Id;
    }));
}

var bookIds = await Task.WhenAll(tasks);
bookIds.Should().OnlyHaveUniqueItems();
```

每個工作使用自己的 DbContext，因為 `DbContext` 不是 thread-safe。fixture 已在啟動階段完成 schema 初始化，所以這裡只測並行資料寫入，不會把「十個工作同時建資料庫」混入測試目標。

## 執行測試

從 `samples/day24` 執行：

```powershell
dotnet restore Day24.AspireTesting.sln
dotnet build Day24.AspireTesting.sln --no-restore --no-incremental
dotnet test --solution Day24.AspireTesting.sln --no-build
```

列出測試：

```powershell
dotnet test --solution Day24.AspireTesting.sln --no-build --list-tests
```

產生 TRX：

```powershell
dotnet test --solution Day24.AspireTesting.sln `
  --no-build `
  --report-trx `
  --report-trx-filename day24.trx
```

.NET 10 原生 MTP 使用 `--solution` 明確指定 solution，MTP option 直接接在 `dotnet test` 後面，不需要再用額外的 `--` 轉發。

## 實際遷移結果

遷移前的 xUnit v2 baseline：

| Total | Passed | Failed | Skipped | Duration |
| ---: | ---: | ---: | ---: | ---: |
| 44 | 42 | 2 | 0 | 3 分 36 秒 |

兩個失敗分別是：

1. 並行測試重複呼叫 `EnsureCreatedAsync`，多個工作同時建立 `bookstore-db`
2. 搜尋測試讀到其他案例留下的資料

完成 Aspire 13.4、xUnit v3、readiness 與資料隔離修正後，連續執行結果如下：

| 執行 | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| Run 1 | 44 | 44 | 0 | 0 | 30.064 秒 |
| Run 2 | 44 | 44 | 0 | 0 | 26.342 秒 |
| repo 外 portability | 44 | 44 | 0 | 0 | 30.238 秒 |

build 結果是 0 warnings、0 errors。測試數量維持 44，沒有為了讓數字好看而刪除失敗案例。

上表是遷移當時在 Aspire 13.4.6、xUnit v3 3.2.2 量到的數字。2026-10-04 升到 Aspire 13.5.3 與 xUnit v3 4.0.0 後，在 Windows 重跑兩次都是 44／44，耗時 31.3 秒與 27.1 秒，build 仍是 0 warnings、0 errors。

## NuGet 套件稽核

更新套件後要檢查的不只是「能不能 restore」。本次執行：

```powershell
dotnet list Day24.AspireTesting.sln package --outdated
dotnet list Day24.AspireTesting.sln package --deprecated --include-transitive
dotnet list Day24.AspireTesting.sln package --vulnerable --include-transitive
```

2026-10-04 的結果：

- deprecated（包含 transitive）：0
- vulnerable（包含 transitive）：0
- 直接相依 outdated：4 個套件。Aspire 兩個套件有 13.6.0，`xunit.v3.mtp-v2` 有 4.0.1，`Microsoft.Testing.Extensions.TrxReport` 有 2.4.1

outdated 不是 0，這是刻意的。Day24 的版本跟 Day25 和系列其他天數對齊，沒有單獨追到最新。要升就整個系列一起升、一起重跑測試。

舊版 Aspire dependency graph 帶入 MessagePack 2.5.192，NuGet 會回報多個中度與高度弱點。升級到 Aspire 13 之後不再出現這些 vulnerable package 警告。

## Portability 驗證

per-day CPM 與 `global.json` 的目的，是讓範例不靠 repo 根目錄才能運作。本次把 `samples/day24` 複製到 repo 外的系統暫存目錄，排除既有 `bin`、`obj` 與 `TestResults`，再從零執行 restore、build、test。

結果仍是：

```text
Build: 0 warnings, 0 errors
Test:  44 passed, 0 failed, 0 skipped
```

這一關可以抓出最常見的範例封裝問題：忘了帶 CPM、意外吃到根目錄 `global.json`，或依賴先前 build 留下的 generated files。

## Aspire 13.4 的行為變更

Day24 沒有 HTTP project resource，因此 `CreateHttpClient` 的 HTTPS-first 變更不會直接影響這個範例。不過升級 Aspire Testing 時仍要知道：Aspire 13.4 在沒有指定 endpoint name 時，`CreateHttpClient` 與 `GetEndpointUriString` 會優先選 HTTPS。依賴舊有 HTTP-first 行為的測試，應明確傳入 endpoint name。

另一項變更是 PostgreSQL 預設 image 升到 18.3。Day24 使用 SQL Server，所以也不受影響；下一篇 Day25 使用 PostgreSQL，會另外實測。這也是為什麼升級不能只改 NuGet 版本，還要檢查範例實際使用的 resource integration。

## 常見問題

### Docker daemon 沒有啟動

症狀通常是找不到 named pipe 或無法連接 Docker API。先執行：

```powershell
docker version
```

確認 Server 資訊存在，再跑測試。

### SQL Server 顯示 Running 但連不上

不要直接增加 sleep 秒數。改看 resource health 與 container log，並使用有 timeout 的 `WaitForResourceHealthyAsync`。

### 測試單獨通過、整批執行失敗

先檢查資料是否跨案例殘留，以及測試是否共用同一個 DbContext。Day24 的做法是 container 共用、資料每個案例清除、DbContext 每次建立。

### 交易測試被 retrying execution strategy 拒絕

交易測試使用 non-retry DbContext。若正式程式需要 retry，使用 EF Core execution strategy 執行整個 transaction block，不要只在測試裡關掉錯誤。

## 附錄：Windows 上的 SQL Server 登入前交握逾時

這不是 Aspire Testing 的必要設定，也不會發生在每一台 Windows。如果 44 個測試全部停在 SQL Server 登入前交握，而容器本身顯示 Running，先檢查主機上的防毒軟體、EDR、VPN、廣告攔截器或其他網路防護工具。

```text
Microsoft.Data.SqlClient.SqlException : 已超過連接逾時的設定。
在嘗試使用登入前的信號交換確認時超過逾時等待的時間。
```

這類軟體可能會檢查 IPv4 loopback 流量。TCP 連線仍可建立，所以單純測試連接埠會得到「可連線」；SQL Server 的 TDS PRELOGIN 回應若被截斷，`sql_check` 還是會一直等到逾時。

本機確認的案例是 AdGuard v8。使用 WFP 驅動並開啟 `Filter localhost` 時，不論流量是否經過 Docker，`127.0.0.1` 都只收到 TDS 回應的前 9／26 bytes；主機端與 Docker 各測三次，結果一致。停止 AdGuard 服務後，兩組測試都恢復為 26／26。服務重新啟動並恢復過濾後，主機端又回到 9／26。

不必關掉整套防護。在 AdGuard 開啟 `Settings → App settings → Advanced settings`，關閉 `Filter localhost` 即可。AdGuard 服務與其他防護仍維持執行，只有 loopback 流量不再交給它檢查。

調整後的驗證結果：

| 測試 | 結果 |
| --- | --- |
| 主機端 `127.0.0.1` 模擬服務 | 完整 26 bytes，3／3 |
| Docker SQL Server `127.0.0.1` | 完整 26 bytes，3／3，1～3 ms |
| Docker SQL Server `localhost`／`::1` | 各 3／3 完整 |
| 本篇 Aspire Testing 範例 | 44／44 通過，34.9 秒 |

其他產品的設定名稱可能不同。遇到相同症狀時，可以找 localhost、loopback、network inspection 或 web protection 相關選項，先停用範圍最小的本機流量檢查，再重跑測試。若公司政策不允許變更，請交由管理防毒或網路防護軟體的人員建立 loopback 排除規則。

Testcontainers 可以用 `host.override=localhost` 暫時改走另一條 loopback 路徑，但 Aspire 的 SQL Server 連線字串使用 `127.0.0.1`，這個設定對 Aspire 無效。不要為了避開主機網路檢查而改寫 Aspire 的健康檢查或等待流程；那會讓範例偏離正式環境的行為。

參考與追蹤：[AdGuard 進階設定說明](https://adguard.com/kb/adguard-for-windows/settings/app-settings/advanced-settings/)、[AdGuard for Windows #6242](https://github.com/AdguardTeam/AdguardForWindows/issues/6242)、[Docker Desktop #622](https://github.com/docker/desktop-feedback/issues/622)、[Aspire #19803](https://github.com/microsoft/aspire/issues/19803)。

## 小結

Aspire Testing 讓測試與應用程式共用同一份 AppHost 拓撲，省下的是重複維護資源定義的成本。resource readiness、timeout、schema 初始化、資料隔離與 async disposal 仍要明確設計。

Day24 的完成狀態是：Aspire 13.5.3、xUnit v3 4.0.0 + MTP、44 個測試連續通過、package audit 無 deprecated／vulnerable，並通過 repo 外 portability。下一篇會把相同方法用到 PostgreSQL、Redis 與 Web API 的多服務整合測試。

## 參考資料

- [Upgrade to Aspire 13](https://learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13)
- [What's new in Aspire 13.4](https://aspire.dev/whats-new/aspire-13-4/)
- [Access resources in Aspire tests](https://aspire.dev/testing/accessing-resources/)
- [xUnit v2 to v3 migration](https://xunit.net/docs/getting-started/v3/migration)
- [.NET 10 `dotnet test`](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
- [EF Core testing](https://learn.microsoft.com/ef/core/testing/)
