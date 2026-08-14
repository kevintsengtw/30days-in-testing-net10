# Day 25 - .NET Aspire 整合測試實戰

這個範例延續 Day25 原本的主題：把 PostgreSQL、Redis 與 Web API 放進同一個 Aspire AppHost，再用 Aspire Testing 執行真正的多服務整合測試。Day23 的 Testcontainers 範例只用來比較容器管理方式，沒有修改或搬移 Day23。

## 專案結構

```text
day25/
├── Day25.AspireIntegration.sln
├── Directory.Packages.props
├── global.json
├── Day25.AppHost/
├── src/
│   ├── Day25.Domain/
│   ├── Day25.Application/
│   ├── Day25.Infrastructure/
│   └── Day25.Api/
└── tests/
    └── Day25.Tests.Integration/
        ├── Controllers/
        ├── Infrastructure/
        └── VerifyAspireContainers.cs
```

## 版本

| 套件／工具 | 版本 |
| --- | --- |
| .NET SDK | 10.0.300，`latestFeature` |
| Aspire AppHost、Hosting、Testing、client integrations | 13.4.6 |
| xUnit MTP v2 | 3.2.2 |
| Microsoft Testing Platform TRX | 2.3.2 |
| PostgreSQL image | 18.3（Aspire 13.4 預設值，測試有實際驗證） |
| Redis image | 8.6（Aspire 13.4 預設值，測試期間由 Docker 實際核對） |
| Npgsql | 10.0.3 |
| StackExchange.Redis | 3.0.17 |
| Dapper | 2.1.79 |
| FluentValidation | 12.1.1 |
| AwesomeAssertions | 9.5.0 |
| AwesomeAssertions.Web | 1.9.6 |
| Respawn | 7.0.0 |
| Flurl.Http | 4.0.2 |

`Microsoft.AspNetCore.OpenApi` 10.0.10 會帶入具有高嚴重性公告的 `Microsoft.OpenApi` 2.0.0，因此 per-day CPM 將它釘選到相容的 2.x 最新穩定版 2.11.0。這項釘選已通過完整 API 測試與 NuGet 弱點稽核。

## 前置需求

- .NET 10 SDK
- Docker Desktop 或可用的 Docker daemon
- Aspire CLI 13.4.6（執行測試不是必要條件，但可用 `aspire doctor` 檢查環境）

本機 `aspire doctor` 會提示 HTTPS 開發憑證未受信任。整合測試明確使用 AppHost 的 `http` endpoint，因此不依賴受信任的 HTTPS 開發憑證。

## 建置與測試

在 `samples/day25` 執行：

```powershell
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

`global.json` 已將 `dotnet test` runner 設為 `Microsoft.Testing.Platform`，所以不使用 `Microsoft.NET.Test.Sdk` 或 `xunit.runner.visualstudio` 的 VSTest 路徑。

## Aspire 資源模型

AppHost 管理三類資源：

```text
PostgreSQL 18.3 ── productdb ─┐
                              ├── day25-api ── HTTP integration tests
Redis ────────────────────────┘
```

- PostgreSQL 與 Redis 都明確使用 `ContainerLifetime.Session`。
- API 使用 `WithReference` 取得 `productdb` 與 `redis` 連線資訊。
- API 使用 `WaitFor` 等待相依資源。
- 測試使用 `CreateHttpClient("day25-api", "http")`，不依賴 Aspire 13.4 的預設 endpoint 選擇順序。
- Production Redis cache command 使用 request cancellation token 約束等待，`OperationCanceledException` 會繼續向外傳遞。

## 測試生命週期

`AspireAppFixture` 在整個 xUnit collection 期間只啟動一次 AppHost：

1. 用兩分鐘 cancellation timeout 建立並啟動 AppHost。
2. 等待 PostgreSQL 與 Redis resource healthy。
3. 建立一次 `productdb`、資料表與 Respawner。
4. 在 10 秒 Redis client timeout 與 fixture 的兩分鐘總 timeout 內連線 Redis 並執行 ping。
5. 等待 API healthy，建立明確指定 `http` 的 HttpClient，再呼叫 `/health`。
6. 每個測試前清除 PostgreSQL 與 Redis 資料。
7. collection 結束時依序釋放 HttpClient、Redis connection 與 AppHost。

這個流程修正了舊版每個測試各自執行 `CREATE DATABASE` 的競爭條件。遷移前完整測試會偶發收到 PostgreSQL `23505 duplicate key`；初始化集中到 fixture 後不再發生。

## API 與測試範圍

| 類別 | 驗證內容 |
| --- | --- |
| `ProductsControllerTests` | CRUD、分頁、搜尋、FluentValidation、ProblemDetails |
| `HealthControllerTests` | `/health`、`/health/alive` |
| `VerifyAspireContainers` | 動態連線字串、Redis PING、PostgreSQL `server_version` 18.3 |

總數維持原有的 16 個測試，沒有新增 Day23 的測試案例。

## 實測結果

環境：.NET SDK 10.0.302、Aspire 13.4.6、Docker 29.6.2。

| 執行 | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| 遷移前 xUnit v2 baseline | 16 | 15 | 1 | 0 | 約 1 分 27 秒 |
| Run 1 | 16 | 16 | 0 | 0 | 28.048 秒 |
| Run 2 | 16 | 16 | 0 | 0 | 25.472 秒 |
| repo 外 portability | 16 | 16 | 0 | 0 | 28.820 秒 |

正式 TRX 位於：

- `TestResults/day25-run1.trx`
- `TestResults/day25-run2.trx`

## 套件稽核

```powershell
dotnet list Day25.AspireIntegration.sln package --outdated
dotnet list Day25.AspireIntegration.sln package --deprecated --include-transitive
dotnet list Day25.AspireIntegration.sln package --vulnerable --include-transitive
```

結果：

- Direct outdated：0
- Deprecated（含遞移相依）：0
- Vulnerable（含遞移相依）：0

## 常見問題

### restore 出現 NU1605

確認命令是在 `samples/day25` 執行，讓 NuGet 使用這一層的 `Directory.Packages.props`。遷移前根目錄 CPM 混用了 `Microsoft.Extensions.*` 10.0.5 與 10.0.9；Day25 現在將保留的 Extensions 套件對齊到 10.0.10。

### PostgreSQL 顯示 running，但 API 還無法使用

`running` 只代表程序已啟動，不等於資料庫或 API 已能接受請求。Fixture 先等待 resource healthy，再用 Redis ping 與 `/health` 做實際探測，全部都有兩分鐘總 timeout。

### HTTP request 被導向 HTTPS

Aspire 13.4 在未指定 endpoint name 時採 HTTPS-first。測試必須明確使用：

```csharp
app.CreateHttpClient("day25-api", "http");
```

### 測試單獨通過、整批執行失敗

先檢查資料庫建立是否被多個測試重複執行，以及 Redis 是否保留舊 cache。這個範例讓 fixture 初始化一次，並在每個案例前同時重設 PostgreSQL 與 Redis。

## 參考資料

- [Upgrade to Aspire 13](https://learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13)
- [What's new in Aspire 13.4](https://aspire.dev/whats-new/aspire-13-4/)
- [Access resources in Aspire tests](https://aspire.dev/testing/accessing-resources/)
- [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration)
