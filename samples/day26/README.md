# Day26：從 xUnit v2 升級到 xUnit v3

這個範例以 .NET 10 展示 xUnit v2 → v3 的遷移。`Calculator.Tests.V2` 是升級前對照；`Calculator.Tests.V3` 是採用 xUnit v3 3.2.2 與 Microsoft Testing Platform v2 的完成版本。

## 專案結構

```text
samples/day26/
├── Day26.XunitUpgrade.sln
├── Directory.Packages.props
├── global.json                         # 正式方案使用 MTP
├── src/
│   ├── Calculator.Core/
│   ├── Calculator.Tests.V2/            # VSTest 對照，不在主 solution
│   │   └── global.json                 # 在此目錄切回 VSTest
│   └── Calculator.Tests.V3/
│       ├── Fixtures/SharedStateFixture.cs
│       ├── AssemblyInfo.cs
│       ├── CalculatorTests.cs
│       ├── XunitV3FeatureTests.cs
│       └── xunit.runner.json
└── tools/upgrade-script.ps1            # 唯讀遷移檢查
```

主 solution 只包含 `Calculator.Core` 與 `Calculator.Tests.V3`。原因是 .NET 10 由 `global.json` 選擇 test runner；MTP-only solution 不應混入只能由 VSTest 執行的 v2 專案。

## 版本

| 項目 | 版本 |
| --- | --- |
| Target framework | `net10.0` |
| xUnit v2 對照 | 2.9.3 |
| xUnit v3 | 3.2.2 |
| Microsoft Testing Platform | v2（由 `xunit.v3.mtp-v2` 提供） |
| Microsoft.Testing.Extensions.TrxReport | 2.3.2 |
| AwesomeAssertions | 9.5.0 |
| Microsoft.NET.Test.Sdk（僅 v2） | 18.8.1 |
| xunit.runner.visualstudio（僅 v2） | 3.1.5 |
| coverlet.collector（僅 v2） | 10.0.1 |

## 執行正式 v3 方案

請從 `samples/day26` 執行：

```powershell
dotnet restore Day26.XunitUpgrade.sln
dotnet build Day26.XunitUpgrade.sln --no-restore --no-incremental
dotnet test --solution Day26.XunitUpgrade.sln --no-build
```

產生 TRX：

```powershell
dotnet test --solution Day26.XunitUpgrade.sln --no-build `
  --report-trx --report-trx-filename day26.trx
```

預設結果為 30 tests：28 passed、2 skipped。兩個 skipped 分別是動態跳過示例，以及預設不執行的 explicit test。

## 執行 v2 對照專案

v2 專案必須從自己的目錄執行，讓該目錄的 `global.json` 選擇 VSTest：

```powershell
Set-Location src/Calculator.Tests.V2
dotnet restore Calculator.Tests.V2.csproj
dotnet build Calculator.Tests.V2.csproj --no-restore --no-incremental
dotnet test Calculator.Tests.V2.csproj --no-build
```

結果為 17/17 passed。build 會刻意保留兩個 `xUnit1048` 警告，指出 v2 範例內的 `async void` 測試必須在遷移前改成 `async Task`。

## 這個範例示範什麼

- test project 從 Library 改成可執行的 `Exe`
- 將 `xunit`、VS runner 與 Test SDK 替換為 `xunit.v3.mtp-v2`
- 使用 `global.json` 讓 .NET 10 選擇 Microsoft Testing Platform
- 將 `async void` 改成 `async Task`
- 使用 `ValueTask` 實作 xUnit v3 `IAsyncLifetime`
- 使用真實的 `TestContext` cancellation token 與 attachment API
- dynamic skip、explicit test、`MatrixTheoryData`
- assembly fixture、Console 與 Trace output capture

`xunit.v3` 3.2.2 本身內含 MTP v1 runner；本範例改用 `xunit.v3.mtp-v2`，明確選擇 MTP v2。正式 v3 專案不需要 `Microsoft.NET.Test.Sdk` 或 `xunit.runner.visualstudio`。

## 遷移檢查工具

工具只做唯讀盤點，不會自動覆寫專案：

```powershell
./tools/upgrade-script.ps1 -ProjectPath ./src/Calculator.Tests.V2
```

套件替換、runner 選型及自訂擴充相容性都需要人工判斷，因此範例不提供會直接修改 csproj 或測試程式碼的升級腳本。

## 參考資料

- [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration)
- [xUnit v3 with Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [What is new in xUnit v3](https://xunit.net/docs/getting-started/v3/whats-new)
- [.NET `dotnet test`](https://learn.microsoft.com/dotnet/core/tools/dotnet-test)
