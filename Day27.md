---
day: 27
title: "Day 27 - TUnit 入門：在 .NET 10 使用 Microsoft Testing Platform"
sample: samples/day27
target_framework: net10.0
packages:
  - Microsoft.Bcl.TimeProvider
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.CodeCoverage
  - Microsoft.Testing.Extensions.TrxReport
  - TUnit
---

# Day 27 - TUnit 入門：在 .NET 10 使用 Microsoft Testing Platform

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [本篇驗證環境](#本篇驗證環境)
- [TUnit 的三個核心差異](#tunit-的三個核心差異)
- [建立測試專案](#建立測試專案)
- [第一個 TUnit 測試](#第一個-tunit-測試)
- [參數化測試](#參數化測試)
- [例外斷言](#例外斷言)
- [並行控制](#並行控制)
- [規則重疊：要測交界，不只測各自的代表值](#規則重疊要測交界不只測各自的代表值)
- [CLI 與 IDE](#cli-與-ide)
- [Native AOT 要不要現在就用](#native-aot-要不要現在就用)
- [與 xUnit 的基本對照](#與-xunit-的基本對照)
- [本篇驗證結果](#本篇驗證結果)
- [重點整理](#重點整理)
- [明日預告](#明日預告)
- [參考資源](#參考資源)

<!-- /toc -->

## 前言

Day26 在 xUnit v2 到 v3 的遷移中已經碰過 Microsoft Testing Platform（MTP）。今天改從零建立 TUnit 專案，實際檢查 MTP、Source Generator 與預設並行執行會帶來哪些差異。

這次覆寫刻意拿掉「下世代框架一定比較快」這類難以驗證的宣傳句。TUnit 的設計確實不同，但採不採用仍要看團隊現況、套件相容性與遷移成本，不能只看一次 benchmark。

## 本篇內容

- 建立 .NET 10 + TUnit 測試專案
- 理解 Source Generation、MTP 與 Native AOT 的關係
- 使用 `[Test]`、`[Arguments]` 與 TUnit Assertions
- 釐清同步測試、非同步測試與 `await` assertions
- 使用 `dotnet test`、`dotnet run` 與 IDE 執行測試
- 控制預設並行行為
- 評估 TUnit 適不適合既有專案

## 本篇驗證環境

本文與範例在 2026-07-22 使用下列環境驗證：

| 項目 | 版本 |
| --- | --- |
| .NET SDK | 10.0.302 |
| Target framework | `net10.0` |
| TUnit | 1.65.0 |
| Microsoft Testing Platform | 由 TUnit 套件帶入 |

套件版本由 `samples/day27/Directory.Packages.props` 集中管理。測試專案不安裝 `Microsoft.NET.Test.Sdk`，因為 TUnit 使用 MTP，不走 VSTest runner。

## TUnit 的三個核心差異

### Source Generator 負責測試發現

TUnit 預設在編譯期間產生測試註冊與呼叫程式碼。好處不只是少用反射，也包含編譯期檢查、型別安全，以及 Native AOT 相容性。

這裡要把兩件事分開：

- Source Generation 是一般 build 就會使用的測試發現機制。
- Native AOT 只有在 `dotnet publish` 並啟用 `PublishAot` 時才會發生。

使用 TUnit 不代表每次執行測試都是 AOT，也不代表測試一定會快固定倍數。測試數量、I/O、容器啟動、JIT 暖機與 CI 機器規格都會影響結果。

### 執行平台是 Microsoft Testing Platform

TUnit 建立在 MTP 上，因此測試專案本身可以執行。它支援 `dotnet test`、`dotnet run`、直接執行產出的 DLL，以及 publish 後的執行檔。

對日常 solution 驗證，我仍建議先用：

```powershell
dotnet test --solution Day27.TUnit.sln
```

要把 coverage、TRX 或 TUnit runner 參數直接傳給單一測試專案時，`dotnet run` 比較直覺：

```powershell
dotnet run --project tests/TUnit.Demo.Tests --configuration Release --coverage --report-trx
```

兩個命令不是互斥關係。`dotnet test` 適合 solution 與多 target framework；`dotnet run` 適合直接操作單一可執行測試專案。

### 預設並行執行

TUnit 預設讓能並行的測試一起執行。這能縮短沒有共享狀態的測試套件，但也會把原本被執行順序掩蓋的問題提早暴露出來。

如果測試共用資料庫、固定連接埠或同一份檔案，先隔離資源；真的不能隔離，再用 `[NotInParallel]` 建立互斥群組。

## 建立測試專案

官方 template 是最簡單的起點：

```powershell
dotnet new install TUnit.Templates
dotnet new TUnit -n MyProject.Tests
```

也可以從 console 專案手動建立，再加入 `TUnit` 套件。不論採哪一種方式，都不要額外加入 `Microsoft.NET.Test.Sdk`。

本篇範例放在 `samples/day27`：

```text
samples/day27/
├── Day27.TUnit.sln
├── Directory.Packages.props
├── global.json
├── src/TUnit.Demo.Core/
└── tests/TUnit.Demo.Tests/
```

## 第一個 TUnit 測試

TUnit 不需要 `[TestClass]`。公開 instance method 加上 `[Test]` 就能被發現。

```csharp
[Test]
public async Task Add_輸入1和2_應回傳3()
{
    // Arrange
    int a = 1;
    int b = 2;
    int expected = 3;

    // Act
    var result = _calculator.Add(a, b);

    // Assert
    await Assert.That(result).IsEqualTo(expected);
}
```

TUnit assertions 是 awaitable。少了 `await`，assertion 不會照預期執行；新版 analyzer 會協助抓出這類問題。

### 同步測試不是禁止項目

早期文章常把「TUnit assertions 必須 await」簡化成「所有 TUnit 測試都必須是 `async Task`」，兩者並不相同。

同步 `void` 測試是合法的：

```csharp
[Test]
public void Add_同步測試方法_可以執行()
{
    _ = _calculator.Add(1, 2);
}
```

不過同步測試不能直接使用 TUnit 的 awaitable assertions。只要需要 `Assert.That(...)`，測試方法通常就應寫成 `async Task`。`async void` 則不允許，analyzer 會回報 `TUnit0031`。

## 參數化測試

TUnit 統一使用 `[Test]`，再用 `[Arguments]` 提供常數測試資料，不像 xUnit 需要在 `[Fact]` 與 `[Theory]` 之間切換。

```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(-1, 1, 0)]
[Arguments(0, 0, 0)]
[Arguments(100, -50, 50)]
public async Task Add_多組輸入_應回傳正確結果(int a, int b, int expected)
{
    // Act
    var result = _calculator.Add(a, b);

    // Assert
    await Assert.That(result).IsEqualTo(expected);
}
```

`[Arguments]` 適合字串、數字、enum 等可以寫在 attribute 的常數。複雜物件、檔案資料或動態組合，留到 Day28 的 `MethodDataSource`、`ClassDataSource` 與 Matrix Tests。

## 例外斷言

要驗證同步方法拋出的例外，把呼叫包成 delegate 交給 assertion：

```csharp
[Test]
public async Task Divide_輸入0作為除數_應拋出DivideByZeroException()
{
    // Arrange
    int dividend = 10;
    int divisor = 0;

    // Act & Assert
    await Assert.That(() => _calculator.Divide(dividend, divisor))
        .Throws<DivideByZeroException>();
}
```

測試非同步方法時則要把 `Task` 傳給對應的 async exception assertion。不要先 `await` 被測方法，否則例外會在 assertion 接手前就拋出。

## 並行控制

以下兩個模擬資料庫測試共用 `DatabaseTests` 群組，因此彼此不並行；其他沒有加入群組的測試仍可並行。

```csharp
[Test]
[NotInParallel("DatabaseTests")]
public async Task 資料庫測試1_不並行執行()
{
    // 模擬資料庫操作
    await Task.Delay(100);
    var result = 1 + 1;
    await Assert.That(result).IsEqualTo(2);
}

[Test]
[NotInParallel("DatabaseTests")]
public async Task 資料庫測試2_不並行執行()
{
    // 模擬資料庫操作
    await Task.Delay(100);
    var result = 2 + 2;
    await Assert.That(result).IsEqualTo(4);
}
```

`[NotInParallel]` 是最後一道保護，不是測試隔離的替代品。能為每個測試建立獨立資料、獨立 schema 或獨立容器時，優先隔離。

## 規則重疊：要測交界，不只測各自的代表值

時間規則單獨成立，不代表重疊時仍會選到正確結果。例如聖誕節可能同時是週五；如果程式先判斷每週五優惠，較具體的聖誕優惠就永遠不會執行。

只測「一般週五」與「不是週五的聖誕節」會讓兩條規則看似都正確，卻漏掉真正決定優先序的交界。因此範例直接固定在 2026 年 12 月 25 日：

```csharp
[Test]
public async Task GetTimeBasedDiscount_聖誕節同時為週五_應優先回傳聖誕優惠()
{
    // 2026/12/25 同時是聖誕節與週五，直接驗證兩條規則的交界。
    var christmasFriday = new DateTimeOffset(2026, 12, 25, 12, 0, 0, TimeSpan.Zero);
    var fakeTimeProvider = new FakeTimeProvider(christmasFriday);
    var timeService = new TimeService(fakeTimeProvider);

    var discount = timeService.GetTimeBasedDiscount();

    await Assert.That(discount).IsEqualTo("聖誕特惠：八折優惠");
}
```

這個案例要求 `TimeService` 先判斷特定節日，再判斷週期性的星期規則。2026 只是測試資料，真正要驗證的是兩條規則同時成立時的優先順序。

## CLI 與 IDE

### 執行全部測試

```powershell
cd samples/day27
dotnet test --solution Day27.TUnit.sln
```

### 顯示 runner 說明

```powershell
dotnet run --project tests/TUnit.Demo.Tests -- --help
```

### IDE 支援

- Visual Studio：啟用 Testing Platform server mode。
- Rider：啟用 Testing Platform support。
- VS Code：安裝 C# Dev Kit，並啟用 Testing Platform Protocol。

IDE 選項名稱可能隨版本變動；若 Test Explorer 找不到測試，先確認 TUnit 套件、`OutputType`、MTP 支援與乾淨重建，再查 IDE 設定。

## Native AOT 要不要現在就用

TUnit 支援 Native AOT，但單元測試通常不是因為「部署到容器」才執行。真正值得評估的情況是：大型測試套件的啟動成本已經成為 CI 瓶頸，而且所有相依套件都能通過 AOT 分析。

請用自己的專案量測：

1. 固定 SDK、組態與 CI 機器。
2. 區分 build、publish、啟動與測試執行時間。
3. 同時比較產物大小與維護成本。
4. 不用單次執行結果宣稱固定倍數。

## 與 xUnit 的基本對照

| 情境 | xUnit v3 | TUnit |
| --- | --- | --- |
| 一般測試 | `[Fact]` | `[Test]` |
| 參數化測試 | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| 斷言 | 同步呼叫為主 | `Assert.That(...)` 必須 await |
| 測試平台 | VSTest 或 MTP runner | MTP |
| 預設執行 | 依 collection 與 runner 設定 | 預設並行 |
| 測試發現 | runner／框架機制 | 預設 Source Generation |

不要因為語法短就立即遷移。既有專案若大量依賴 xUnit fixtures、custom attributes、第三方 extensions 或團隊工具鏈，先做相容性 spike，再估算完整遷移成本。

## 本篇驗證結果

範例執行結果：

```text
總計：48
成功：48
失敗：0
```

這 48 個測試涵蓋基本語法、arguments、assertions、TimeProvider、規則交界、生命週期與並行控制。Day28 會把資料來源與生命週期拆開深入處理。

## 重點整理

- TUnit 建立在 MTP 上，不使用 `Microsoft.NET.Test.Sdk`。
- Source Generation 與 Native AOT 有關，但不是同一件事。
- 同步測試合法；使用 TUnit assertions 時必須 `await`。
- `dotnet test` 與 `dotnet run` 都能執行 TUnit，各有適合情境。
- 預設並行執行要求測試先做好資源隔離。
- 框架選擇要看生態、相容性與維護成本，不只看 benchmark。

## 明日預告

Day28 會處理 `MethodDataSource`、`ClassDataSource`、Matrix Tests、生命週期 hooks 與 Dependency Injection，並檢查這些功能在預設並行模型下的共享範圍。

## 參考資源

- [TUnit 官方文件](https://tunit.dev/)
- [Installing TUnit](https://tunit.dev/docs/getting-started/installation/)
- [Writing your first test](https://tunit.dev/docs/getting-started/writing-your-first-test/)
- [Running your tests](https://tunit.dev/docs/getting-started/running-your-tests/)
- [Test Filters](https://tunit.dev/docs/execution/test-filters/)
- [Engine Modes](https://tunit.dev/docs/execution/engine-modes/)
- [從 xUnit 遷移](https://tunit.dev/docs/migration/xunit/)
- [Microsoft Testing Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十七天。**
