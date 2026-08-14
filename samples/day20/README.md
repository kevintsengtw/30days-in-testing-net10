# Day20 整合測試專案說明

## 專案結構

```text
Day20/
├── src/
│   └── Day20.Core/                           # 核心業務邏輯類別庫
│       ├── Models/                           # 資料模型
│       │   ├── User.cs                       # 使用者實體
│       │   └── UserRequests.cs               # 使用者請求模型 (UserCreateRequest, UserUpdateRequest)
│       ├── Data/                            # 資料存取層
│       │   └── UserDbContext.cs             # Entity Framework DbContext
│       └── Services/                        # 服務層
│           ├── IUserService.cs              # 使用者服務介面
│           ├── IExternalApiService.cs       # 外部 API 服務介面
│           ├── SqlUserService.cs            # SQL 使用者服務
│           ├── CacheService.cs              # 快取服務
│           └── Implementations/             # 服務實作
│               └── ExternalApiService.cs    # 外部 API 服務實作
└── tests/
    └── Day20.Core.Integration.Tests/         # 整合測試專案
        └── Integration/                      # 整合測試
            ├── PostgreSqlIntegrationTests.cs   # PostgreSQL 基本整合測試
            ├── SqlServerIntegrationTests.cs    # SQL Server 基本整合測試
            ├── UserServicePostgreSqlTests.cs   # PostgreSQL UserService 測試
            ├── UserServiceSqlServerTests.cs    # SQL Server UserService 測試
            ├── RedisIntegrationTests.cs        # Redis 整合測試
            └── WireMockIntegrationTests.cs     # WireMock API 模擬測試
```

## 技術堆疊

### 核心依賴

- **.NET 10**: 目標框架 net10.0
- **Entity Framework Core 10.0.5**: ORM 框架，支援 PostgreSQL 和 SQL Server
- **StackExchange.Redis 2.12.1**: Redis 用戶端
- **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1**: PostgreSQL 連接器

### Testcontainers 4.11.0

- **Testcontainers.PostgreSql 4.11.0**: PostgreSQL 容器支援
- **Testcontainers.MsSql 4.11.0**: SQL Server 容器支援
- **Testcontainers.Redis 4.11.0**: Redis 容器支援
- **WireMock.Net.Testcontainers 2.0.0**: WireMock 容器支援

### 測試框架

- **xunit.v3.mtp-v2 3.2.2**: 測試框架（xUnit v3，走 Microsoft.Testing.Platform）
- **AwesomeAssertions 9.4.0**: 流暢的斷言庫

## 測試涵蓋

6 個測試類別，共 **37 個整合測試**（全部通過）：

| 測試類別 | 測試數 | 說明 |
| --- | --- | --- |
| `PostgreSqlIntegrationTests` | 2 | PostgreSQL 基本整合測試 |
| `RedisIntegrationTests` | 7 | Redis 資料結構操作測試 |
| `SqlServerIntegrationTests` | 3 | SQL Server 基本整合測試（含交易回滾） |
| `UserServicePostgreSqlTests` | 9 | PostgreSQL UserService 測試 |
| `UserServiceSqlServerTests` | 4 | SQL Server UserService 測試 |
| `WireMockIntegrationTests` | 12 | WireMock 外部 API 模擬測試 |
| **合計** | **37** | |

## 主要特色

### 1. 多資料庫支援

- **PostgreSQL**: 開源關聯式資料庫
- **SQL Server**: Microsoft 關聯式資料庫
- **Redis**: 記憶體快取資料庫

### 2. Testcontainers 整合測試

- 真實資料庫環境測試，避免 InMemory 的限制
- 自動容器生命週期管理
- 隔離的測試環境
- 支援平行測試執行

### 3. API 模擬測試

- **WireMock**: 外部 API 模擬和測試
- 支援複雜的 HTTP 請求/回應模擬
- 測試外部服務整合情境

### 4. 統一服務介面

```csharp
public interface IUserService
{
    Task<User> CreateUserAsync(UserCreateRequest request);
    Task<User?> GetUserByIdAsync(string id);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> UpdateUserAsync(string id, UserUpdateRequest request);
    Task<bool> DeleteUserAsync(string id);
    Task<bool> IsValidUserAsync(User user);
}
```

### 5. 外部 API 服務整合

```csharp
public interface IExternalApiService
{
    Task<bool> ValidateEmailAsync(string email);
    Task<string> GetLocationAsync(string address);
}
```

## 執行測試

### 前置條件

1. **Docker Desktop**: 用於運行 Testcontainers（需正在執行）
2. **WSL2** (Windows): Docker 後端要求
3. **.NET 10 SDK**: 編譯和執行專案

> 可先執行專案根目錄的 `verify-environment.ps1` 確認 Docker 與 .NET 環境就緒後再跑測試。

### 建置與執行測試

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 `global.json` 指定。**務必先切換到本日 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗：

```powershell
# 先切換到本日 sample 目錄
Set-Location samples/day20

# 還原 / 建置 / 執行測試
dotnet restore Day20.Samples.sln
dotnet build Day20.Samples.sln -c Release
dotnet test --solution Day20.Samples.sln -c Release
```

### 執行特定測試類別

xUnit v3 走 Microsoft.Testing.Platform，篩選改用 `--filter-class`。在 .NET 10 的 MTP 模式下，參數直接接在 `dotnet test` 後面：

```bash
# PostgreSQL 基本整合測試（實測 2 個通過）
dotnet test --solution Day20.Samples.sln --filter-class "Day20.Core.Integration.Tests.Integration.PostgreSqlIntegrationTests"

# UserService PostgreSQL 測試
dotnet test --solution Day20.Samples.sln --filter-class "Day20.Core.Integration.Tests.Integration.UserServicePostgreSqlTests"

# WireMock API 模擬測試
dotnet test --solution Day20.Samples.sln --filter-class "Day20.Core.Integration.Tests.Integration.WireMockIntegrationTests"
```

## 測試範例

### PostgreSQL 整合測試

```csharp
[Fact]
public async Task CreateUserAsync_輸入有效使用者資料_應成功建立使用者()
{
    // Arrange
    var request = new UserCreateRequest
    {
        Username = "testuser_postgres",
        FullName = "Test User_postgres",
        Email = "test_postgres@example.com",
        Age = 25
    };

    // Act
    var result = await _userService.CreateUserAsync(request);

    // Assert
    result.Should().NotBeNull();
    result.Username.Should().Be(request.Username);
    result.Email.Should().Be(request.Email);
    result.Id.Should().NotBeNullOrEmpty();
}
```

### WireMock API 模擬測試

```csharp
[Fact]
public async Task ValidateEmailAsync_使用有效電子郵件_應回傳True()
{
    // Arrange
    var email = "test@example.com";
    var mappingJson = """
        {
          "request": {
            "method": "GET",
            "urlPath": "/api/email/validate",
            "queryParameters": {
              "email": { "equalTo": "test@example.com" }
            }
          },
          "response": {
            "status": 200,
            "headers": { "Content-Type": "application/json" },
            "body": "{\"IsValid\": true, \"Message\": \"Email is valid\"}"
          }
        }
        """;

    // 設定 WireMock mapping
    var adminUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}/__admin/mappings";
    var content = new StringContent(mappingJson, Encoding.UTF8, "application/json");
    await _httpClient.PostAsync(adminUrl, content);

    // Act
    var result = await _externalApiService.ValidateEmailAsync(email);

    // Assert
    result.Should().BeTrue();
}
```

## 設計優勢

### 1. **真實環境測試**

- 使用真實資料庫而非 InMemory 模擬
- 能測試資料庫特定功能和約束
- 發現實際部署時可能遇到的問題

### 2. **容器化隔離**

- 每個測試都有獨立的資料庫實例
- 避免測試間的相互影響
- 自動清理測試資源

### 3. **多資料庫兼容性**

- 統一的服務介面支援多種資料庫
- 便於在不同環境間切換
- 比較不同資料庫的效能特性

### 4. **效能基準測試**

- 比較 Testcontainers vs InMemory 的效能差異
- 測試不同資料庫的效能特性
- 建立效能基準線

### 5. **開發者友善**

- 豐富的測試輔助工具
- 清晰的錯誤訊息和斷言
- 易於擴展的架構設計

## 注意事項

1. **Docker 要求**: 確保 Docker Desktop 正在運行
2. **網路連線**: 首次執行會下載 Docker 映像
3. **效能影響**: Testcontainers 比 InMemory 測試慢，但提供更真實的測試環境
4. **資源消耗**: 容器測試會消耗更多 CPU 和記憶體資源
5. **並行限制**: 過多的並行容器測試可能會造成資源競爭

這個專案展示了如何使用 Testcontainers 建立強大的整合測試，平衡了測試真實性和執行效率。
