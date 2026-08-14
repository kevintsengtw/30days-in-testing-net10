---
day: 26
title: "Day 26 - 從 xUnit v2 升級到 xUnit v3：.NET 10 與 Microsoft Testing Platform 遷移指南"
sample: samples/day26
target_framework: net10.0
packages:
  - AwesomeAssertions
  - coverlet.collector
  - Microsoft.NET.Test.Sdk
  - xunit
  - xunit.runner.visualstudio
  - Microsoft.Testing.Extensions.TrxReport
  - xunit.v3.mtp-v2
---

# Day 26 - 從 xUnit v2 升級到 xUnit v3：.NET 10 與 Microsoft Testing Platform 遷移指南

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [先確認版本與支援範圍](#先確認版本與支援範圍)
- [遷移前先跑 baseline](#遷移前先跑-baseline)
- [最終專案架構](#最終專案架構)
- [為什麼 v2 不放在主 solution](#為什麼-v2-不放在主-solution)
- [用 per-day CPM 固定版本](#用-per-day-cpm-固定版本)
- [專案檔怎麼改](#專案檔怎麼改)
- [Breaking change 1：不能再用 async void](#breaking-change-1不能再用-async-void)
- [Breaking change 2：IAsyncLifetime 改用 ValueTask](#breaking-change-2iasynclifetime-改用-valuetask)
- [Breaking change 3：檢查 xunit.abstractions 與擴充套件](#breaking-change-3檢查-xunitabstractions-與擴充套件)
- [真實的 xUnit v3 功能示例](#真實的-xunit-v3-功能示例)
- [執行 v2 對照](#執行-v2-對照)
- [執行 v3 正式方案](#執行-v3-正式方案)
- [稽核 NuGet 套件](#稽核-nuget-套件)
- [建議的實際升級順序](#建議的實際升級順序)
- [小結](#小結)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

前面的文章範例已陸續更新為 .NET 10、xUnit v3 與 Microsoft Testing Platform（MTP）。Day26 回頭處理一個實務問題：既有專案仍使用 xUnit v2 時，應該怎麼升級，才能避免只換套件名稱卻留下不相容的測試程式碼與 runner 設定？

本篇用同一個 Calculator 範例保留兩個狀態：

- `Calculator.Tests.V2`：升級前對照，使用 .NET 10 + xUnit 2.9.3 + VSTest。
- `Calculator.Tests.V3`：完成狀態，使用 .NET 10 + xUnit 3.2.2 + MTP v2。

原始範例是 .NET 9；lab 建立時曾先把 TFM 機械式改成 `net10.0`，但那不代表 xUnit v3 遷移已完成。這次才正式處理套件、runner、專案輸出型態、breaking changes 與文件驗證。

## 本篇內容

- 將測試專案從 .NET 9 更新為 .NET 10
- 選擇 xUnit v3 的 MTP runner，而不是繼續使用 VSTest
- 調整 test project 的套件與 `OutputType`
- 修正 `async void` 與 `IAsyncLifetime` 的相容性問題
- 保留可執行的 v2／v3 對照，又不在同一個 solution 混用兩套 runner
- 用真實 API 展示 xUnit v3 的 TestContext、dynamic skip、explicit test、matrix theory 與 assembly fixture
- 以 build、test、TRX 與 package audit 驗證結果

## 先確認版本與支援範圍

本篇在 2026-07-22 查得並實際使用的穩定版本如下：

| 項目 | 版本 |
| --- | --- |
| .NET target framework | `net10.0` |
| xUnit v2 對照 | 2.9.3 |
| xUnit v3 | 3.2.2 |
| `xunit.v3.mtp-v2` | 3.2.2 |
| `Microsoft.Testing.Extensions.TrxReport` | 2.3.2 |
| `AwesomeAssertions` | 9.5.0 |
| `Microsoft.NET.Test.Sdk`（僅 v2） | 18.8.1 |
| `xunit.runner.visualstudio`（僅 v2） | 3.1.5 |
| `coverlet.collector`（僅 v2） | 10.0.1 |

xUnit NuGet 頁面當時雖已出現 v4 prerelease，但最新穩定產品版仍是 3.2.2，所以範例不採 preview。

xUnit v3 最低支援 .NET 8 或 .NET Framework 4.7.2。若專案仍使用更早的 target framework，需要先處理框架升級，不能只替換 xUnit package。

## 遷移前先跑 baseline

`Calculator.Tests.V2` 有 17 個測試，執行結果為 17 passed、0 failed。不過 build 會產生兩個 `xUnit1048`：

```text
Support for 'async void' unit tests is being removed from xUnit.net v3.
To simplify upgrading, convert the test to 'async Task' instead.
```

這兩個 warning 是刻意保留的升級教材。baseline 能證明測試在 v2 原本會通過，也提早指出升級後一定要改的地方。

既有 v3 範例則無法由原始碼重新 build：Microsoft.Extensions 套件混用 10.0.5 與 10.0.9，restore 產生 `NU1605`，SQLite 相依鏈還帶入具有高嚴重性公告的舊版 native library。那些套件與 xUnit 遷移沒有必要關係，因此本次直接移除，讓範例只保留測試框架升級所需內容。

## 最終專案架構

```text
samples/day26/
├── Day26.XunitUpgrade.sln
├── Directory.Packages.props
├── global.json
├── src/
│   ├── Calculator.Core/
│   ├── Calculator.Tests.V2/
│   │   └── global.json
│   └── Calculator.Tests.V3/
│       ├── Fixtures/SharedStateFixture.cs
│       ├── AssemblyInfo.cs
│       ├── CalculatorTests.cs
│       ├── XunitV3FeatureTests.cs
│       └── xunit.runner.json
└── tools/upgrade-script.ps1
```

主 solution 只包含 `Calculator.Core` 與 `Calculator.Tests.V3`。v2 專案仍留在原始碼目錄作為對照，但不加入正式 solution。

## 為什麼 v2 不放在主 solution

.NET 10 可以在 `global.json` 選擇 solution 使用的 test runner：

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

這個設定表示該目錄下的正式測試使用 MTP。`Calculator.Tests.V2` 則依賴 VSTest、`Microsoft.NET.Test.Sdk` 與 VS runner，不能當成 MTP test application 一起執行。

為了兼顧教學對照與可執行性，v2 專案目錄另有一份巢狀設定：

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "VSTest"
  }
}
```

從 v2 目錄執行時會使用 VSTest；從 `samples/day26` 執行主 solution 時則使用 MTP。真正的產品方案若仍有其他 VSTest-only test project，也應拆開執行或先完成 runner 遷移。

## 用 per-day CPM 固定版本

Day26 有自己的 `Directory.Packages.props`，不依賴 repo 根目錄可能持續變動的套件版本。核心設定如下：

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup Label="Assertions">
    <PackageVersion Include="AwesomeAssertions" Version="9.5.0" />
  </ItemGroup>

  <ItemGroup Label="xUnit v2 baseline">
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup Label="xUnit v3">
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.3" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.2" />
  </ItemGroup>
</Project>
```

## 專案檔怎麼改

v2 對照專案維持 Library 與 VSTest 組合：

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Library</OutputType>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="coverlet.collector" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="xunit" />
  <PackageReference Include="xunit.runner.visualstudio" />
  <PackageReference Include="AwesomeAssertions" />
</ItemGroup>
```

v3 最終專案則是可執行的 test application：

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="AwesomeAssertions" />
  <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
  <PackageReference Include="xunit.v3.mtp-v2" />
</ItemGroup>
```

這裡有三個重點：

1. xUnit v3 test project 預設是 `Exe`，可以作為獨立 test application 執行。
2. `xunit.v3.mtp-v2` 明確選擇 MTP v2；一般 `xunit.v3` 3.2.2 套件內建的是 MTP v1 runner。
3. 原生 MTP 方案不需要 `Microsoft.NET.Test.Sdk` 或 `xunit.runner.visualstudio`。若團隊決定繼續使用 VSTest，套件組合會不同，不要把兩種設定混在一起。

## Breaking change 1：不能再用 async void

v2 對照中有這種寫法：

```csharp
[Fact]
public async void CalculateFibonacciAsync_計算費氏數列_應回傳正確結果()
{
    // Arrange
    var n = 6;
    var expected = 8; // F(6) = 8

    // Act
    var actual = await _calculator.CalculateFibonacciAsync(n);

    // Assert
    actual.Should().Be(expected);
}
```

xUnit v3 遇到 `async void` test 會快速失敗。修正方式是讓 runner 能等待並觀察回傳的工作：

```csharp
[Fact]
public async Task CalculateFibonacciAsync_計算費氏數列_應回傳正確結果()
{
    // Arrange
    var n = 6;
    var expected = 8; // F(6) = 8

    // Act
    var actual = await _calculator.CalculateFibonacciAsync(n);

    // Assert
    actual.Should().Be(expected);
}
```

不要只修 `[Fact]`；所有 `[Theory]`、fixture helper 與自訂測試基底也要一起搜尋。

## Breaking change 2：IAsyncLifetime 改用 ValueTask

xUnit v3 的 `IAsyncLifetime` 使用 `ValueTask`：

```csharp
public sealed class SharedStateFixture : IAsyncLifetime
{
    public bool IsInitialized { get; private set; }

    public ValueTask InitializeAsync()
    {
        IsInitialized = true;
        Console.WriteLine("SharedStateFixture initialized");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsInitialized = false;
        Console.WriteLine("SharedStateFixture disposed");
        return ValueTask.CompletedTask;
    }
}
```

如果類別同時提供同步與非同步清理，xUnit v3 在支援 async disposal 時只會走非同步路徑。遷移時應把資源釋放集中到正確的 `DisposeAsync`，避免以為兩套清理都會執行。

## Breaking change 3：檢查 xunit.abstractions 與擴充套件

xUnit v3 不再使用獨立的 `xunit.abstractions` 套件。若程式碼有 `using Xunit.Abstractions`，要依實際型別改用 v3 namespace/API；例如 `ITestOutputHelper` 位於 `Xunit` namespace。

自訂 discoverer、test case、runner reporter 或第三方 xUnit extension 也不能假設只換 package 就相容。應逐一確認它們是否支援 v3；找不到相容版本時，要先替換或移除，再升級 runner。

## 真實的 xUnit v3 功能示例

完成必要遷移後，才適合導入 v3 新功能。以下內容都來自本篇可實際編譯及執行的測試，不是概念模擬。

### TestContext cancellation token 與附件

```csharp
[Fact]
public async Task TestContext_提供取消權杖與附件功能()
{
    var cancellationToken = TestContext.Current.CancellationToken;
    await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);

    TestContext.Current.AddAttachment(
        "calculation.txt",
        "10 + 20 = 30");

    cancellationToken.CanBeCanceled.Should().BeTrue();
}
```

執行時 runner 會列出附件實際產生的位置。長時間操作也能接收 `TestContext.Current.CancellationToken`，在測試被取消時停止工作。

### Dynamic skip 與 explicit test

```csharp
[Fact]
public void AssertSkip_可在執行期間決定跳過()
{
    if (Environment.GetEnvironmentVariable("DAY26_OPTIONAL_TEST") != "1")
    {
        Assert.Skip("設定 DAY26_OPTIONAL_TEST=1 才執行此選用測試");
    }

    new Calculator.Core.Calculator().Square(5).Should().Be(25);
}

[Fact(Explicit = true)]
public void ExplicitTest_只在明確要求時執行()
{
    new Calculator.Core.Calculator().Multiply(6, 7).Should().Be(42);
}
```

dynamic skip 是測試開始後依條件決定；explicit test 則預設不執行，只有明確選取時才執行。兩者意圖不同，不應用固定 `Skip` 一概取代。

### MatrixTheoryData

```csharp
public static MatrixTheoryData<int, int> AdditionMatrix => new(
    [1, 10],
    [2, 20]);

[Theory]
[MemberData(nameof(AdditionMatrix))]
public void MatrixTheoryData_產生輸入值的笛卡兒積(int a, int b)
{
    new Calculator.Core.Calculator().Add(a, b).Should().Be(a + b);
}
```

兩組 `a` 與兩組 `b` 會組合成 4 筆 theory rows。這適合每個參數可以獨立排列的資料；如果預期值只對應特定輸入組合，普通 `TheoryData` 通常更清楚。

### Assembly fixture 與輸出擷取

```csharp
[assembly: CaptureConsole]
[assembly: CaptureTrace]
[assembly: AssemblyFixture(typeof(SharedStateFixture))]
```

assembly fixture 在整個 test assembly 共用一個 instance。`CaptureConsole` 與 `CaptureTrace` 則讓 runner 把對應輸出附在正確的測試結果中。

xUnit v3 沒有內建 `[Test]`、`RetryFact` 或 `RetryTheory`。若需要 retry，要使用明確支援 v3 的第三方套件或在 CI 層設計重跑政策，不能把自製迴圈描述成 runner 內建重試。

## 執行 v2 對照

請從 v2 project 目錄執行，讓巢狀 `global.json` 選擇 VSTest：

```powershell
Set-Location samples/day26/src/Calculator.Tests.V2
dotnet restore Calculator.Tests.V2.csproj
dotnet build Calculator.Tests.V2.csproj --no-restore --no-incremental
dotnet test Calculator.Tests.V2.csproj --no-build
```

實測結果：

```text
Build: 0 errors, 2 xUnit1048 warnings
Test:  Total 17, Passed 17, Failed 0, Skipped 0
```

## 執行 v3 正式方案

回到 `samples/day26`：

```powershell
dotnet restore Day26.XunitUpgrade.sln
dotnet build Day26.XunitUpgrade.sln --no-restore --no-incremental
dotnet test --solution Day26.XunitUpgrade.sln --no-build `
  --report-trx --report-trx-filename day26.trx
```

第一次實測結果：

```text
Build:   0 errors, 0 warnings
Test:    Total 30, Passed 28, Failed 0, Skipped 2
TRX:     samples/day26/TestResults/day26.trx
```

兩個 skipped 是預期行為：一個 dynamic skip 示例，以及一個預設不執行的 explicit test。

## 稽核 NuGet 套件

.NET 10 的 `dotnet package list` 使用 `--project` 指定 solution 或 project。從 `samples/day26` 執行：

```powershell
dotnet package list --project Day26.XunitUpgrade.sln --outdated
dotnet package list --project Day26.XunitUpgrade.sln --deprecated
dotnet package list --project Day26.XunitUpgrade.sln `
  --vulnerable --include-transitive
```

v3 正式 solution 的三項實測結果都是 0：沒有可更新的直接套件、deprecated 套件或已知 vulnerable 套件。v2 對照專案若另外執行 deprecated 稽核，`xunit` 2.9.3 會被標記為 Legacy；這正是本篇要升級的 baseline，不是 v3 正式方案遺留的相依。

## 建議的實際升級順序

1. 先在 v2 狀態儲存 test discovery、通過／失敗／跳過數量與報告。
2. 將 target framework 更新到 xUnit v3 支援的版本，本篇使用 `net10.0`。
3. 搜尋 `async void`、`IAsyncLifetime`、`xunit.abstractions` 與自訂 extension。
4. 決定使用 MTP 或 VSTest，再選擇相符的 package 組合。
5. 將 test project 改成 `Exe`，修正 breaking changes。
6. clean、restore、build，先把 analyzer 與 package 問題處理乾淨。
7. 比對升級前後的 test discovery 與 skip/filter 行為。
8. 產生 TRX，並在 CI 與 repo 外複製環境重跑。

範例附帶的 `tools/upgrade-script.ps1` 只做唯讀盤點。runner 選型、自訂擴充與套件相容性需要專案判斷，不適合用搜尋取代直接覆寫原始碼。

## 小結

xUnit v2 → v3 會同時改到 target framework、可執行 test project、runner 與套件組合，也牽涉非同步測試簽名、fixture lifecycle 和 CI 命令。只換成 `xunit.v3` 套件並不完整。

Day26 的最終方案使用 .NET 10、xUnit 3.2.2 與 MTP v2；v2 對照被隔離在自己的 VSTest 執行範圍。如此既能看見升級前後差異，也能確保正式 solution 是可重現、可建置且不混用 runner 的狀態。

## 參考資料

- [xUnit v2 → v3 migration guide](https://xunit.net/docs/getting-started/v3/migration)
- [xUnit v3 and Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [What is new in xUnit v3](https://xunit.net/docs/getting-started/v3/whats-new)
- [xUnit v3 3.2.2 release notes](https://xunit.net/releases/v3/3.2.2)
- [.NET dotnet test command](https://learn.microsoft.com/dotnet/core/tools/dotnet-test)
