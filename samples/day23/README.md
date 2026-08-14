# Day23 - 整合測試實戰：產品管理 WebAPI

## 專案簡介

本專案示範如何使用 ASP.NET Core 10 建立一個產品管理 WebAPI，並配備完整的整合測試基礎設施。

### 技術堆疊

- **API Framework**: ASP.NET Core 10 (Controllers)
- **Architecture**: Clean Architecture
- **Database**: PostgreSQL + Dapper
- **Cache**: Redis
- **Validation**: FluentValidation
- **Testing**: xUnit v3 (MTP) + Testcontainers + Flurl.Http + Respawn
- **Assertions**: AwesomeAssertions
- **Time Provider**: Microsoft.Bcl.TimeProvider

> 測試框架已遷移至 xUnit v3（走 Microsoft.Testing.Platform）：測試專案改用 `xunit.v3.mtp-v2` + `Microsoft.Testing.Extensions.TrxReport`，`.csproj` 加 `<OutputType>Exe</OutputType>`，版本統一集中在 per-day 的 `Directory.Packages.props`（CPM）。

### 專案結構

```
src/
├── Day23.Domain/           # 領域模型
├── Day23.Application/      # 應用服務層
├── Day23.Infrastructure/   # 基礎設施層
└── Day23.Api/             # Web API 層

tests/
└── Day23.Tests.Integration/ # 整合測試
```

### API 端點

#### 健康檢查
- `GET /health` → 200 + 系統狀態

#### 產品管理
- `POST /products` → 201 + ProductResponse
- `GET /products/{id}` → 200 + ProductResponse / 404
- `GET /products?keyword=&page=1&pageSize=20&sort=name&direction=asc` → 200 + PagedResult<ProductResponse>
- `PUT /products/{id}` → 204 / 404
- `DELETE /products/{id}` → 204 / 404

### 快速開始

#### 前置需求
- .NET 10 SDK
- Docker Desktop (用於整合測試的容器)

#### 建置與執行

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 `global.json` 指定。**務必先切換到本日 sample 目錄再執行**，否則不會套用 per-day `global.json`：

```powershell
# 先切換到本日 sample 目錄
Set-Location samples/day23

# 還原 / 建置
dotnet restore Day23.ProductApi.sln
dotnet build Day23.ProductApi.sln -c Release

# 執行整合測試（會自動啟動 PostgreSQL / Redis 測試容器）
dotnet test --solution Day23.ProductApi.sln -c Release
```

若要手動執行 API（需自備 PostgreSQL 與 Redis）：

```powershell
dotnet run --project src/Day23.Api
```

#### 環境變數

API 專案需要以下連線字串：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=day23_products;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  }
}
```

### 整合測試特色

- **Testcontainers**: 自動啟動 PostgreSQL 和 Redis 容器
- **Respawn**: 快速重置資料庫狀態
- **Flurl.Http**: 流暢的 HTTP 用戶端測試
- **Cache Testing**: 驗證 Cache-Aside 策略
- **Time Control**: 使用 FakeTimeProvider 控制時間

### 測試涵蓋

共 **32 個整合測試**，分佈於 4 個測試類別：

| 測試類別 | 測試數 |
| --- | --- |
| `ProductsControllerTests` | 19 |
| `ExceptionHandlerTests` | 8 |
| `HealthControllerTests` | 3 |
| `TimeProviderBehaviorTests` | 2 |
| **合計** | **32** |

### 開發注意事項

1. **時間處理**: 所有時間都使用 `TimeProvider.GetUtcNow()`，避免直接使用 `DateTime.Now`
2. **快取策略**: 實作 Cache-Aside 模式，更新/刪除時自動失效快取
3. **測試隔離**: 每個測試前後都會重置資料庫和 Redis
4. **錯誤處理**: 統一使用 ProblemDetails 格式回傳錯誤

### 學習重點

- ASP.NET Core 整合測試最佳實務
- Clean Architecture 在 .NET 中的實作
- PostgreSQL + Dapper 的資料存取模式
- Redis 快取策略與測試
- FluentValidation 輸入驗證
- Testcontainers 容器化測試

## 故障排除

### 常見問題

1. **Docker 未啟動**: 整合測試需要 Docker Desktop 運行
2. **埠號衝突**: 確保 PostgreSQL (5432) 和 Redis (6379) 埠號未被佔用
3. **權限問題**: Windows 上可能需要以管理員身分執行 Docker

### 除錯模式

```powershell
# 僅執行特定測試類別（xUnit v3 走 MTP）
dotnet test --solution Day23.ProductApi.sln --filter-class "Day23.Tests.Integration.Controllers.ProductsControllerTests"

# 執行單一命名空間下所有測試
dotnet test --solution Day23.ProductApi.sln --filter-namespace "Day23.Tests.Integration.Controllers"
```

---

> 本專案為「30天測試修練」系列 Day23 的範例，專注於整合測試實戰技巧。
