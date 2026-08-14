# Day22 - Testcontainers 整合測試：MongoDB 及 Redis 基礎應用

本專案示範如何使用 Testcontainers 進行 NoSQL 資料庫的整合測試，包含 MongoDB 和 Redis 的基礎操作。

## 專案描述

在現代軟體開發中，NoSQL 資料庫扮演越來越重要的角色。本專案展示如何在 .NET 環境中：

- 使用 Testcontainers 建立獨立的測試環境
- 實作 MongoDB 文件資料庫的 CRUD 操作
- 實作 Redis 快取系統的基礎功能
- 透過整合測試確保資料存取邏輯的正確性

## 專案結構

```
Day22.NoSqlTesting/
├── Day22.NoSqlTesting.sln
├── README.md
├── src/
│   └── Day22.Core/                     # 核心業務邏輯
│       ├── Configuration/              # 設定物件（MongoDbSettings、RedisSettings）
│       ├── Extensions/                 # DateTimeExtensions
│       ├── Infrastructure/             # 資料庫組態（MongoDbConfig、RedisConfig）
│       ├── Models/
│       │   ├── Mongo/                  # UserDocument、UserProfile、Address、GeoLocation、Skill、SkillLevel
│       │   └── Redis/                  # CacheItem、UserSession、RecentView、LeaderboardEntry、NotificationMessage
│       └── Services/                   # IUserService／MongoUserService、ICacheService／RedisCacheService
└── tests/
    └── Day22.Integration.Tests/        # 整合測試
        ├── Fixtures/                   # MongoDbContainerFixture、RedisContainerFixture、TestSettings
        ├── MongoDB/                    # MongoUserServiceTests、MongoBsonTests、MongoIndexTests
        ├── Redis/                      # RedisCacheServiceTests
        └── Extensions/                 # DateTimeExtensionsTests
```

## 使用套件版本

### 核心套件 (Day22.Core)
- .NET 10 (net10.0)
- MongoDB.Driver: 3.7.0
- StackExchange.Redis: 2.12.1
- System.Text.Json: 10.0.5
- Microsoft.Bcl.TimeProvider: 10.0.5

### 測試套件 (Day22.Integration.Tests)
- xunit.v3.mtp-v2: 3.2.2（xUnit v3，走 Microsoft.Testing.Platform）
- Microsoft.Testing.Extensions.TrxReport: 2.2.3
- AwesomeAssertions: 9.4.0
- Testcontainers: 4.11.0／Testcontainers.MongoDb: 4.11.0／Testcontainers.Redis: 4.11.0
- NSubstitute: 5.3.0
- Microsoft.Extensions.TimeProvider.Testing: 10.4.0

> 版本統一集中在 per-day 的 `Directory.Packages.props`（CPM）；測試專案 `.csproj` 需加 `<OutputType>Exe</OutputType>`。

## 執行方式

### 前置需求
- Docker Desktop 或 Docker Engine（需正在執行，供 MongoDB／Redis 容器使用）
- .NET 10 SDK

### 建置與執行測試

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 `global.json` 指定。**務必先切換到本日 sample 目錄再執行**，否則不會套用 per-day `global.json`：

```powershell
# 先切換到本日 sample 目錄
Set-Location samples/day22

# 還原 / 建置 / 執行測試
dotnet restore Day22.NoSqlTesting.sln
dotnet build Day22.NoSqlTesting.sln -c Release
dotnet test --solution Day22.NoSqlTesting.sln -c Release
```

xUnit v3 走 Microsoft.Testing.Platform，篩選改用 `--filter-class`。在 .NET 10 的 MTP 模式下，參數直接接在 `dotnet test` 後面：

```bash
# 僅執行 MongoDB 使用者服務測試
dotnet test --solution Day22.NoSqlTesting.sln --filter-class "Day22.Integration.Tests.MongoDB.MongoUserServiceTests"

# 僅執行 Redis 快取服務測試
dotnet test --solution Day22.NoSqlTesting.sln --filter-class "Day22.Integration.Tests.Redis.RedisCacheServiceTests"
```

## 重點學習內容

### 1. Testcontainers 容器管理
- 使用 `IAsyncLifetime` 介面管理容器生命週期
- Collection Fixture 模式確保測試間的獨立性
- 動態埠口綁定避免衝突

### 2. MongoDB 整合測試
- 文件資料庫的 CRUD 操作測試
- 查詢條件和篩選邏輯驗證
- 資料一致性檢查

### 3. Redis 整合測試
- 字串快取操作測試
- 物件序列化/反序列化驗證
- TTL（存活時間）功能測試

### 4. 測試最佳實務
- 3A 模式（Arrange-Act-Assert）
- 測試資料隔離
- 有意義的測試命名

## 範例功能展示

### MongoDB 功能
- ✅ 使用者 CRUD 操作
- ✅ 依條件查詢（年齡範圍、電子郵件）
- ✅ 統計功能（活躍使用者數量）

### Redis 功能
- ✅ 基礎快取操作（設定、取得、刪除）
- ✅ 物件快取（JSON 序列化）
- ✅ 過期時間管理
- ✅ 快取存在性檢查

## 測試覆蓋範圍

共 **53 個整合測試**，分佈於 5 個測試類別：

| 測試類別 | 測試數 |
| --- | --- |
| `MongoUserServiceTests` | 14 |
| `MongoBsonTests` | 4 |
| `MongoIndexTests` | 5 |
| `RedisCacheServiceTests` | 20 |
| `DateTimeExtensionsTests` | 10 |
| **合計** | **53** |

涵蓋 MongoDB 服務的公開方法、BSON 序列化與索引、Redis 五種資料結構、以及日期時間擴充方法。

## 實際應用情境

此專案的實作模式適用於：
- 電商系統的使用者資料管理
- 社群平台的內容快取
- IoT 應用的感測器資料儲存
- 微服務架構的資料層測試

---

透過本專案，你將學會如何建立可靠的 NoSQL 整合測試，確保應用程式在真實容器環境中的正確運作。
