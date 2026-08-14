---
day: 21
title: "Day 21 - Testcontainers 整合測試：MSSQL + EF Core 以及 Dapper 基礎應用"
sample: samples/day21
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Dapper
  - Microsoft.EntityFrameworkCore.Design
  - Microsoft.EntityFrameworkCore.SqlServer
  - Microsoft.Testing.Extensions.TrxReport
  - SSH.NET
  - Testcontainers.MsSql
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 21 - Testcontainers 整合測試：MSSQL + EF Core 以及 Dapper 基礎應用

<!-- toc -->

- [前言](#前言)
- [學習目標](#學習目標)
- [內容大綱](#內容大綱)
- [Day 20 的挑戰：單一容器的效能瓶頸](#day-20-的挑戰單一容器的效能瓶頸)
- [1. MSSQL 容器環境建置](#1-mssql-容器環境建置)
- [2. Repository Pattern 設計原則](#2-repository-pattern-設計原則)
- [3. SQL 指令碼外部化策略](#3-sql-指令碼外部化策略)
- [4. EF Core Repository 整合測試](#4-ef-core-repository-整合測試)
- [5. Dapper Repository 整合測試](#5-dapper-repository-整合測試)
- [6. 重點整理](#6-重點整理)
- [在本機執行測試（MTP）](#在本機執行測試mtp)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

昨天用 Testcontainers 建立了 Docker 測試環境。今天把同一個 MSSQL container 分別交給 EF Core 與 Dapper，觀察兩種資料存取方式需要驗證的重點。

專案選擇 EF Core 或 Dapper，通常取決於資料關聯、SQL 控制需求與效能考量。本篇讓兩者共用相同測試環境，差異就會落在 Repository 行為與查詢驗證上。

## 學習目標

- **建立穩定的 MSSQL Testcontainers 測試環境**：使用 Collection Fixture 模式
- **實作 Repository Pattern 與介面分離原則**：學習如何將基礎 CRUD 與進階功能分離
- **學習 EF Core 進階功能的測試策略**：Include、AsSplitQuery、ExecuteUpdate、N+1 查詢問題演示
- **掌握 Dapper 進階功能的測試實作**：QueryMultiple、DynamicParameters、預存程序呼叫
- **理解兩種不同資料存取技術的測試特點**：掌握各自的優勢與適用情境

## 內容大綱

- **從 Day 20 的挑戰談起：為何需要容器共享？**
  - Collection Fixture 模式的核心價值
- **MSSQL Testcontainers 環境建置**
  - 基本容器設定與設定
  - Collection Fixture 模式實作
  - 容器生命週期管理
  - SQL 腳本外部化策略

- **Repository Pattern 設計原則**
  - Interface Segregation Principle (ISP) 的應用
  - 基礎 CRUD 與進階功能的分離
  - 相依性注入與測試可控性

- **EF Core Repository 進階測試**
  - Include/ThenInclude 多層關聯查詢
  - AsSplitQuery 避免笛卡兒積
  - ExecuteUpdate/ExecuteDelete 批次操作
  - N+1 查詢問題演示與效能對比
  - AsNoTracking 唯讀查詢最佳化

- **Dapper Repository 進階測試**
  - QueryMultiple 一對多關聯處理
  - DynamicParameters 動態查詢建構
  - 預存程序呼叫與複雜業務邏輯
  - SQL 腳本外部化與維護策略

## Day 20 的挑戰：單一容器的效能瓶頸

在 Day 20 的範例中，我們為每一個測試類別都建立了一個新的 Testcontainer 容器。這種做法雖然確保了測試之間的完全隔離，但在大型專案中會遇到嚴重的效能瓶頸。

如果十幾個測試類別各自建立資料庫 container，每次執行都會重複以下流程：

1. **啟動容器**：建立一個新的 Docker 容器（例如 MSSQL）。
2. **等待就緒**：等待資料庫服務完全啟動並準備好接受連線。
3. **執行測試**：執行該測試類別中的所有測試方法。
4. **關閉並銷毀容器**：測試完成後，將容器關閉並移除。

MSSQL 的啟動成本不低。Day 20 的範例中，單次包含 MSSQL 的測試可能超過 10 秒；若每個類別都重付這筆成本，數十個測試很快就會拖慢本機回饋與 CI。

### 解決方案：使用 xUnit 的 Collection Fixture 模式

要避免重複啟動，可以讓多個測試類別共享同一個 container。xUnit 的 **Collection Fixture** 正好提供這個生命週期。

**Collection Fixture 的核心價值**：

1. **效能大幅提升**：
    - **傳統方式**：每個測試類別啟動一個容器。若有 3 個測試類別，總耗時約 `3 * 10 秒 = 30 秒`。
    - **Collection Fixture**：所有測試類別共享同一個容器。總耗時僅為容器啟動一次的時間，約 `1 * 10 秒 = 10 秒`。**在「3 個測試類別、每個容器啟動成本相同」的這個示例算式下，啟動耗時約減少 67%**。實際效益取決於測試類別數量、容器啟動成本與測試本身的執行時間，並非固定數字。

2. **資源使用最佳化**：
    - **記憶體節約**：只需維護一個 MSSQL 容器執行個體，而不是多個。
    - **Docker 資源**：降低 Docker daemon 的負擔，避免因資源競爭導致測試不穩定。

3. **測試環境一致性**：
    - **統一環境**：確保 EF Core 和 Dapper 的測試都在完全相同的資料庫容器中執行。
    - **資料隔離**：雖然共享容器，但每個測試結束後仍會清理資料（`IDisposable` 或類似機制），維持測試獨立性。

`Collection Fixture` 省下重複啟動時間，但資料隔離仍要自己處理。共享的是基礎設施，不是每個案例的狀態。

## 1. MSSQL 容器環境建置

### MSSQL 容器設定

MSSQL 在 .NET 專案中很常見。Testcontainers.MsSql 能啟動相同的資料庫引擎，但版本、設定與資料仍要由測試專案明確控制，不能直接宣稱和正式環境「完全一致」。

**為什麼選擇 MSSQL？**

- **企業環境普及率高**：大部分 .NET 專案都會用到
- **開發工具整合性佳**：與 Visual Studio 和 SSMS 整合密切
- **團隊熟悉度**：多數 .NET 工程師都有使用經驗
- **效能穩定**：成熟的查詢最佳化器和索引策略

### 專案設定與相依

建立新的測試專案並安裝必要的套件：

**MSSQL + EF Core + Dapper 必要套件**：

- **測試框架**：`xunit.v3.mtp-v2` (3.2.2)、`Microsoft.Testing.Extensions.TrxReport` (2.2.3)、`AwesomeAssertions` (9.4.0)
- **EF Core**：`Microsoft.EntityFrameworkCore.SqlServer` (10.0.5)
- **MSSQL 容器**：`Testcontainers.MsSql` (4.11.0)
- **Dapper**：`Dapper` (2.1.72)

比起 xUnit v2，這裡拿掉了 `Microsoft.NET.Test.Sdk`、`xunit`、`xunit.runner.visualstudio`，改由 `xunit.v3.mtp-v2` 一次涵蓋。xUnit v3 走 Microsoft.Testing.Platform（MTP），測試專案本身是可執行檔，`.csproj` 要加 `<OutputType>Exe</OutputType>`；`PackageReference` 只列名稱、不寫版本，版本統一集中在 per-day `Directory.Packages.props`（CPM）。

**關於 `Microsoft.Data.SqlClient`**：Dapper 使用 `SqlConnection` 連 MSSQL，因此會用到 `Microsoft.Data.SqlClient`，但**測試專案不需要顯式安裝**。它隨 `Microsoft.EntityFrameworkCore.SqlServer` 10.0.5 傳遞相依進來（實際解析版本為 6.1.5），在 `GlobalUsings.cs` 加上 `global using Microsoft.Data.SqlClient;` 即可使用。請選 `Microsoft.Data.SqlClient`，不要使用舊版的 `System.Data.SqlClient`。

### 測試資料準備

我們設計一個基本的電商資料模型，涵蓋常見的測試情境：

**核心實體與關聯**：

本文使用包含 `Product`、`Order` 等多個實體的電商資料模型。為求簡潔，此處僅展示 `Category` 實體作為範例。

```csharp
// 分類實體
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

// 完整實體定義請參考範例專案
```

**實體關聯設計**：

- **Category ↔ Product**：一對多關聯
- **Product ↔ ProductTag**：一對多關聯
- **Order ↔ OrderItem**：一對多關聯
- **Product ↔ OrderItem**：一對多關聯

這個設計涵蓋了常見的測試情境：CRUD 操作、關聯查詢、聚合統計等。

### DbContext 設定：核心實體關聯設定

展示 DbContext 的核心設定，重點在實體關聯和索引設定：

```csharp
public class ECommerceDbContext : DbContext
{
    public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options) : base(options) { }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductTag> ProductTags { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 僅展示 Product 實體的關鍵設定作為範例
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SKU).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.SKU).IsUnique();
            
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ... 其他實體的設定，請參考範例專案 ...
    }

    // ... SaveChangesAsync 攔截器 ...
}
```

### Collection Fixture 模式實作：容器共享

使用 Collection Fixture 模式可以讓多個測試類別共享同一個容器執行個體，提升測試效能。以下是範例專案中的實際實作：

```csharp
/// <summary>
/// MSSQL 容器的 Collection Fixture，用於在多個測試類別間共享同一個容器執行個體。
/// </summary>
public class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerContainerFixture()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                     .WithPassword("Test123456!")
                     .WithCleanUp(true)
                     .Build();
    }

    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// 初始化容器
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // Testcontainers MsSql module 內建等待策略：StartAsync 會等到 SQL Server
        // 真正可接受連線才返回，不需要再用固定 Task.Delay 猜測就緒時間
        // （這也呼應 Day20 建立的 Wait Strategy 原則：不要用固定 sleep 猜測服務是否就緒）。
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        Console.WriteLine($"SQL Server 容器已啟動，連線字串：{ConnectionString}");
    }

    /// <summary>
    /// 清理容器
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        Console.WriteLine("SQL Server 容器已清理");
    }
}

/// <summary>
/// 定義測試集合，讓多個測試類別可以共享同一個 SqlServerContainerFixture。
/// </summary>
[CollectionDefinition(nameof(SqlServerCollectionFixture))]
public class SqlServerCollectionFixture : ICollectionFixture<SqlServerContainerFixture>
{
    // 此類別只是用來定義 Collection，不需要實作內容
}
```

**重要特點**：

1. **容器設定最佳化**：使用 MSSQL 2022-latest 映像，設定強密碼和自動清理
2. **靜態連線字串**：確保所有測試類別都能存取同一個資料庫連線
3. **生命週期管理**：容器在測試集合開始時啟動，結束時自動清理
4. **執行效率**：多個測試類別共享同一容器，省下重複啟動容器的成本（前述示例約減少 67%，實際數字視測試類別數與容器啟動成本而定）

上面的 fixture 用的是 xUnit v3 的 `IAsyncLifetime`：`InitializeAsync`／`DisposeAsync` 的回傳型別從 v2 的 `Task` 改成 `ValueTask`（`DisposeAsync` 現在來自 `IAsyncDisposable`），方法體不用動；`ICollectionFixture`／`[CollectionDefinition]`／`[Collection]` 在 v3 同構，維持原樣。

#### xUnit1051：真實呼叫要傳 CancellationToken

xUnit v3 內建一條分析規則 xUnit1051：測試方法裡呼叫「有 `CancellationToken` 多載」的非同步方法卻沒傳 token，就會跳警告，要你改用 `TestContext.Current.CancellationToken`。整合測試幾乎全是真實呼叫——EF Core 的 `SaveChangesAsync`／`ToListAsync`／`FirstAsync` 等都算，本日樣本一共補了 37 處 token。

其中 `DbSet.FindAsync` 比較特別：它的第一個多載是 `params object?[]`，直接寫 `FindAsync(id, token)` 會把 token 當成另一個主鍵值，要改用陣列多載 `FindAsync([id], token)`。

### 測試類別設計：Collection Fixture 整合

範例專案讓每個測試類別用 Collection Fixture 共享同一個 MSSQL 容器：

```csharp
/// <summary>
/// EF Core 進階功能測試類別，展示 Repository Pattern 整合。
/// </summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class EfCoreAdvancedTests : IDisposable
{
    private readonly ECommerceDbContext _dbContext;
    private readonly IProductByEFCoreRepository _advancedRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public EfCoreAdvancedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        var connectionString = SqlServerContainerFixture.ConnectionString;
        _testOutputHelper.WriteLine($"使用連線字串：{connectionString}");

        // 建立 EF Core DbContext
        var options = new DbContextOptionsBuilder<ECommerceDbContext>()
                      .UseSqlServer(connectionString)
                      .EnableSensitiveDataLogging()
                      .LogTo(_testOutputHelper.WriteLine, LogLevel.Information)
                      .Options;

        _dbContext = new ECommerceDbContext(options);

        // 使用 SQL 腳本建立表格，而不是 EnsureCreated()
        EnsureTablesExist();

        // 注入 EF Core 的進階 Repository 實作
        _advancedRepository = new EfCoreProductRepository(_dbContext);
    }

    public void Dispose()
    {
        // 按照外鍵約束順序清理資料，確保測試隔離
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM ProductTags");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM OrderItems");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Orders");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Products");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Categories");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
        _dbContext.Dispose();
    }
    
    // 測試方法將在後續章節展示...
}
```

**重點特色**：

1. **Repository Pattern 整合**：直接注入並測試 `IProductByEFCoreRepository`
2. **資料隔離機制**：在 `Dispose` 方法清理每個測試留下的資料
3. **外鍵約束處理**：按照正確順序執行 DELETE 語句避免約束錯誤
4. **日誌整合**：將 EF Core SQL 日誌輸出到測試結果，便於除錯

## 2. Repository Pattern 設計原則

先看本專案的 Repository Pattern 設計。它遵循 **Interface Segregation Principle (ISP)**，把不同職責的資料存取操作拆到不同介面。

### 2.1 介面分離原則的應用

我們的 Repository 設計分為三個層次的介面：

```csharp
/// <summary>
/// 定義產品相關的基礎 CRUD 資料存取操作
/// </summary>
public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}

/// <summary>
/// 定義 EF Core 特有的進階資料存取操作
/// </summary>
public interface IProductByEFCoreRepository
{
    // EF Core 特有功能：Include、AsSplitQuery、ExecuteUpdate、AsNoTracking 等
    Task<Product?> GetProductWithCategoryAndTagsAsync(int productId);
    Task<IEnumerable<Product>> GetProductsByCategoryWithSplitQueryAsync(int categoryId);
    Task<int> BatchUpdateProductPricesAsync(int categoryId, decimal priceMultiplier);
    Task<int> BatchDeleteInactiveProductsAsync(int categoryId);
    Task<IEnumerable<Product>> GetProductsWithNoTrackingAsync(decimal minPrice);
    
    // N+1 查詢問題驗證：提供有問題和已最佳化的不同實作
    Task<IEnumerable<Category>> GetCategoriesWithN1ProblemAsync();      // 示範錯誤做法（會產生 N+1 查詢）
    Task<IEnumerable<Category>> GetCategoriesWithProductsOptimizedAsync(); // 正確做法（使用 Include 最佳化）
    Task<IEnumerable<Product>> GetProductsByCategoryIdAsync(int categoryId); // 輔助方法
}

/// <summary>
/// 定義 Dapper 特有的進階資料存取操作
/// </summary>
public interface IProductByDapperRepository
{
    // Dapper 特有功能：QueryMultiple、DynamicParameters、預存程序呼叫等
    Task<Product?> GetProductWithTagsAsync(int productId);
    Task<IEnumerable<Product>> SearchProductsAsync(int? categoryId = null, decimal? minPrice = null, bool? isActive = null);
    Task<IEnumerable<ProductSalesReport>> GetProductSalesReportAsync(decimal minPrice);
}
```

### 2.2 為什麼要分離基礎 CRUD 與進階功能？

**1. 單一職責原則 (SRP)**：

- `IProductRepository` 專注於基本的資料存取操作
- `IProductByEFCoreRepository` 專注於 EF Core 特有的進階功能
- `IProductByDapperRepository` 專注於 Dapper 特有的進階功能

**2. 介面隔離原則 (ISP)**：

- 使用基礎 CRUD 的程式碼不需要依賴進階功能的介面
- 不同技術堆疊的進階功能不會互相污染
- 測試時可以更精確地模擬所需的行為

**3. 相依反轉原則 (DIP)**：

- 高層模組（業務邏輯）相依於抽象（介面）而非具體實作
- 可以輕易切換不同的資料存取技術
- 提升程式碼的可測試性和可維護性

### 2.3 測試策略的優勢

這種設計帶來以下測試優勢：

**1. 測試隔離性**：

- 基礎 CRUD 測試與進階功能測試分離
- EF Core 和 Dapper 的進階測試互不影響
- 可以針對特定功能進行精準測試

**2. Mock 的精確性**：

- 可以只模擬實際需要的介面
- 減少不必要的 Mock 設定
- 提升測試的可讀性和維護性

**3. 技術特性驗證**：

- EF Core 測試專注於 LINQ、Change Tracking、Query Optimization
- Dapper 測試專注於 SQL 控制、效能、動態查詢
- 每種技術的特色都能得到充分驗證

## 3. SQL 指令碼外部化策略

動手寫測試前，先講一個策略：**SQL 指令碼外部化**。把大量 SQL（例如建表指令）直接塞進 C# 程式碼，檔案會越長越腫，也不好維護。改成獨立的 `.sql` 檔，測試執行時再讀進來。

### 3.1 為什麼需要外部化 SQL 指令碼？

**優點**：

- **關注點分離 (SoC)**：C# 程式碼專注於測試邏輯，SQL 指令碼專注於資料庫結構。
- **可維護性**：修改資料庫結構時，只需編輯 `.sql` 檔案，不需重新編譯程式碼。
- **可讀性**：C# 程式碼變得更簡潔，更容易閱讀。
- **工具支援**：SQL 檔案可以獲得編輯器的語法高亮和格式化支援。
- **版本控制友善**：SQL 變更可以清楚地在版本控制系統中追蹤。

### 3.2 實作步驟

#### 步驟 1：建立 SQL 腳本資料夾結構

在測試專案中建立以下資料夾結構：

```text
tests/DatabaseTesting.Tests/
├── SqlScripts/
│   ├── Tables/
│   │   ├── CreateCategoriesTable.sql
│   │   ├── CreateTagsTable.sql
│   │   ├── CreateCustomersTable.sql
│   │   ├── CreateProductsTable.sql
│   │   ├── CreateOrdersTable.sql
│   │   ├── CreateOrderItemsTable.sql
│   │   └── CreateProductTagsTable.sql
│   └── StoredProcedures/
│       └── GetProductSalesReport.sql
```

#### 步驟 2：設定 .csproj 檔案

將 `.sql` 檔案設定為在建置時複製到輸出目錄：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- ... 其他設定 ... -->
  
  <ItemGroup>
    <!-- Tables -->
    <Content Include="SqlScripts\Tables\CreateCategoriesTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateTagsTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateCustomersTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateProductsTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateOrdersTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateOrderItemsTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="SqlScripts\Tables\CreateProductTagsTable.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    
    <!-- Stored Procedures -->
    <Content Include="SqlScripts\StoredProcedures\GetProductSalesReport.sql">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

#### 步驟 3：實作腳本載入邏輯（EnsureTablesExist 方法）

建立一個可重用的方法來按照相依順序載入 SQL 腳本。這個方法需要加到測試類別中，以下是 EF Core 版本的實作：

```csharp
/// <summary>
/// 確保資料表存在，使用外部 SQL 腳本建立
/// </summary>
    private void EnsureTablesExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            return;
        }

        // 按照依賴順序執行表格建立腳本
        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateCustomersTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var script = File.ReadAllText(fullPath);
            _dbContext.Database.ExecuteSqlRaw(script);
        }
    }
```

**重要注意事項**：

- **相依順序很重要**：必須先建立主表（如 Categories），再建立有外鍵約束的表（如 Products）
- **使用 `AppContext.BaseDirectory`**：確保在不同的執行環境下都能正確找到 SQL 檔案
- **錯誤處理**：檢查檔案和目錄是否存在，避免執行時錯誤
- **使用方式**：在每個測試類別的建構式中呼叫 `EnsureTablesExist()` 方法

## 4. EF Core Repository 整合測試

現在我們來看 EF Core 的實作。接下來會展示如何對使用 EF Core 實作的 Repository 進行整合測試，包含基礎 CRUD 操作和 EF Core 特有的進階功能。

### 4.1 基礎 CRUD 操作測試類別設定

測試類別 `EfCoreCrudTests` 的設定與之前類似，但關鍵的區別在於，我們現在注入並測試 `IProductRepository` 的實作，而不是直接操作 `DbContext`。

```csharp
[Collection(nameof(SqlServerCollectionFixture))]
public class EfCoreCrudTests : IDisposable
{
    private readonly ECommerceDbContext _dbContext;
    private readonly IProductRepository _productRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public EfCoreCrudTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        var connectionString = SqlServerContainerFixture.ConnectionString;
        _testOutputHelper.WriteLine($"使用連線字串：{connectionString}");

        // 建立 EF Core DbContext
        var options = new DbContextOptionsBuilder<ECommerceDbContext>()
                      .UseSqlServer(connectionString)
                      .EnableSensitiveDataLogging()
                      .LogTo(_testOutputHelper.WriteLine, LogLevel.Information)
                      .Options;

        _dbContext = new ECommerceDbContext(options);
        _productRepository = new EfCoreProductRepository(_dbContext);

        // 使用 SQL 腳本建立表格，而不是 EnsureCreated()
        EnsureTablesExist();
    }

    /// <summary>
    /// 確保資料表存在，若不存在則執行 SQL 腳本建立
    /// </summary>
    private void EnsureTablesExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            return;
        }

        // 按照相依順序執行表格建立腳本
        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateCustomersTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var script = File.ReadAllText(fullPath);
            _dbContext.Database.ExecuteSqlRaw(script);
        }
    }

    /// <summary>
    /// 清理測試資料
    /// </summary>
    public void Dispose()
    {
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM ProductTags");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM OrderItems");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Orders");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Products");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Categories");
        _dbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

### 4.2 基礎 CRUD 操作測試

以下測試案例完整驗證 `EfCoreProductRepository` 中基本的 Create、Read、Update、Delete 操作：

```csharp
[Fact]
public async Task AddAsync_使用EfCoreRepository新增商品_應該成功儲存()
{
    // Arrange
    await SeedCategoryAsync();
    var category = await _dbContext.Categories.FirstAsync(TestContext.Current.CancellationToken);
    var product = new Product
    {
        Name = "EF Core Repo 測試商品",
        Description = "這是一個測試商品",
        Price = 1500,
        Stock = 25,
        CategoryId = category.Id,
        SKU = "EFCORE-REPO-001",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    // Act
    await _productRepository.AddAsync(product);

    // Assert
    product.Id.Should().BeGreaterThan(0);
    var saved = await _dbContext.Products.FindAsync([product.Id], TestContext.Current.CancellationToken);
    saved.Should().NotBeNull();
    saved.Name.Should().Be("EF Core Repo 測試商品");
}

[Fact]
public async Task GetAllAsync_使用EfCoreRepository查詢所有商品_應回傳所有商品()
{
    // Arrange
    await SeedCategoryAsync();
    var category = await _dbContext.Categories.FirstAsync(TestContext.Current.CancellationToken);
    await _productRepository.AddAsync(new Product
    {
        Name = "商品1", Price = 100, CategoryId = category.Id, SKU = "SKU1", IsActive = true, CreatedAt = DateTime.UtcNow
    });
    await _productRepository.AddAsync(new Product
    {
        Name = "商品2", Price = 200, CategoryId = category.Id, SKU = "SKU2", IsActive = true, CreatedAt = DateTime.UtcNow
    });

    // Act
    var products = await _productRepository.GetAllAsync();

    // Assert
    products.Should().HaveCount(2);
}

[Fact]
public async Task GetByIdAsync_使用EfCoreRepository查詢單一商品_應回傳正確商品()
{
    // Arrange
    await SeedCategoryAsync();
    var category = await _dbContext.Categories.FirstAsync(TestContext.Current.CancellationToken);
    var newProduct = new Product
    {
        Name = "查詢用商品",
        Price = 150,
        CategoryId = category.Id,
        SKU = "SKU3",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    await _productRepository.AddAsync(newProduct);

    // Act
    var product = await _productRepository.GetByIdAsync(newProduct.Id);

    // Assert
    product.Should().NotBeNull();
    product!.Id.Should().Be(newProduct.Id);
    product.Name.Should().Be("查詢用商品");
}

[Fact]
public async Task UpdateAsync_使用EfCoreRepository更新商品_應成功更新()
{
    // Arrange
    await SeedCategoryAsync();
    var category = await _dbContext.Categories.FirstAsync(TestContext.Current.CancellationToken);
    var productToUpdate = new Product
    {
        Name = "待更新商品",
        Price = 300,
        CategoryId = category.Id,
        SKU = "SKU4",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    await _productRepository.AddAsync(productToUpdate);

    _dbContext.ChangeTracker.Clear(); // 清除追蹤，模擬從不同上下文中取得並更新

    var product = await _productRepository.GetByIdAsync(productToUpdate.Id);
    product!.Name = "已更新商品";
    product.Price = 350;

    // Act
    await _productRepository.UpdateAsync(product);

    // Assert
    var updatedProduct = await _dbContext.Products.FindAsync([productToUpdate.Id], TestContext.Current.CancellationToken);
    updatedProduct.Should().NotBeNull();
    updatedProduct!.Name.Should().Be("已更新商品");
    updatedProduct.Price.Should().Be(350);
}

[Fact]
public async Task DeleteAsync_使用EfCoreRepository刪除商品_應成功刪除()
{
    // Arrange
    await SeedCategoryAsync();
    var category = await _dbContext.Categories.FirstAsync(TestContext.Current.CancellationToken);
    var productToDelete = new Product
    {
        Name = "待刪除商品",
        Price = 400,
        CategoryId = category.Id,
        SKU = "SKU5",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    await _productRepository.AddAsync(productToDelete);

    // Act
    await _productRepository.DeleteAsync(productToDelete.Id);

    // Assert
    var deletedProduct = await _productRepository.GetByIdAsync(productToDelete.Id);
    deletedProduct.Should().BeNull();
}

/// <summary>
/// 預先建立一個分類，供商品測試使用
/// </summary>
    private async Task SeedCategoryAsync()
    {
        if (!await _dbContext.Categories.AnyAsync())
        {
            _dbContext.Categories.Add(new Category
            {
                Name = "電子產品",
                Description = "各種電子設備",
                IsActive = true
            });
            await _dbContext.SaveChangesAsync();
        }
    }
```

### 4.3 EF Core 進階功能測試

EF Core 的強項在於其強型別的 LINQ 查詢、變更追蹤機制，以及各種查詢最佳化功能。以下我們將測試這些進階功能：

```csharp
/// <summary>
/// EF Core 進階功能的整合測試
/// </summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class EfCoreAdvancedTests : IDisposable
{
    private readonly ECommerceDbContext _dbContext;
    private readonly IProductByEFCoreRepository _advancedRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public EfCoreAdvancedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        var connectionString = SqlServerContainerFixture.ConnectionString;
        _testOutputHelper.WriteLine($"使用連線字串：{connectionString}");

        // 建立 EF Core DbContext
        var options = new DbContextOptionsBuilder<ECommerceDbContext>()
                      .UseSqlServer(connectionString)
                      .EnableSensitiveDataLogging()
                      .LogTo(_testOutputHelper.WriteLine, LogLevel.Information)
                      .Options;

        _dbContext = new ECommerceDbContext(options);

        // 使用 SQL 腳本建立表格，而不是 EnsureCreated()
        EnsureTablesExist();

        // 注入 EF Core 的進階 Repository 實作
        _advancedRepository = new EfCoreProductRepository(_dbContext);
    }

    // Dispose 和 EnsureTablesExist 方法同基礎測試
}
```

#### Include/ThenInclude 多層關聯查詢

```csharp
    [Fact]
    public async Task GetProductWithCategoryAndTagsAsync_載入完整關聯資料_應該正確載入所有相關資料()
    {
        // Arrange
        var category = new Category { Name = "進階測試分類" };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var tag1 = new Tag { Name = "標籤1" };
        var tag2 = new Tag { Name = "標籤2" };
        _dbContext.Tags.Add(tag1);
        _dbContext.Tags.Add(tag2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var product = new Product
        {
            Name = "測試產品",
            Price = 1000,
            CategoryId = category.Id,
            SKU = "ADV-TEST-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var productTag1 = new ProductTag { ProductId = product.Id, TagId = tag1.Id };
        var productTag2 = new ProductTag { ProductId = product.Id, TagId = tag2.Id };
        _dbContext.ProductTags.AddRange(productTag1, productTag2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _advancedRepository.GetProductWithCategoryAndTagsAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Category.Should().NotBeNull();
        result.Category.Name.Should().Be("進階測試分類");
        result.ProductTags.Should().HaveCount(2);
        result.ProductTags.Should().AllSatisfy(pt => pt.Tag.Should().NotBeNull());
    }
```

#### 使用分割查詢避免笛卡兒積 (`AsSplitQuery`)

當一個查詢中包含多個一對多 `Include` 時，可能會導致效能問題，這就是所謂的「笛卡兒積爆炸 (Cartesian Explosion)」。EF Core 提供了 `AsSplitQuery()` 方法來將一個 LINQ 查詢分解成多個 SQL 查詢，以避免這個問題。

```csharp
    [Fact]
    public async Task GetProductsByCategoryWithSplitQueryAsync_使用分割查詢_應該避免笛卡兒積問題()
    {
        // Arrange
        var category = new Category { Name = "分割查詢分類" };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 建立多個產品和標籤來模擬複雜關聯
        for (var i = 1; i <= 3; i++)
        {
            var product = new Product
            {
                Name = $"分割查詢產品{i}",
                Price = 100 * i,
                CategoryId = category.Id,
                SKU = $"SPLIT-{i:000}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // 每個產品加入多個標籤
            for (var j = 1; j <= 2; j++)
            {
                var tag = new Tag { Name = $"標籤{i}-{j}" };
                _dbContext.Tags.Add(tag);
                await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

                var productTag = new ProductTag { ProductId = product.Id, TagId = tag.Id };
                _dbContext.ProductTags.Add(productTag);
            }
        }

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _advancedRepository.GetProductsByCategoryWithSplitQueryAsync(category.Id);

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(p =>
        {
            p.Category.Should().NotBeNull();
            p.ProductTags.Should().HaveCount(2);
            p.ProductTags.Should().AllSatisfy(pt => pt.Tag.Should().NotBeNull());
        });
    }
```

> **什麼是笛卡兒積 (Cartesian Explosion)？**
>
> 在資料庫查詢中，如果用 `JOIN` 一次載入一個主實體及多個關聯集合（例如，一個 `Product` 同時 `Include` 它的 `Tags` 和 `Reviews`），資料庫會為每一種組合產生一列資料。一個產品若有 10 個標籤和 5 則評論，查詢結果會回傳 `1 * 10 * 5 = 50` 列，即使最後只需要一個產品。重複資料越多，資料庫與應用程式之間的傳輸成本就越高。
>
> `AsSplitQuery()` 的作用是將一個 LINQ 查詢分解成多個獨立的 SQL 查詢。EF Core 會先查詢主實體，然後為每個 `Include` 的關聯集合產生一個額外的查詢，最後在記憶體中將這些結果組合起來。這樣就避免了單一查詢中的笛卡兒積問題，大幅提升了複雜關聯查詢的效率。

#### ExecuteUpdate 批次操作

```csharp
    [Fact]
    public async Task BatchApplyDiscountAsync_ExecuteUpdate批次更新關聯資料_應該高效更新()
    {
        // Arrange
        var category = new Category { Name = "特價商品", IsActive = true, CreatedAt = DateTime.UtcNow };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var products = new[]
        {
            new Product { Name = "商品A", Price = 1000, CategoryId = category.Id, SKU = "SALE001", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Product { Name = "商品B", Price = 2000, CategoryId = category.Id, SKU = "SALE002", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Product { Name = "商品C", Price = 3000, CategoryId = category.Id, SKU = "SALE003", IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Products.AddRange(products);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - 批次調整特定分類下所有商品的價格（8折）
        var discountPercentage = 0.8m;
        var affectedRows = await _advancedRepository.BatchApplyDiscountAsync(category.Id, discountPercentage);

        // Assert
        affectedRows.Should().Be(3);

        // 清除 DbContext 快取以確保從資料庫讀取最新資料
        _dbContext.ChangeTracker.Clear();

        var updatedProducts = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == category.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        updatedProducts.Should().HaveCount(3);
        updatedProducts[0].Price.Should().Be(800m);  // 1000 * 0.8
        updatedProducts[1].Price.Should().Be(1600m); // 2000 * 0.8
        updatedProducts[2].Price.Should().Be(2400m); // 3000 * 0.8
    }
```

#### N+1 查詢問題的驗證與最佳化測試

**測試目標**：驗證 Repository 實作是否正確解決了 N+1 查詢問題

**測試情境說明**：

1. **驗證問題存在**：測試 `GetCategoriesWithN1ProblemAsync()` 方法會產生 N+1 查詢
2. **驗證最佳化效果**：測試 `GetCategoriesWithProductsOptimizedAsync()` 方法只產生少量查詢  
3. **效能對比**：展示兩種 Repository 實作策略的查詢效能差異

**Repository 實作說明**：

在實際的 `EfCoreProductRepository` 類別中，這兩個方法的實作差異如下：

```csharp
/// <summary>
/// 取得所有分類及其產品資料（會產生 N+1 查詢問題的錯誤做法）。
/// 此方法故意不使用 Include，導致每個分類都會額外查詢一次產品資料。
/// </summary>
public async Task<IEnumerable<Category>> GetCategoriesWithN1ProblemAsync()
{
    var categories = await context.Categories.ToListAsync();

    // 故意觸發 N+1 查詢：每個分類都會產生一次額外的資料庫查詢
    foreach (var category in categories)
    {
        // 存取 Products 屬性會觸發 lazy loading 或額外查詢
        _ = category.Products.Count();
    }

    return categories;
}

/// <summary>
/// 取得所有分類及其產品資料（正確做法，使用 Include 最佳化）。
/// 使用 Include 預載入產品資料，避免 N+1 查詢問題。
/// </summary>
public async Task<IEnumerable<Category>> GetCategoriesWithProductsOptimizedAsync()
{
    return await context.Categories
                        .Include(c => c.Products)
                        .ToListAsync();
}
```

以下測試方法的重點是**驗證 Repository 方法的實作正確性**，而不是在測試程式碼中示範問題。

```csharp
[Fact]
public async Task N1QueryProblemVerification_對比有問題與最佳化的Repository方法_應該展示查詢效率差異()
{
    // Arrange - 建立測試資料
    await CreateCategoriesWithProductsAsync();
    var stopwatch = new Stopwatch();

    // Act 1: 測試有問題的方法
    stopwatch.Start();
    var categoriesWithProblem = await _advancedRepository.GetCategoriesWithN1ProblemAsync();
    stopwatch.Stop();
    var problemTime = stopwatch.ElapsedMilliseconds;

    // Act 2: 測試最佳化方法
    stopwatch.Restart();
    var categoriesOptimized = await _advancedRepository.GetCategoriesWithProductsOptimizedAsync();
    stopwatch.Stop();
    var optimizedTime = stopwatch.ElapsedMilliseconds;

    // Assert - 驗證結果正確性和效能差異
    categoriesWithProblem.Should().HaveCount(3, "有問題的方法也要回傳正確的資料數量");
    categoriesOptimized.Should().HaveCount(3, "最佳化方法要回傳正確的資料數量");
    
    // 最佳化方法包含完整的關聯資料
    foreach (var category in categoriesOptimized)
    {
        category.Products.Should().NotBeEmpty("最佳化方法應該預載入產品資料");
    }

    // 記錄效能差異
    _testOutputHelper.WriteLine($"有問題的方法: {problemTime}ms");
    _testOutputHelper.WriteLine($"最佳化方法: {optimizedTime}ms");
}
```

執行的輸出內容（擷取關鍵部分）

```text
有問題的方法: 487ms
最佳化方法: 165ms
```

**什麼是 N+1 查詢問題？**

> N+1 是 ORM 常見的效能陷阱：先查詢一份主實體清單（1 次查詢），再於迴圈中逐筆查詢關聯資料（N 次查詢），總共會產生 1+N 次資料庫往返。
>
> **錯誤做法**：Repository 方法不使用 Include，導致在迴圈中產生額外查詢
> **正確做法**：Repository 方法使用 `Include()` 或 `ThenInclude()` 在一次查詢中預載入所有需要的關聯資料
>
> 這個問題在有大量關聯資料時會嚴重影響效能，是整合測試中必須驗證的重要情境。

#### AsNoTracking 唯讀查詢最佳化

```csharp
[Fact]
public async Task GetProductsWithNoTrackingAsync_唯讀查詢_應該提升效能並減少記憶體使用()
{
    // Arrange
    await CreateMultipleProductsAsync();
    var minPrice = 500m;

    // Act
    var products = await _advancedRepository.GetProductsWithNoTrackingAsync(minPrice);

    // Assert
    products.Should().NotBeEmpty();
    products.All(p => p.Price >= minPrice).Should().BeTrue();
    
    // 驗證這些實體不被 ChangeTracker 追蹤
    var trackedEntities = _dbContext.ChangeTracker.Entries<Product>().Count();
    trackedEntities.Should().Be(0, "AsNoTracking 查詢不應該追蹤實體");
    
    _testOutputHelper.WriteLine($"查詢到 {products.Count()} 個產品，無追蹤狀態");
}
```

## 5. Dapper Repository 整合測試

Dapper 是輕量級 Micro-ORM，SQL 由開發者直接撰寫。這一節測試 `DapperProductRepository`，重點是手寫 SQL、參數與資料庫回傳結果是否符合預期。

### 5.1 Dapper 環境設定

我們延續第 3 章介紹的 SQL 指令碼外部化策略，在 Dapper 測試中採用相同的 SqlScripts 資料夾結構和 .csproj 設定。

唯一的差異在於 `EnsureDatabaseObjectsExist()` 方法的實作方式，因為 Dapper 使用 `IDbConnection.Execute()` 而非 EF Core 的 `ExecuteSqlRaw()`。

#### Dapper 版本的腳本載入實作

在測試專案中建立以下資料夾結構：

```csharp
/// <summary>
/// Dapper 版本的腳本載入方法，使用 IDbConnection.Execute() 執行 SQL
/// </summary>
    private void EnsureDatabaseObjectsExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            return;
        }

        // 按照依賴順序執行表格建立腳本
        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateCustomersTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var script = File.ReadAllText(fullPath);
            _connection.Execute(script);
        }

        // 建立預存程序
        var storedProceduresDirectory = Path.Combine(scriptDirectory, "StoredProcedures");
        if (Directory.Exists(storedProceduresDirectory))
        {
            var spScriptFiles = Directory.GetFiles(storedProceduresDirectory, "*.sql");
            foreach (var scriptFile in spScriptFiles)
            {
                var script = File.ReadAllText(scriptFile);
                _connection.Execute(script);
            }
        }
    }
```

### 5.2 Dapper 基本 CRUD 整合測試

前一節已整理 SQL 指令碼外部化策略，接著把它套用到 Dapper Repository 測試。`DapperProductRepository` 實作 `IProductRepository`，並自行撰寫 CRUD 所需的 SQL。

以下是完整的 `DapperCrudTests` 測試類別實作：

```csharp
/// <summary>
/// Dapper Repository CRUD 操作測試
/// </summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class DapperCrudTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly IProductRepository _productRepository;
    private readonly ITestOutputHelper _testOutputHelper;

    public DapperCrudTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var connectionString = SqlServerContainerFixture.ConnectionString;
        _connection = new SqlConnection(connectionString);
        _connection.Open();

        _productRepository = new DapperProductRepository(connectionString);

        // 確保測試資料表存在 (使用第 3 章介紹的 SQL 指令碼外部化策略)
        EnsureTablesExist();
    }

    public void Dispose()
    {
        // 清理測試資料
        _connection.Execute("DELETE FROM ProductTags");
        _connection.Execute("DELETE FROM OrderItems");
        _connection.Execute("DELETE FROM Orders");
        _connection.Execute("DELETE FROM Products");
        _connection.Execute("DELETE FROM Categories");
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 實作第 3 章介紹的 SQL 指令碼外部化策略。
    /// 按照相依順序載入所有必要的資料表腳本。
    /// </summary>
    private void EnsureTablesExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            return;
        }

        // 按照相依順序執行表格建立腳本（參考第 3 章的實作）
        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateCustomersTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var script = File.ReadAllText(fullPath);
            _connection.Execute(script);
        }

        // 建立測試分類
        var categoryExists = _connection.QuerySingle<int>("SELECT COUNT(*) FROM Categories");
        if (categoryExists == 0)
        {
            _connection.Execute("""
                                INSERT INTO Categories (Name, Description, IsActive) 
                                VALUES ('電子產品', '各種電子設備', 1), ('書籍', '各類書籍', 1)
                                """);
        }
    }

    [Fact]
    public async Task AddAsync_使用DapperRepository新增商品_應該成功儲存()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");
        var product = new Product
        {
            Name = "Dapper Repository 測試商品",
            Description = "Dapper Repo 測試用",
            Price = 2500m,
            Stock = 15,
            CategoryId = categoryId,
            SKU = "DAPPER-REPO-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await _productRepository.AddAsync(product);

        // Assert
        product.Id.Should().BeGreaterThan(0);
        var savedProduct = await _productRepository.GetByIdAsync(product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct.Name.Should().Be(product.Name);
    }

    [Fact]
    public async Task GetAllAsync_使用DapperRepository查詢所有商品_應該回傳所有商品()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");
        await _productRepository.AddAsync(new Product
        {
            Name = "商品1", Price = 100m, CategoryId = categoryId, SKU = "SKU1", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await _productRepository.AddAsync(new Product
        {
            Name = "商品2", Price = 200m, CategoryId = categoryId, SKU = "SKU2", IsActive = true, CreatedAt = DateTime.UtcNow
        });

        // Act
        var products = await _productRepository.GetAllAsync();

        // Assert
        products.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_使用DapperRepository查詢單一商品_應該回傳正確商品()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");
        var newProduct = new Product
        {
            Name = "查詢用商品", 
            Price = 150m, 
            CategoryId = categoryId, 
            SKU = "SKU3", 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow
        };
        await _productRepository.AddAsync(newProduct);

        // Act
        var product = await _productRepository.GetByIdAsync(newProduct.Id);

        // Assert
        product.Should().NotBeNull();
        product!.Id.Should().Be(newProduct.Id);
        product.Name.Should().Be("查詢用商品");
    }

    [Fact]
    public async Task UpdateAsync_使用DapperRepository更新商品_應該成功更新()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");
        var productToUpdate = new Product
        {
            Name = "待更新商品",
            Price = 300m,
            CategoryId = categoryId, 
            SKU = "SKU4", 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow
        };
        await _productRepository.AddAsync(productToUpdate);

        var product = await _productRepository.GetByIdAsync(productToUpdate.Id);
        product!.Name = "已更新商品";
        product.Price = 350m;

        // Act
        await _productRepository.UpdateAsync(product);

        // Assert
        var updatedProduct = await _productRepository.GetByIdAsync(productToUpdate.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct.Name.Should().Be("已更新商品");
        updatedProduct.Price.Should().Be(350m);
    }

    [Fact]
    public async Task DeleteAsync_使用DapperRepository刪除商品_應該成功刪除()
    {
        // Arrange
        var categoryId = await _connection.QuerySingleAsync<int>("SELECT TOP 1 Id FROM Categories WHERE IsActive = 1");
        var productToDelete = new Product
        {
            Name = "待刪除商品",
            Price = 400m,
            CategoryId = categoryId,
            SKU = "SKU5",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _productRepository.AddAsync(productToDelete);

        // Act
        await _productRepository.DeleteAsync(productToDelete.Id);

        // Assert
        var deletedProduct = await _productRepository.GetByIdAsync(productToDelete.Id);
        deletedProduct.Should().BeNull();
    }
}
```

### 5.3 Dapper 進階功能整合測試

Dapper 讓開發者直接控制 SQL，複雜查詢與效能調整也因此更明確。範例專案以 `IProductByDapperRepository` 集中這些進階資料存取功能。

```csharp
/// <summary>
/// Dapper 進階功能的整合測試。
/// </summary>
[Collection(nameof(SqlServerCollectionFixture))]
public class DapperAdvancedTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly IProductByDapperRepository _advancedRepository;
    private readonly IProductRepository _basicRepository;
    private readonly string _connectionString;
    private readonly ITestOutputHelper _testOutputHelper;

    public DapperAdvancedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _connectionString = SqlServerContainerFixture.ConnectionString;
        _connection = new SqlConnection(_connectionString);
        _connection.Open();

        // 注入 Dapper 的 Repository 實作
        _advancedRepository = new DapperProductRepository(_connectionString);
        _basicRepository = new DapperProductRepository(_connectionString);

        // 確保測試資料庫物件存在
        EnsureDatabaseObjectsExist();
    }

    /// <summary>
    /// 清理測試資料庫中的資料，確保每次測試後資料庫狀態一致。
    /// </summary>
    public void Dispose()
    {
        _connection.Execute("DELETE FROM ProductTags");
        _connection.Execute("DELETE FROM OrderItems");
        _connection.Execute("DELETE FROM Orders");
        _connection.Execute("DELETE FROM Products");
        _connection.Execute("DELETE FROM Categories");
        _connection.Execute("DELETE FROM Tags");
        _connection.Execute("DELETE FROM Customers");
        _connection.Dispose();
    }

    /// <summary>
    /// 確保資料庫中的必要物件（表格、預存程序等）存在。
    /// </summary>
    private void EnsureDatabaseObjectsExist()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        if (!Directory.Exists(scriptDirectory))
        {
            return;
        }

        // 按照相依順序執行表格建立腳本
        var orderedScripts = new[]
        {
            "Tables/CreateCategoriesTable.sql",
            "Tables/CreateTagsTable.sql",
            "Tables/CreateCustomersTable.sql",
            "Tables/CreateProductsTable.sql",
            "Tables/CreateOrdersTable.sql",
            "Tables/CreateOrderItemsTable.sql",
            "Tables/CreateProductTagsTable.sql"
        };

        foreach (var scriptPath in orderedScripts)
        {
            var fullPath = Path.Combine(scriptDirectory, scriptPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var script = File.ReadAllText(fullPath);
            _connection.Execute(script);
        }

        // 建立預存程序
        var storedProceduresDirectory = Path.Combine(scriptDirectory, "StoredProcedures");
        if (Directory.Exists(storedProceduresDirectory))
        {
            var spScriptFiles = Directory.GetFiles(storedProceduresDirectory, "*.sql");
            foreach (var scriptFile in spScriptFiles)
            {
                var script = File.ReadAllText(scriptFile);
                _connection.Execute(script);
            }
        }
    }
}
```

#### 使用 `QueryMultiple` 處理一對多關聯

當需要從資料庫載入一個主物件及其關聯的多個集合時（例如，一個產品和它的所有標籤），如果使用傳統的 `JOIN`，會產生與 EF Core 相同的笛卡兒積問題。

在 Dapper 中，最佳的解決方案是使用 `QueryMultiple`。這個功能允許我們在一次資料庫往返中執行多個 SELECT 查詢，然後在程式碼中將結果集手動組合起來。

```csharp
[Fact]
public async Task GetProductWithTagsAsync_使用QueryMultiple_應該正確組合資料()
{
    // Arrange
    var categoryId = await CreateTestCategoryAsync("QueryMultiple 分類");
    var product = await CreateAndAddTestProductAsync("多查詢商品", "MULTI-001", 100, categoryId, true);

    // 使用 Dapper 建立 Tag 和關聯
    var tagId1 = await CreateTestTagAsync("標籤A");
    var tagId2 = await CreateTestTagAsync("標籤B");
    await LinkProductAndTagAsync(product.Id, tagId1);
    await LinkProductAndTagAsync(product.Id, tagId2);

    // Act
    var result = await _advancedRepository.GetProductWithTagsAsync(product.Id);

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(product.Id);
    result.Name.Should().Be("多查詢商品");
    result.ProductTags.Should().HaveCount(2);
    result.ProductTags.Should().AllSatisfy(pt => pt.Tag.Should().NotBeNull());
    result.ProductTags.Select(pt => pt.Tag.Name).Should().Contain(new[] { "標籤A", "標籤B" });
}
```

這種做法要自己寫 SQL、自己組物件，換來的是不傳多餘資料，查詢效能也完全掌握在你手上。

#### 使用 `DynamicParameters` 處理動態查詢

查詢條件會在執行期改變時，可以用 Dapper 的 `DynamicParameters` 建立參數化 SQL，避免把值直接拼進查詢字串。

```csharp
[Fact]
public async Task SearchProductsAsync_使用動態條件查詢_應該返回符合條件的商品()
{
    // Arrange
    var categoryId = await CreateTestCategoryAsync("動態查詢分類");
    await CreateAndAddTestProductAsync("動態商品A", "DYN-A", 800, categoryId, true);
    await CreateAndAddTestProductAsync("動態商品B", "DYN-B", 1200, categoryId, true);
    await CreateAndAddTestProductAsync("動態商品C", "DYN-C", 1500, categoryId, false);

    // Act - 測試多重條件查詢
    var results = await _advancedRepository.SearchProductsAsync(
        categoryId: categoryId,
        minPrice: 1000,
        isActive: true
    );

    // Assert
    results.Should().HaveCount(1);
    var product = results.First();
    product.Name.Should().Be("動態商品B");
    product.Price.Should().Be(1200);
    product.IsActive.Should().BeTrue();
    product.CategoryId.Should().Be(categoryId);
}

[Fact]
public async Task SearchProductsAsync_使用部分條件_應該返回符合條件的商品()
{
    // Arrange
    var categoryId = await CreateTestCategoryAsync("部分條件分類");
    await CreateAndAddTestProductAsync("部分條件商品A", "PARTIAL-A", 500, categoryId, true);
    await CreateAndAddTestProductAsync("部分條件商品B", "PARTIAL-B", 1500, categoryId, true);

    // Act - 只使用價格條件
    var results = await _advancedRepository.SearchProductsAsync(minPrice: 1000);

    // Assert
    results.Should().HaveCount(1);
    results.First().Name.Should().Be("部分條件商品B");
}
```

這些測試證明了不同輸入參數都能安全地組出 SQL 並執行。搜尋條件會變的功能幾乎都用得到。

#### 呼叫預存程序進行複雜業務邏輯

Dapper 也能輕易地呼叫資料庫預存程序，這對於處理複雜的報表查詢或業務邏輯特別有用。

```csharp
[Fact]
public async Task GetProductSalesReportAsync_呼叫預存程序_應該返回正確的銷售報表()
{
    // Arrange
    var categoryId = await CreateTestCategoryAsync("銷售報表分類");
    var customerId = await CreateTestCustomerAsync("測試客戶");

    // 建立產品
    var product1 = await CreateAndAddTestProductAsync("高價商品", "SALES-HIGH", 1500, categoryId, true);
    var product2 = await CreateAndAddTestProductAsync("低價商品", "SALES-LOW", 500, categoryId, true);

    // 建立訂單和訂單項目
    var orderId = await CreateTestOrderAsync(customerId);
    await CreateTestOrderItemAsync(orderId, product1.Id, 2, 1500); // 數量 2, 單價 1500
    await CreateTestOrderItemAsync(orderId, product2.Id, 5, 500);  // 數量 5, 單價 500

    // Act
    var report = await _advancedRepository.GetProductSalesReportAsync(1000m);

    // Assert
    report.Should().NotBeEmpty();
    var highPriceProductReport = report.FirstOrDefault(r => r.Name == "高價商品");
    highPriceProductReport.Should().NotBeNull();
    highPriceProductReport!.TotalQuantity.Should().Be(2);
    highPriceProductReport.TotalRevenue.Should().Be(3000m);
}
```

### 5.4 Dapper 測試輔助方法

為了讓 Dapper 的測試程式碼更簡潔且可重用，我們實作了一系列輔助方法來準備測試資料：

```csharp
/// <summary>
/// 建立測試用的分類，並回傳其 Id。
/// </summary>
    private async Task<int> CreateTestCategoryAsync(string name)
    {
        var sql = """
                  INSERT INTO Categories (
                      Name,
                      IsActive,
                      CreatedAt
                  )
                  OUTPUT INSERTED.Id
                  VALUES (
                      @Name,
                      1,
                      GETUTCDATE()
                  )
                  """;
        return await _connection.QuerySingleAsync<int>(sql, new { Name = name });
    }

/// <summary>
/// 建立並新增測試用的商品，回傳該商品實體。
/// </summary>
    private async Task<Product> CreateAndAddTestProductAsync(string name, string sku, decimal price, int categoryId, bool isActive)
    {
        var product = new Product
        {
            Name = name,
            Price = price,
            CategoryId = categoryId,
            SKU = sku,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        await _basicRepository.AddAsync(product);
        return product;
    }

/// <summary>
/// 建立測試用的標籤，並回傳其 Id。
/// </summary>
    private async Task<int> CreateTestTagAsync(string name)
    {
        var sql = """
                  INSERT INTO Tags (
                      Name,
                      IsActive,
                      CreatedAt
                  )
                  OUTPUT INSERTED.Id
                  VALUES (
                      @Name,
                      1,
                      GETUTCDATE()
                  )
                  """;
        return await _connection.QuerySingleAsync<int>(sql, new { Name = name });
    }

/// <summary>
/// 建立產品與標籤的關聯。
/// </summary>
    private async Task LinkProductAndTagAsync(int productId, int tagId)
    {
        var sql = """
                  INSERT INTO ProductTags (
                      ProductId,
                      TagId
                  )
                  VALUES (
                      @ProductId,
                      @TagId
                  )
                  """;
        await _connection.ExecuteAsync(sql, new { ProductId = productId, TagId = tagId });
    }
```

## 6. 重點整理

本篇用同一個 MSSQL container 驗證 EF Core 與 Dapper 的 Repository 實作。兩者共用基礎設施，但測試焦點不同。

### 核心學習要點

1. **容器共享是效能關鍵**：
    - 用 xUnit 的 `Collection Fixture` 模式共享單一 MSSQL 容器執行個體。
    - 這避免了為每個測試類別重複啟動和銷毀容器，大幅提升了整合測試的執行效率，是真實世界專案的標準實踐。

2. **Repository Pattern 提升了測試的抽象層次**：
    - 定義 `IProductRepository` 介面，將測試程式碼與具體的資料存取技術（EF Core 或 Dapper）解耦。
    - 測試目標是確認 Repository 是否符合業務契約，不是重新驗證 ORM 本身。

3. **SQL 指令碼外部化是企業級實踐**：
    - 將 SQL DDL 指令碼從 C# 程式碼抽離成 `.sql` 檔案，再由 `.csproj` 自動複製到輸出目錄。
    - 這種作法提升了程式碼的可維護性、可讀性，並支援複雜的資料庫結構管理，是企業級專案的標準作法。
    - `EnsureDatabaseObjectsExist()` 會按照相依順序載入 SQL 腳本，避免外鍵約束錯誤。

4. **EF Core 與 Dapper 的測試策略差異**：
    - **EF Core 測試**：重點在於驗證 LINQ 查詢、實體關聯設定（`Include`）、以及效能最佳化（`AsSplitQuery`, `ExecuteUpdateAsync`）是否能正確地轉換為預期的資料庫操作。
    - **Dapper 測試**：重點在於驗證手寫的 SQL 語句是否語法正確、能處理各種參數，並返回預期的結果。測試也涵蓋了 Dapper 的進階功能，如 `QueryMultiple` 和動態查詢的建構。

5. **測試環境的一致性與隔離**：
    - Testcontainers 提供了一個與正式環境一致、但完全隔離的拋棄式資料庫環境。
    - 即使共享容器，`Dispose` 仍會清理資料表，維持各測試案例的獨立性。

### 實務應用建議

在實際專案中應用今天學到的技術時，建議遵循以下最佳實務：

1. **專案結構規劃**：
   - 在測試專案中建立 `SqlScripts` 目錄，按照功能分類管理 SQL 檔案（Tables、StoredProcedures、Views 等）
   - 使用 Collection Fixture 模式共享容器執行個體，減少測試執行時間

2. **程式碼組織策略**：
   - 為不同的資料存取技術建立獨立的測試類別（如 `EfCoreTests`、`DapperTests`）
   - 實作共用的測試輔助方法，如 `EnsureDatabaseObjectsExist()` 和資料清理邏輯

3. **測試範圍設計**：
   - Repository 層級的整合測試應該專注於驗證業務邏輯的正確性，而非 ORM 框架本身的功能
   - 涵蓋關鍵的 CRUD 操作、複雜查詢、以及錯誤處理情境

這套做法的邊界很清楚：Testcontainers 管基礎設施，Repository Pattern 定義要驗證的契約，外部 SQL 指令碼則保留資料庫物件的可讀性。換用其他資料存取技術時，仍可沿用相同分工。

## 在本機執行測試（MTP）

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 sample 的 `global.json` 指定。**務必先切換到該 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗；也不要從 repository root 直接指定子目錄 solution：

```powershell
Set-Location samples/day21
dotnet test --solution Day21.DatabaseTesting.sln -c Release
```

## 參考資料

**Testcontainers 相關資源**：

- [Testcontainers 官方網站](https://testcontainers.com/)
- [Testcontainers for .NET 入門指南](https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/)
- [Testcontainers GitHub 官方組織](https://github.com/testcontainers)
- [Testcontainers for .NET 官方文件](https://dotnet.testcontainers.org/)
- [Testcontainers for .NET GitHub Repository](https://github.com/testcontainers/testcontainers-dotnet)
- [Testcontainers NuGet Package](https://www.nuget.org/packages/Testcontainers/)
- [Code Maze: C# Testing Using Testcontainers](https://code-maze.com/csharp-testing-using-testcontainers-for-net-and-docker/)

**測試模式與架構**：

- [xUnit.net: Sharing context between tests](https://xunit.net/docs/shared-context)

**資料存取技術**：

- [Entity Framework Core 官方文件](https://learn.microsoft.com/en-us/ef/core/)
- [Dapper 官方文件](https://dapper-tutorial.net/)

**專案組態管理**：

- [MSBuild: Content Items](https://learn.microsoft.com/zh-tw/visualstudio/msbuild/common-msbuild-project-items#content)
- [Managing SQL Scripts in .NET Projects](https://learn.microsoft.com/zh-tw/dotnet/core/tools/dotnet-build#options)

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day21>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十一天。明天會介紹 Day 22 - Testcontainers 整合測試：MongoDB 及 Redis 基礎應用。**
