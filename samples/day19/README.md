# Day 19: ASP.NET Core Web API 整合測試

這是一個示範如何進行 ASP.NET Core Web API 整合測試的範例專案，使用 InMemory 資料庫進行基礎教學。

## 專案架構

```
Day19.Samples/
├── src/
│   └── Day19.WebApplication/     # 主要 Web API 專案
│       ├── Controllers/          # API 控制器
│       ├── Data/                 # 資料存取層 (DbContext)
│       ├── Entities/             # 實體類別
│       ├── Models/               # DTO 模型
│       └── Services/             # 服務層
└── tests/
    └── Day19.WebApplication.Integration.Tests/  # 整合測試專案
        ├── Controllers/          # 控制器測試
        ├── Integration/          # 進階整合測試
        ├── Infrastructure/       # 測試基礎架構
        └── Examples/             # 範例測試
```

## 技術特點

### 核心技術堆疊
- **.NET 10 / ASP.NET Core** - Web API 框架
- **Entity Framework Core 10.0.5** - ORM 框架（InMemory 提供者）
- **InMemory Database** - 測試用記憶體資料庫（基礎篇適用）
- **xunit.v3.mtp-v2 3.2.2** - 測試框架（xUnit v3，走 Microsoft.Testing.Platform）
- **AwesomeAssertions 9.4.0** - 流暢的斷言語法
- **AwesomeAssertions.Web 1.9.6** - HTTP 回應斷言
- **Microsoft.AspNetCore.Mvc.Testing 10.0.5** - 整合測試支援

### 整合測試架構設計
- **CustomWebApplicationFactory** - 自訂測試環境設定
- **IntegrationTestBase** - 測試基礎類別，提供共用功能
- **InMemory 資料庫設定** - 簡化測試環境設定

## API 端點規格

### 貨運商管理 API (`/api/shippers`)

| HTTP 方法 | 端點路由             | 功能描述             | 回應狀態碼                     |
| --------- | -------------------- | -------------------- | ------------------------------ |
| GET       | `/api/shippers`      | 取得所有貨運商清單   | 200 OK                         |
| GET       | `/api/shippers/{id}` | 取得指定 ID 的貨運商 | 200 OK / 404 Not Found         |
| POST      | `/api/shippers`      | 建立新的貨運商記錄   | 201 Created                    |
| PUT       | `/api/shippers/{id}` | 更新指定貨運商資料   | 200 OK / 404 Not Found         |
| DELETE    | `/api/shippers/{id}` | 刪除指定的貨運商     | 204 No Content / 404 Not Found |

## 完整測試案例覆蓋

### ShippersController 整合測試 (20 個測試案例)

#### 基本 CRUD 操作測試 (8 個)

- **GetShipper_當貨運商存在_應回傳成功結果** - 驗證正常取得貨運商資料
- **GetShipper_當貨運商不存在_應回傳404** - 驗證錯誤處理機制
- **CreateShipper_輸入有效資料_應建立成功** - 驗證資料建立流程
- **UpdateShipper_當貨運商存在_應更新成功** - 驗證資料更新功能
- **UpdateShipper_當貨運商不存在_應回傳404** - 驗證更新錯誤處理
- **DeleteShipper_當貨運商存在_應刪除成功** - 驗證資料刪除功能
- **DeleteShipper_當貨運商不存在_應回傳404** - 驗證刪除錯誤處理
- **GetAllShippers_應回傳所有貨運商** - 驗證列表查詢功能

#### 參數驗證測試 (6 個)

- **GetShipper_當ID為0_應回傳404** - 驗證 ID 不能為 0
- **GetShipper_當ID為負數_應回傳404** - 驗證 ID 不能為負數
- **CreateShipper_當公司名稱為空_應回傳400BadRequest** - 驗證必填欄位
- **CreateShipper_當公司名稱超過40字元_應回傳400BadRequest** - 驗證字串長度限制
- **CreateShipper_當電話號碼超過24字元_應回傳400BadRequest** - 驗證電話長度限制
- **CreateShipper_當請求內容格式不正確_應回傳400BadRequest** - 驗證 JSON 格式

#### 邊界值測試 (3 個)

- **CreateShipper_當公司名稱剛好40字元_應建立成功** - 驗證邊界值正常處理
- **CreateShipper_當電話號碼剛好24字元_應建立成功** - 驗證電話邊界值
- **GetShipper_當ID為最大整數值_應回傳404** - 驗證極值處理

#### 業務邏輯測試 (3 個)

- **UpdateShipper_更新後驗證資料確實變更** - 驗證更新操作的完整性
- **DeleteShipper_刪除後確認資料不存在** - 驗證刪除操作的完整性
- **CreateMultipleShippers_驗證ID自動遞增** - 驗證 ID 自動產生機制

**全專案總計：42 個測試案例，全部通過**

| 測試類別 | 測試數 |
| --- | --- |
| `ShippersController` 主要整合測試 | 20 |
| `Advanced` 進階測試（HTTP Headers／並行／效能） | 7 |
| Level 1 基礎 WebApi | 3 |
| Level 2 服務依賴 | 3 |
| Level 3 完整資料庫 | 9 |
| **合計** | **42** |

其中 `ShippersController` 的 20 個測試又分為：基本 CRUD 操作 8 個、參數驗證 6 個、邊界值 3 個、業務邏輯 3 個。

## 快速開始

### 環境需求
- .NET 10.0 或更高版本
- Visual Studio 2022 或 VS Code

### 建置與執行

xUnit v3 走 Microsoft Testing Platform（MTP），MTP runner 由本日 `global.json` 指定。**務必先切換到本日 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗：

```powershell
# 先切換到本日 sample 目錄
Set-Location samples/day19

# 還原 / 建置 / 執行測試
dotnet restore Day19.Samples.sln
dotnet build Day19.Samples.sln -c Release
dotnet test --solution Day19.Samples.sln -c Release
```

### 執行特定測試

xUnit v3 走 Microsoft.Testing.Platform，篩選參數改用 `--filter-class`／`--filter-method`。在 .NET 10 的 MTP 模式下，參數直接接在 `dotnet test` 後面：

```bash
# 執行特定類別的所有測試（實測 20 個通過）
dotnet test --solution Day19.Samples.sln --filter-class "Day19.WebApplication.Tests.Controllers.ShippersControllerTests"

# 執行單一測試方法
dotnet test --solution Day19.Samples.sln --filter-method "*GetShipper_當貨運商存在_應回傳成功結果"
```

## 核心設計模式

### 1. InMemory 資料庫設定 (基礎篇重點)
```csharp
// CustomWebApplicationFactory 中的設定
services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase(databaseName: "TestDb");
    options.EnableSensitiveDataLogging();
});
```

### 2. 測試隔離策略
```csharp
// 每個測試前清理資料庫狀態
await CleanupDatabaseAsync();

// 建立測試所需的種子資料
var shipperId = await SeedShipperAsync("測試公司", "02-1234-5678");
```

### 3. HTTP 整合測試模式
```csharp
// 使用真實的 HTTP 用戶端進行測試
var response = await Client.GetAsync($"/api/shippers/{shipperId}", TestContext.Current.CancellationToken);

// 使用 AwesomeAssertions.Web 驗證回應狀態與內容
response.Should().Be200Ok();
```

### 4. 完整請求生命週期測試
- 路由解析驗證
- 模型綁定測試
- 業務邏輯執行
- 資料庫操作確認
- 回應序列化檢查

## 測試架構優勢

### WebApplicationFactory 模式的好處
1. **真實環境模擬** - 使用完整的 ASP.NET Core 管道
2. **依賴注入支援** - 可以輕鬆替換測試用的服務
3. **中介軟體測試** - 包含完整的請求/回應處理流程
4. **設定靈活性** - 可自訂測試環境的各種設定

### InMemory 資料庫適用情境
- 單元測試和整合測試的快速執行
- 不需要外部資料庫依賴
- 測試資料的快速重置
- 適合 CI/CD 管道中的自動化測試

## 學習目標達成

這個基礎篇範例專案幫助您掌握：

- ✅ **整合測試基礎概念** - WebApplicationFactory 的使用方式
- ✅ **InMemory 資料庫應用** - 簡化測試環境設定
- ✅ **RESTful API 測試策略** - 完整 CRUD 操作的測試涵蓋
- ✅ **測試資料管理** - 種子資料建立與清理機制
- ✅ **錯誤情境驗證** - 404、驗證錯誤等例外狀況處理
- ✅ **HTTP 狀態碼驗證** - 正確的 API 回應格式確認

適合初學者建立整合測試的基礎知識和實作能力！
