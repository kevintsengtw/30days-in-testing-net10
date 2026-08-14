# Day21 - MSSQL + EF Core 以及 Dapper 基礎應用

本專案展示如何使用 Testcontainers 進行 SQL Server 資料庫測試，涵蓋 EF Core 和 Dapper 兩種資料存取技術的基礎 CRUD 操作測試。

## 專案結構

```
Day21.DatabaseTesting/
├── Day21.DatabaseTesting.sln
├── README.md
├── src/
│   └── DatabaseTesting.Core/        # 核心類別庫
│       ├── Models/                  # 實體模型
│       │   ├── Category.cs
│       │   ├── Product.cs
│       │   ├── ProductTag.cs
│       │   ├── Order.cs
│       │   └── OrderItem.cs
│       ├── Data/                    # 資料存取層
│       │   └── ECommerceDbContext.cs
│       └── GlobalUsings.cs
└── tests/
    └── DatabaseTesting.Tests/       # 測試專案
        ├── Infrastructure/          # 測試基礎設施
        │   └── SqlServerContainerFixture.cs
        ├── SqlScripts/              # 建表與預存程序 SQL 腳本（隨建置複製到輸出）
        ├── EfCoreCrudTests.cs       # EF Core CRUD 測試
        ├── EfCoreAdvancedTests.cs   # EF Core 進階功能測試
        ├── DapperCrudTests.cs       # Dapper CRUD 測試
        ├── DapperAdvancedTests.cs   # Dapper 進階功能測試
        └── GlobalUsings.cs
```

## 使用的套件版本

### 核心專案 (DatabaseTesting.Core)
- .NET 10 (net10.0)
- Microsoft.EntityFrameworkCore.SqlServer: 10.0.5
- Microsoft.EntityFrameworkCore.Design: 10.0.5
- Dapper: 2.1.72

### 測試專案 (DatabaseTesting.Tests)
- xunit.v3.mtp-v2: 3.2.2（xUnit v3，走 Microsoft.Testing.Platform）
- Microsoft.Testing.Extensions.TrxReport: 2.2.3
- AwesomeAssertions: 9.4.0
- Testcontainers.MsSql: 4.11.0

> 版本統一集中在 per-day 的 `Directory.Packages.props`（CPM）；測試專案 `.csproj` 需加 `<OutputType>Exe</OutputType>`。`Microsoft.EntityFrameworkCore.SqlServer` 10.0.5 與 `Dapper` 2.1.72 由測試專案引用；`Microsoft.Data.SqlClient` 6.1.5 隨 EF Core SqlServer 傳遞相依、不需顯式安裝。

## 執行方式

### 先決條件
- .NET 10 SDK
- Docker Desktop (用於執行 SQL Server 容器，需正在執行)

### 執行測試

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 `global.json` 指定。**務必先切換到本日 sample 目錄再執行**，否則不會套用 per-day `global.json`：

```powershell
# 先切換到本日 sample 目錄
Set-Location samples/day21

# 還原 / 建置 / 執行測試
dotnet restore Day21.DatabaseTesting.sln
dotnet build Day21.DatabaseTesting.sln -c Release
dotnet test --solution Day21.DatabaseTesting.sln -c Release
```

xUnit v3 走 Microsoft.Testing.Platform，篩選改用 `--filter-class`。在 .NET 10 的 MTP 模式下，參數直接接在 `dotnet test` 後面：

```bash
# 執行特定測試類別（實測 DapperCrudTests 5 個通過）
dotnet test --solution Day21.DatabaseTesting.sln --filter-class "DatabaseTesting.Tests.EfCoreCrudTests"
dotnet test --solution Day21.DatabaseTesting.sln --filter-class "DatabaseTesting.Tests.DapperCrudTests"
```

## 測試涵蓋

共 **24 個整合測試**，分佈於 4 個測試類別（數字取自 `day21.trx` 實際計數）：

| 測試類別 | 測試數 |
| --- | --- |
| `EfCoreCrudTests` | 5 |
| `EfCoreAdvancedTests` | 10 |
| `DapperCrudTests` | 5 |
| `DapperAdvancedTests` | 4 |
| **合計** | **24** |

## 重點學習內容

### 1. Collection Fixture 模式
- 使用 `ICollectionFixture<T>` 和 `[CollectionDefinition]` 實現容器共享
- 避免每個測試都啟動新容器，提升測試效能
- 正確的資源生命週期管理

### 2. EF Core 測試技巧
- DbContext 的正確設定和初始化
- 實體關聯的測試方法
- Query Filter 和 `IgnoreQueryFilters()` 的使用
- 批次操作 (`ExecuteUpdateAsync`, `ExecuteDeleteAsync`) 的測試

### 3. Dapper 測試技巧
- 原生 SQL 的執行和驗證
- 動態參數的使用
- 複雜聯結查詢的測試
- 與 EF Core 在同一容器環境中的協作

### 4. 測試設計原則
- 每個測試的獨立性 (透過 `Dispose` 清理資料)
- 3A 模式 (Arrange, Act, Assert) 的實踐
- 有意義的測試命名規範
- 適當的斷言使用

### 5. Testcontainers 實務應用
- SQL Server 容器的設定和管理
- 容器啟動等待策略
- 連線字串的動態取得
- 容器清理和資源回收

## 核心功能展示

### EF Core 功能
- ✅ 基本 CRUD 操作
- ✅ 關聯資料查詢 (Include, ThenInclude)
- ✅ 查詢篩選器測試
- ✅ 批次更新操作
- ✅ 軟刪除模式驗證

### Dapper 功能
- ✅ 原生 SQL CRUD 操作
- ✅ 動態參數查詢
- ✅ 複雜聯結查詢
- ✅ 批次資料操作
- ✅ 效能最佳化驗證

### 測試技術
- ✅ Collection Fixture 容器共享
- ✅ 自動化表格建立
- ✅ 測試資料隔離
- ✅ 關聯資料驗證
- ✅ 效能測試基礎

## 學習目標

通過本專案，您將學會：
1. 如何設計可靠的資料庫測試架構
2. EF Core 和 Dapper 的測試最佳實務
3. Testcontainers 的實際應用技巧
4. 測試資料管理和隔離策略
5. 資料庫操作的完整驗證方法

這個範例專案為資料庫測試提供了堅實的基礎，可以作為實際專案中資料存取層測試的參考模板。
