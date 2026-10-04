# Day 24 - .NET Aspire Testing 入門基礎介紹

這個範例使用 .NET 10、Aspire 13.5.3 與 xUnit v3，示範如何透過 AppHost 啟動 SQL Server，並以真實資料庫執行 EF Core 整合測試。

## 專案結構

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
│       └── Program.cs
└── tests/
    └── BookStore.Tests/
        ├── Helpers/
        ├── Infrastructure/
        ├── Integration/
        └── Models/
```

## 版本

版本由這個範例自己的 `Directory.Packages.props` 管理，不依賴 repo 根目錄的 CPM。

| 套件 | 版本 |
| --- | ---: |
| .NET SDK | 10.0.300，`latestFeature` roll-forward |
| Aspire AppHost SDK | 13.5.3 |
| `Aspire.Hosting.SqlServer` | 13.5.3 |
| `Aspire.Hosting.Testing` | 13.5.3 |
| `xunit.v3.mtp-v2` | 4.0.0 |
| `Microsoft.Testing.Extensions.TrxReport` | 2.4.0 |
| `AwesomeAssertions` | 9.6.0 |
| Entity Framework Core | 10.0.12 |

`Microsoft.Data.SqlClient` 不另外釘選。早期為了避開 SqlClient 6.1.1 的已淘汰相依鏈曾釘在 7.0.2；EF Core 10.0.12 的下限已是 6.1.6，拿掉釘選後 deprecated 掃描為 0，所以移除。

## 前置需求

1. .NET 10 SDK
2. Docker Desktop 或其他可用的 Docker daemon
3. 可選：Aspire CLI 13.4，用於執行 `aspire doctor`

先確認 Docker daemon 已啟動：

```powershell
docker version
```

## 執行方式

以下指令從 `samples/day24` 執行：

```powershell
dotnet restore Day24.AspireTesting.sln
dotnet build Day24.AspireTesting.sln --no-restore --no-incremental
dotnet test --solution Day24.AspireTesting.sln --no-build
```

產生 TRX：

```powershell
dotnet test --solution Day24.AspireTesting.sln `
  --no-build `
  --report-trx `
  --report-trx-filename day24.trx
```

列出測試：

```powershell
dotnet test --solution Day24.AspireTesting.sln --no-build --list-tests
```

## 實作重點

### Aspire 13 AppHost 格式

AppHost 使用 Aspire 13 的 SDK 宣告：

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.5.3">
```

Aspire 13 SDK 已自動提供 `Aspire.Hosting.AppHost`，不需要再加入同名 `PackageReference`。

AppHost 另外用 `NoWarn` 抑制 `ASPIRE010`。Aspire 13.5.0 起，沒有啟用 Aspire CLI bundle 就會發出這則提示；本範例不要求安裝 Aspire CLI，所以不啟用 bundle。

### 服務就緒判斷

`KnownResourceStates.Running` 只代表程序已開始執行，不保證 SQL Server 已可連線。Fixture 改用有兩分鐘上限的健康狀態等待：

```csharp
using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
await app.ResourceNotifications.WaitForResourceHealthyAsync(
    "sql",
    cancellationTokenSource.Token);
```

資料庫只在 Collection Fixture 初始化時執行一次 `EnsureCreatedAsync`，不會在每次取得 DbContext 時重複建立。

### 測試隔離

所有整合測試共用一個 Aspire AppHost 與 SQL Server 容器。每個測試案例開始前，`IntegrationTestBase` 會刪除 `Books` 資料，兼顧容器重用與測試隔離。

### xUnit v3 與 MTP

測試專案是可執行檔，使用 `xunit.v3.mtp-v2`。`global.json` 將 `dotnet test` runner 設成 `Microsoft.Testing.Platform`，因此不再引用：

- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`

`IAsyncLifetime.InitializeAsync` 與 `DisposeAsync` 使用 `ValueTask`；測試會把 `TestContext.Current.CancellationToken` 經由 service 與 repository 傳到可取消的 EF Core 呼叫。

## 測試範圍

目前共有 44 個測試，涵蓋：

- Repository CRUD 與查詢
- Service 驗證與業務規則
- SQL Server schema 與原生 SQL
- 交易回滾
- 並行新增資料
- 測試資料隔離
- 錯誤診斷與資料庫健康檢查

2026-07-21 的遷移驗證結果：

| 執行 | Total | Passed | Failed | Skipped | Duration |
| --- | ---: | ---: | ---: | ---: | ---: |
| Run 1 | 44 | 44 | 0 | 0 | 30.064 秒 |
| Run 2 | 44 | 44 | 0 | 0 | 26.342 秒 |
| repo 外 portability | 44 | 44 | 0 | 0 | 30.238 秒 |

2026-10-04 升到 Aspire 13.5.3 與 xUnit v3 4.0.0 後重跑兩次都是 44／44，耗時 31.3 秒與 27.1 秒。

遷移前 xUnit v2 baseline 是 44 個測試、42 passed、2 failed，耗時 3 分 36 秒。失敗原因分別是並行呼叫 `EnsureCreatedAsync`，以及測試資料互相污染。

## 套件稽核

2026-10-04 執行 NuGet 稽核：

- deprecated（包含 transitive）：0
- vulnerable（包含 transitive）：0
- 直接相依 outdated：4 個套件（Aspire 兩個套件 13.6.0、`xunit.v3.mtp-v2` 4.0.1、`Microsoft.Testing.Extensions.TrxReport` 2.4.1）。版本刻意跟 Day25 和系列其他天數對齊，沒有單獨追到最新

可用以下指令重新確認：

```powershell
dotnet list Day24.AspireTesting.sln package --outdated
dotnet list Day24.AspireTesting.sln package --deprecated --include-transitive
dotnet list Day24.AspireTesting.sln package --vulnerable --include-transitive
```

## 常見問題

### 找不到 Docker daemon

先啟動 Docker Desktop，再執行 `docker version`。只有 Docker client 版本而沒有 server 版本時，容器測試無法執行。

### 測試停在資源啟動

不要改回固定 `Task.Delay`。先檢查：

- Docker 資源是否足夠
- SQL Server container log
- readiness timeout 是否真的到期
- `aspire doctor` 的環境檢查結果

### 交易測試出現 execution strategy 錯誤

啟用 `EnableRetryOnFailure` 的 DbContext 不支援直接建立 user-initiated transaction。交易測試應使用 fixture 提供的 non-retry DbContext，或透過 EF Core execution strategy 包裝整個交易單元。

## 附錄：Windows 上的 SQL Server 登入前交握逾時

這不是每台 Windows 都會遇到。若容器顯示 Running，但測試仍在 TDS PRELOGIN 階段逾時，先檢查防毒軟體、EDR、VPN、廣告攔截器或其他會檢查 localhost 流量的工具。

本機的成因是 AdGuard v8 開啟 `Filter localhost`。關閉 `Settings → App settings → Advanced settings → Filter localhost` 後，AdGuard 服務仍維持執行，TDS 回應恢復為完整 26 bytes；本範例也已在 Windows 實測 44／44 通過，耗時 34.9 秒。

其他產品可找 localhost、loopback、network inspection 或 web protection 相關設定。先停用範圍最小的本機流量檢查，不要先改寫 Aspire 的健康檢查或等待流程。

參考與追蹤：[AdGuard 進階設定說明](https://adguard.com/kb/adguard-for-windows/settings/app-settings/advanced-settings/)、[AdGuard for Windows #6242](https://github.com/AdguardTeam/AdguardForWindows/issues/6242)、[Docker Desktop #622](https://github.com/docker/desktop-feedback/issues/622)。

## 參考資料

- [Upgrade to Aspire 13](https://learn.microsoft.com/dotnet/aspire/get-started/upgrade-to-aspire-13)
- [Access resources in Aspire tests](https://aspire.dev/testing/accessing-resources/)
- [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration)
- [.NET 10 `dotnet test`](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
