# Day 08：測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger

## 專案概述

本專案展示如何在單元測試中有效使用測試輸出與記錄功能，包括 xUnit 的 `ITestOutputHelper` 和 .NET 的 `ILogger` 測試策略。透過實際的電商訂單處理情境，學習如何建立具備可觀測性與診斷能力的測試。

## 學習目標

- 掌握 `ITestOutputHelper` 的正確使用方式與生命週期管理
- 學會設計結構化的測試輸出格式，提升可讀性
- 熟練使用 `ILogger` 進行測試中的記錄驗證與行為測試
- 理解 `ILogger` 擴充方法的測試挑戰與解決方案
- 建立測試診斷工具與快速問題定位的技巧

## 專案結構

```text
Day08.TestingLoggingOutput/
├── Day08.TestingLoggingOutput.sln
├── Directory.Packages.props                      # 中央套件版本管理（CPM）
├── global.json                                   # SDK 版本與 MTP runner 設定
├── README.md
├── src/
│   └── Day08.TestingLoggingOutput.Core/          # 主要專案
│       ├── Interface/                            # 服務介面定義
│       │   ├── IInventoryService.cs
│       │   ├── IOrderRepository.cs
│       │   └── IPaymentService.cs
│       ├── Logging/                              # 記錄相關
│       │   └── AbstractLogger.cs                 # 抽象 Logger 基底類別
│       ├── Models/                               # 模型類別
│       │   ├── Customer.cs / CustomerType.cs     # 客戶模型
│       │   ├── Order.cs / OrderItem.cs / OrderResult.cs # 訂單模型
│       │   ├── PaymentRequest.cs / PaymentResult.cs # 付款相關模型
│       │   └── Product.cs                        # 商品模型
│       └── Services/                             # 業務邏輯服務
│           ├── ProductService.cs                 # 商品服務
│           ├── OrderProcessingService.cs         # 訂單處理服務
│           ├── OrderProcessor.cs                 # 訂單處理器（DI 整合範例用）
│           ├── DataProcessor.cs                  # 資料處理器
│           └── DataProcessingResult.cs / PriceCalculationResult.cs # 結果模型
└── tests/
    └── Day08.TestingLoggingOutput.Tests/         # 測試專案
        ├── TestOutputHelper/                     # ITestOutputHelper 測試
        │   └── ProductServiceTests.cs            # 商品服務測試與診斷範例
        ├── LoggerTests/                          # ILogger 測試
        │   └── OrderProcessingServiceTests.cs    # 訂單處理服務記錄測試
        ├── Logging/                              # 測試用 Logger 工具
        │   └── TestLoggers.cs                    # 測試用 Logger 實作
        └── Integration/                          # 整合測試
            └── OrderProcessingIntegrationTests.cs # 訂單處理整合測試
```

## 使用的套件與版本

### 主要專案 (Day08.TestingLoggingOutput.Core)

- `Microsoft.Extensions.Logging.Abstractions` 10.0.10
- `Microsoft.Extensions.DependencyInjection` 10.0.10

### 測試專案 (Day08.TestingLoggingOutput.Tests)

- `xunit.v3.mtp-v2` 3.2.2
- `Microsoft.NET.Test.Sdk` 18.8.1（IDE 測試總管相容，雙軌設定）
- `xunit.runner.visualstudio` 3.1.5（IDE 測試總管相容，雙軌設定）
- `Microsoft.Testing.Extensions.TrxReport` 2.3.3
- `AwesomeAssertions` 9.5.0
- `NSubstitute` 5.3.0
- `Microsoft.Extensions.Logging` 10.0.10
- `Microsoft.Extensions.DependencyInjection` 10.0.10

## 執行方式

### 建置專案

```bash
dotnet build
```

### 執行所有測試

```bash
dotnet test
```

### 執行特定測試類別

```bash
# 執行 ITestOutputHelper 相關測試
dotnet test --filter-namespace "*.TestOutputHelper"

# 執行 ILogger 相關測試
dotnet test --filter-namespace "*.LoggerTests"

# 執行整合測試
dotnet test --filter-namespace "*.Integration"
```

### 產出 TRX 測試報告

```bash
dotnet test --solution Day08.TestingLoggingOutput.sln --report-trx --report-trx-filename day08.trx
```

> 以上指令都要在 `samples/day08` 目錄內執行——`dotnet test` 依目前工作目錄解析 `global.json`，在別的位置跑會落回 VSTest 模式。MTP 模式不使用 VSTest 的 `--verbosity`、`--logger` 參數。

## 重點學習內容

### 1. ITestOutputHelper 基礎應用

#### 正確的注入方式

```csharp
public class ProductServiceTests
{
    private readonly ITestOutputHelper _output;

    public ProductServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CalculateDiscount_VIP客戶購買高價商品_應回傳20百分比折扣()
    {
        // 在測試中記錄重要資訊
        _output.WriteLine($"Testing VIP customer: {customer.Type}");
        _output.WriteLine($"Product: {product.Category}, Price: {product.Price}");
        _output.WriteLine($"Calculated discount: {discount}%");
    }
}
```

#### 結構化輸出格式

- 使用一致的章節標題格式
- 記錄測試資料與執行時間
- 提供詳細的診斷資訊

#### 效能測試整合

- 記錄關鍵時間點
- 追蹤執行階段效能
- 建立效能報告

### 2. ILogger 測試策略

#### AbstractLogger 抽象層

```csharp
public abstract class AbstractLogger<T> : ILogger<T>
{
    public abstract void Log(LogLevel logLevel, Exception? ex, string information);
    // 簡化複雜的泛型 Log 方法
}
```

#### 記錄行為驗證

```csharp
// 驗證記錄層級與訊息內容
_logger.Received().Log(
    logLevel: LogLevel.Error, 
    ex: null, 
    information: Arg.Is<string>(msg => msg.Contains("付款失敗")));
```

#### CompositeLogger 組合模式

- 同時支援行為驗證與測試輸出
- 結合 Mock Logger 與 XUnit Logger
- 提供完整的測試診斷能力

### 3. 診斷工具與最佳實踐

#### 測試診斷基底類別

```csharp
public class DiagnosticTestBase
{
    protected void LogTestContext(string testName, object? testData = null);
    protected void LogException(Exception ex, string context = "");
    protected void LogAssertionFailure(string expected, string actual, string fieldName);
}
```

#### 快速問題定位

- 標準化的診斷輸出模式
- 結構化的錯誤報告
- 完整的測試執行軌跡

### 4. 整合測試與 DI 容器

#### Logger 提供者設定

```csharp
services.AddLogging(builder =>
{
    builder.AddProvider(new XUnitLoggerProvider(output));
});
```

#### 完整的測試流程記錄

- 整合真實的 Logger 輸出
- 追蹤跨服務的執行流程
- 提供端到端的可觀測性

## 常見問題與解決方案

### Q: 為什麼不能直接 Mock ILogger.LogError？

A: `LogError` 是擴充方法，NSubstitute 無法直接攔截。需要攔截底層的 `Log<TState>` 方法或使用 `AbstractLogger` 抽象層。

### Q: 如何在測試失敗時快速定位問題？

A: 使用結構化的輸出格式，記錄測試資料、執行步驟與關鍵狀態，建立診斷基底類別統一處理。

### Q: 非同步記錄如何測試？

A: 使用 `ConcurrentTestLogger` 處理並行記錄，並在驗證前適當等待背景任務完成。

### Q: 如何平衡測試效率與診斷能力？

A: 只在複雜測試或失敗情境中使用詳細輸出，避免在每個測試中都大量記錄。

## 實務應用建議

1. **適當使用 ITestOutputHelper**
   - 在複雜測試中記錄關鍵步驟
   - 效能測試中記錄時間點
   - 測試失敗時提供診斷資訊

2. **Logger 測試策略**
   - 使用抽象層簡化測試
   - 驗證關鍵的記錄行為
   - 結合 Mock 與實際輸出

3. **建立診斷工具**
   - 標準化輸出格式
   - 統一錯誤處理
   - 提供快速問題定位能力

這個專案展示了如何建立具備完整可觀測性的測試，讓測試不再是黑盒子，而是強大的診斷工具。
