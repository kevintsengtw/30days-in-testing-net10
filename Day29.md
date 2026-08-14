---
day: 29
title: "Day 29 - TUnit 實戰：執行控制、ASP.NET Core 與 Testcontainers"
sample: samples/day29
target_framework: net10.0
packages:
  - Microsoft.AspNetCore.Mvc.Testing
  - Microsoft.AspNetCore.OpenApi
  - Microsoft.Extensions.Logging.Abstractions
  - Microsoft.OpenApi
  - SSH.NET
  - Testcontainers
  - Testcontainers.Kafka
  - Testcontainers.PostgreSql
  - Testcontainers.Redis
  - TUnit
  - TUnit.AspNetCore
---

# Day 29 - TUnit 實戰：執行控制、ASP.NET Core 與 Testcontainers

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [範例專案](#範例專案)
- [Retry：只重試可辨識的暫時性錯誤](#retry只重試可辨識的暫時性錯誤)
- [Timeout：逾時後還要能停止工作](#timeout逾時後還要能停止工作)
- [DisplayName 與 Properties](#displayname-與-properties)
- [使用 tree node filter](#使用-tree-node-filter)
- [ASP.NET Core：使用 TUnit.AspNetCore](#aspnet-core使用-tunitaspnetcore)
- [Testcontainers：共用啟動，不共用髒資料](#testcontainers共用啟動不共用髒資料)
- [Source Generation、Reflection 與 Native AOT](#source-generationreflection-與-native-aot)
- [套件安全稽核](#套件安全稽核)
- [執行本篇範例](#執行本篇範例)
- [本篇驗證結果](#本篇驗證結果)
- [重點整理](#重點整理)
- [明日預告](#明日預告)
- [參考資源](#參考資源)

<!-- /toc -->

## 前言

前兩天的測試大多留在 process 內。今天把範圍拉到 HTTP pipeline 與真實基礎設施，同時處理測試套件上 CI 後最常見的問題：偶發失敗、無限等待、難以篩選，以及共享資源互相干擾。

Retry 只用來處理明確的暫時性錯誤，重複送幾次 HTTP request 也不等於負載測試。本篇的目標是讓整合測試可重現、可診斷，失敗時能定位到出問題的層次。

## 本篇內容

- 使用 Retry 處理明確的暫時性錯誤
- 使用 Timeout 與 `CancellationToken` 中止逾時工作
- 使用 DisplayName、Property 與 tree node filter 整理測試
- 使用 `TUnit.AspNetCore` 測試 ASP.NET Core API
- 以 Assembly hooks 管理 PostgreSQL、Redis 與 Kafka containers
- 分開處理測試隔離、啟動成本與清理責任
- 認識 Source Generation、Reflection 與 Native AOT 的適用範圍

## 範例專案

```text
samples/day29/
├── Day29.TUnitAdvanced.sln
├── Directory.Packages.props
├── global.json
├── src/
│   ├── TUnit.Advanced.Core/
│   └── TUnit.Advanced.WebApi/
└── tests/
    ├── TUnit.Advanced.ExecutionControl.Tests/
    └── TUnit.Advanced.Integration.Tests/
```

整合測試需要 Docker。若 Docker daemon 不可用，先執行控制測試，不要用 mock container 假裝整合測試已通過。

## Retry：只重試可辨識的暫時性錯誤

`[Retry(n)]` 會在測試失敗後重新執行。它適合短暫網路中斷、HTTP 429、gateway timeout 等有機會在下一次嘗試恢復的情況，不適合 assertion 錯誤、資料競爭或初始化順序問題。

範例刻意用可重現的「前兩次失敗、第三次成功」取代亂數。HTTP 案例也使用記憶體內的 `HttpMessageHandler` 測試替身回傳兩次 503，再於第三次回傳成功；整個 ExecutionControl 專案不需要對外網路。亂數或真實外網都可能讓 flaky test 連續失敗，也可能長期掩蓋 Retry 根本沒有作用。

```csharp
[Test]
[Retry(3)]
[NotInParallel("RetryExamples")]
[Property("Category", TestProperties.CATEGORY_FLAKY)]
[Property("Priority", TestProperties.PRIORITY_HIGH)]
public async Task NetworkCall_可能不穩定_使用重試機制()
{
    // 用可重現的方式模擬前兩次暫時性失敗，避免測試本身帶有隨機性。
    var attempt = Interlocked.Increment(ref _networkAttempts);
    if (attempt < 3)
    {
        throw new HttpRequestException($"第 {attempt} 次呼叫發生暫時性錯誤");
    }

    await Assert.That(attempt).IsEqualTo(3);
}

[Test]
[Timeout(5000)] // 5 秒超時
[Property("Category", TestProperties.CATEGORY_PERFORMANCE)]
[Property("Priority", TestProperties.PRIORITY_MEDIUM)]
public async Task LongRunningOperation_應在時限內完成(CancellationToken cancellationToken)
{
    // 模擬可能會很慢的操作
    await Task.Delay(1000, cancellationToken); // 1 秒操作，應該在 5 秒限制內

    var result = true; // 模擬操作結果
    await Assert.That(result).IsTrue();
}

[Test]
[DisplayName("自訂測試名稱：驗證使用者註冊流程")]
[Property("Category", TestProperties.CATEGORY_UNIT)]
[Property("Priority", TestProperties.PRIORITY_CRITICAL)]
public async Task UserRegistration_CustomDisplayName_測試名稱更易讀()
{
    // 使用自訂顯示名稱讓測試報告更容易理解
    var email = "user@example.com";
    await Assert.That(email).Contains("@");
}

[Test]
[Arguments("valid@email.com", true)]
[Arguments("invalid-email", false)]
[Arguments("", false)]
[DisplayName("電子郵件驗證：{0} 應為 {1}")]
[Property("Category", TestProperties.CATEGORY_UNIT)]
[Property("Priority", TestProperties.PRIORITY_HIGH)]
public async Task EmailValidation_參數化顯示名稱(string email, bool expectedValid)
{
    // 顯示名稱會自動替換參數
    var isValid = !string.IsNullOrEmpty(email) && email.Contains("@");

    await Assert.That(isValid).IsEqualTo(expectedValid);
}
```

正式專案可以繼承 `RetryAttribute`，覆寫 `ShouldRetry`，只對特定 exception 或 HTTP status code 重試。若所有例外都無條件 retry，真正的 bug 只會晚一點才失敗。

## Timeout：逾時後還要能停止工作

`[Timeout]` 的單位是毫秒。逾時時 TUnit 會取消傳入測試方法的 `CancellationToken`；被測的 async API 也必須接收並傳遞這個 token，底層工作才會停止。

只加 attribute、不傳 token，測試雖然會被判定 timeout，背景 I/O 仍可能繼續占用資源。對 HTTP、database command、message consumer 與 `Task.Delay`，都要一路傳遞 cancellation。

ExecutionControl 與 Integration 範例都把 Timeout 當成防止工作失控的上限，不用 `Stopwatch` 對共享 CI runner 做毫秒級 SLA 斷言。真正的 latency baseline 應在固定環境暖機後，以多次量測與 percentile 統計建立。

Retry 與 Timeout 同時存在時，每次 retry 都有新的 timeout window。設定前要估算最壞總時間：

```text
最壞執行時間 ≈ 單次 timeout × 最大嘗試次數 + retry 間隔
```

## DisplayName 與 Properties

`[DisplayName]` 改善報告可讀性，`[Property]` 提供 Category、Priority、Feature 等 metadata。它們不應取代清楚的方法名稱，而是補上報告與 CI 篩選需要的資訊。

對 parameters 使用 `{0}`、`{1}` 插值時，注意測試資料可能包含敏感資訊；不要把 token、密碼或完整個資寫進顯示名稱與 CI log。

## 使用 tree node filter

TUnit 使用 MTP filter，不使用 VSTest 的 `--filter`：

```powershell
cd samples/day29/tests/TUnit.Advanced.ExecutionControl.Tests

dotnet test --treenode-filter "/*/*/*/*[Suite=Smoke]"
dotnet test --treenode-filter "/*/*/*/*[Category=Performance]"
dotnet test --treenode-filter "/*/*/*/*[(Category=Unit)&(Priority=High)]"
```

若使用舊版 SDK，runner arguments 可能需要放在 `--` 之後。本篇鎖定 .NET 10 SDK，以上命令已在範例環境驗證。

## ASP.NET Core：使用 TUnit.AspNetCore

過去可以直接建立 `WebApplicationFactory<Program>`。TUnit 目前提供 `TUnit.AspNetCore`，官方建議改用 `TestWebApplicationFactory<T>` 與 `WebApplicationTest<TFactory, TEntryPoint>`。

這不只是換類別名稱。`TUnit.AspNetCore` 會把 request trace、server-side log 與目前 test context 關聯起來，對預設並行的整合測試尤其重要。

範例 factory 如下：

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
        {
            services.AddLogging();
        });
    builder.UseEnvironment("Testing");
}
```

測試類別繼承 `WebApplicationTest<TestingWebApplicationFactory, Program>`，再透過 `Factory.CreateClient()` 發出真實 HTTP request。API 專案仍需公開 `partial class Program`，讓測試專案能指定 entry point。

`WebApiIntegrationTests` 與 `OrderApiIntegrationTests` 都加上 `[NotInParallel("WebApplicationFactory")]`。這不是預防性的全域序列化：實際並行執行時曾在兩個類別同時清理 factory 的階段觸發 `WebApplicationFactory.DisposeAsync()` race，因此只把會競爭同一類 host 生命週期的測試放進同一個互斥群組。

這與 Day27「優先隔離」的原則不衝突。完全隔離可以讓每個案例各自建立 host 與 factory，但會重複支付啟動與清理成本；此處選擇共用昂貴資源並序列化使用它的測試，其他不共用 factory 的測試仍可並行。

### 整合測試要驗證什麼

HTTP 整合測試至少應跨過：

- routing
- middleware
- model binding／serialization
- DI container
- endpoint 或 controller
- response status、headers 與 body

只驗證 `IsSuccessStatusCode` 太寬鬆。正式 API 應直接驗證預期 status code、Content-Type，以及成功與錯誤 response contract。

範例的 Order API 測試分別驗證 `201 Created` 與 `Location` header、`400 ValidationProblemDetails` 的欄位錯誤，以及 `404 ProblemDetails` 的 status 與 title；不是重複呼叫 WeatherForecast 來代替訂單情境。

### 這不是負載測試工具

在測試中同時送出 50 個 request，可以抓到部分 thread-safety 或共享狀態問題，但不能據此推論 production throughput。負載測試還需要固定環境、暖機、持續時間、percentile latency、資源監控與專用工具。

因此本篇把這類案例定位為「並行整合測試」，不稱為正式效能 benchmark。

## Testcontainers：共用啟動，不共用髒資料

範例使用 `[Before(Assembly)]` 啟動 PostgreSQL、Redis、Kafka 與共用 Docker network，並在 `[After(Assembly)]` 釋放資源。

Assembly scope 能避免每個案例都重新拉起 container，但不代表所有測試可以共用同一份資料。容器生命週期與資料隔離是兩個問題：

- container 可以整個 assembly 共用。
- database schema、transaction、tenant key、Redis key prefix 與 Kafka topic 應按測試隔離。

完整且實際被測試共用的啟動與清理程式碼位於 `GlobalTestInfrastructureSetup.cs`。整個 assembly 只保留這一組 hooks，實際建立 PostgreSQL、Redis、Kafka 三個 container 與一個 Docker network。

### 為什麼使用 Assembly hooks

- 啟動一次，降低本機與 CI 成本。
- 測試開始前就能確認基礎設施是否可用。
- 集中處理 network、port mapping 與 disposal。

代價是共享資源更容易互相干擾。只要案例會寫入同一張表、同一個 cache key 或同一個 topic，仍要加入資料清理或唯一識別碼。

### 清理順序

釋放資源時由上層 consumer 往下拆：

1. message／application client
2. Kafka、Redis 等中介服務
3. PostgreSQL
4. Docker network

清理失敗也要留下診斷資訊；不要為了讓測試顯示綠燈而吞掉 disposal exception。

## Source Generation、Reflection 與 Native AOT

TUnit 預設使用 Source Generation。Reflection Mode 適合 bUnit Razor component 等需要執行期發現的情境，也會由 F#、VB.NET 專案使用。

在目前版本可用下列方式切換：

```powershell
dotnet test -- --reflection
```

也可以在 assembly 加上 `[assembly: ReflectionMode]`。如果專案確定只用 Reflection Mode，可關閉 TUnit source generation，減少 build 成本。

Native AOT 只適合所有相依套件都能通過 AOT 分析的測試專案。`WebApplicationFactory`、Testcontainers、serialization 與第三方套件可能帶入額外限制；先 publish 實測，不要因為 unit test 能 AOT 就推論整套整合測試都能 AOT。

## 套件安全稽核

這次驗證發現 `Microsoft.AspNetCore.OpenApi` 間接帶入 `Microsoft.OpenApi 2.0.0`，命中高嚴重性公告 CVE-2026-49451。依官方公告，2.x 修補版本為 2.7.5，因此範例透過 Central Package Management 固定到 2.7.5。

測試全部通過不代表 dependency 沒有風險。CI 至少要保留：

```powershell
dotnet list Day29.TUnitAdvanced.sln package --vulnerable --include-transitive
```

## 執行本篇範例

### 不需要 Docker 的執行控制測試

```powershell
cd samples/day29
dotnet test --project tests/TUnit.Advanced.ExecutionControl.Tests
```

### 需要 Docker 的整合測試

```powershell
docker info
dotnet test --project tests/TUnit.Advanced.Integration.Tests
```

### 完整 solution

```powershell
dotnet test --solution Day29.TUnitAdvanced.sln
```

## 本篇驗證結果

在 Docker Desktop 29.6.2 上驗證：

| 測試專案 | 結果 |
| --- | ---: |
| ExecutionControl.Tests | 16 passed |
| Integration.Tests | 23 passed |
| 合計 | 39 passed |

容器整合測試約 20 秒。這個數字只代表本次環境的 smoke baseline，不是跨機器的效能承諾。

## 重點整理

- Retry 只處理可辨識的暫時性錯誤，不能掩蓋 deterministic failure。
- Timeout 要搭配 `CancellationToken`，才能真正停止底層工作。
- TUnit 使用 MTP tree node filter，不使用 VSTest filter。
- ASP.NET Core 整合測試優先使用 `TUnit.AspNetCore`。
- 共用 container 不等於共用測試資料；資料隔離仍要另外設計。
- Source Generation、Reflection 與 Native AOT 是不同選項，不要混為一談。
- 測試通過後仍要執行 transitive package security audit。

## 明日預告

Day30 會回到「誰來寫與維護這些測試」：從單次提示詞、Custom Instructions，走到可重用的 .NET Testing Agent Skills，以及 Analyzer、Writer、Executor、Reviewer 組成的 Agent Orchestration 工作流程。

## 參考資源

- [Retrying](https://tunit.dev/docs/execution/retrying/)
- [Timeouts](https://tunit.dev/docs/execution/timeouts/)
- [Test Filters](https://tunit.dev/docs/execution/test-filters/)
- [ASP.NET Core Integration Testing](https://tunit.dev/docs/examples/aspnet/)
- [Engine Modes](https://tunit.dev/docs/execution/engine-modes/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [CVE-2026-49451](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十九天。**
