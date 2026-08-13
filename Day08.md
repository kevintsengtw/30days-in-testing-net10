---
day: 8
title: "Day 08 - 測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger"
sample: samples/day08
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging
  - Microsoft.Extensions.Logging.Abstractions
  - NSubstitute
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Microsoft.Testing.Extensions.TrxReport
---

# Day 08 - 測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger

<!-- toc -->

- [前言](#前言)
- [本日學習目標](#本日學習目標)
- [測試輸出需求分析](#測試輸出需求分析)
- [範例專案環境說明](#範例專案環境說明)
- [ITestOutputHelper 實用應用](#itestoutputhelper-實用應用)
- [ILogger 測試策略](#ilogger-測試策略)
- [實務整合應用](#實務整合應用)
- [診斷工具整合](#診斷工具整合)
- [今日重點回顧](#今日重點回顧)
- [本日小結](#本日小結)

<!-- /toc -->

## 前言

前幾篇已經涵蓋單元測試的核心技能：

- Day 01 建立測試金字塔的觀念基礎
- Day 02 打造第一個測試專案
- Day 03 深入 AAA 模式與 xUnit 框架
- Day 04 掌握各種斷言技巧
- Day 05 探索進階斷言與集合驗證
- Day 06 學會程式碼涵蓋率的實務應用
- Day 07 掌握相依替代與 NSubstitute 的使用

接下來是實務上的診斷問題：測試失敗時，如何快速定位原因？需要分析系統行為時，又該留下哪些資訊？本篇會處理測試輸出與記錄，讓失敗結果保留足夠的調查線索。

## 本日學習目標

- 理解測試輸出與記錄在診斷中的重要性
- 掌握 xUnit ITestOutputHelper 的正確使用方式與生命週期管理
- 學會設計結構化的測試輸出格式，提升可讀性
- 正確使用 ILogger 驗證記錄與行為
- 理解 ILogger 擴充方法的測試挑戰與解決方案
- 建立測試診斷工具與快速問題定位的技巧

## 測試輸出需求分析

### 為何需要測試輸出與記錄？

系統一複雜，測試輸出與記錄就變得很有用：

#### 1. 診斷需求

- 測試失敗時快速定位問題根源
- 理解測試執行過程中的狀態變化
- 驗證系統行為是否符合預期

#### 2. 除錯支援

- 提供測試執行的詳細軌跡
- 記錄重要變數與狀態資訊
- 協助開發者理解複雜的業務邏輯流程

#### 3. 可觀測性

- 建立系統行為的可見性
- 監控效能指標與資源使用
- 追蹤業務流程的執行情況

## 範例專案環境說明

本篇範例（`samples/day08`）使用 xUnit v3，執行平台是 **Microsoft Testing Platform（MTP）**。測試專案的 `csproj` 如下：

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <!-- 關閉 Microsoft.CodeCoverage 的 source root mapping:它每次建置都重寫檔案,會讓 IDE 專案系統反覆重新評估 -->
        <DisableMsCoverageReferencedPathMaps>true</DisableMsCoverageReferencedPathMaps>
        <OutputType>Exe</OutputType>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="AwesomeAssertions"/>
        <PackageReference Include="xunit.v3.mtp-v2"/>
        <!-- IDE 測試總管相容:Rider 的 xUnit 探索需要 Microsoft.NET.Test.Sdk,VSTest 路徑另需 xunit.runner.visualstudio -->
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="xunit.runner.visualstudio" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport"/>
        <PackageReference Include="NSubstitute"/>
        <PackageReference Include="Microsoft.Extensions.Logging"/>
        <PackageReference Include="Microsoft.Extensions.DependencyInjection"/>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\Day08.TestingLoggingOutput.Core\Day08.TestingLoggingOutput.Core.csproj"/>
    </ItemGroup>

</Project>
```

幾個重點：

- **`xunit.v3.mtp-v2` 3.2.2**：xUnit v3 對應 MTP v2 的 metapackage，框架與執行器一次到位。
- **`<OutputType>Exe</OutputType>`**：在 MTP 之下，測試專案是獨立的可執行檔，不再是類別庫。
- **`Microsoft.NET.Test.Sdk` 與 `xunit.runner.visualstudio`**：IDE 的支援還在過渡期，Visual Studio 與 Rider 的**測試總管**探索測試仍走 VSTest 路徑，純 MTP 專案在測試總管會顯示不出任何測試。所以範例採雙軌設定：命令列的 `dotnet test` 依 `global.json` 走 MTP，IDE 測試總管靠這兩個套件走 VSTest 探索，兩邊互不干擾。
- **`<DisableMsCoverageReferencedPathMaps>`**：關閉 Microsoft.CodeCoverage 每次建置都重寫 source root mapping 檔的行為，避免 IDE 專案系統反覆重新評估。
- **`Microsoft.Testing.Extensions.TrxReport`**：讓 MTP 能產出 TRX 測試報告。
- `PackageReference` 都不寫版本：版本集中在 `samples/day08/Directory.Packages.props`（CPM）管理。
- `samples/day08/global.json` 除了指定 SDK 版本，還宣告了 `"test": { "runner": "Microsoft.Testing.Platform" }`，讓 `dotnet test` 走 MTP 模式：

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

執行測試並產出 TRX 報告：

```shell
dotnet test --solution Day08.TestingLoggingOutput.sln --report-trx --report-trx-filename day08.trx
```

要留意的是，`dotnet test` 是依**目前工作目錄**解析 `global.json`，所以這條指令要在 `samples/day08` 目錄內執行；在別的位置跑會落回 VSTest 模式，不認得 `--report-trx` 這類 MTP 專屬參數。

## ITestOutputHelper 實用應用

### 基礎使用與正確注入

#### 為什麼需要使用 ITestOutputHelper？

在傳統的程式開發中，我們習慣使用 `Console.WriteLine()` 來輸出除錯資訊。但在 xUnit 測試環境中，這種方式有幾個問題：

1. **輸出不會顯示在測試結果中**：`Console.WriteLine()` 的輸出不會整合到測試報告裡
2. **並行執行時的混亂**：當多個測試並行執行時，console 輸出會混在一起
3. **測試失敗時缺乏上下文**：無法在測試失敗時看到相關的診斷資訊

#### ITestOutputHelper 的解決方案

xUnit 提供了 `ITestOutputHelper` 介面來解決這些問題：

- **測試隔離**：每個測試方法都有獨立的輸出通道
- **整合報告**：輸出會出現在測試結果和測試報告中
- **上下文保持**：測試失敗時可以看到相關的診斷資訊
- **並行安全**：不同測試的輸出不會互相干擾

#### 正確的注入方式

測試需要輸出訊息時，應從建構式注入 `ITestOutputHelper`。

順帶一提：xUnit v3 把 `ITestOutputHelper` 定義在 `Xunit` 命名空間，不再像 v2 一樣需要 `using Xunit.Abstractions;`。範例專案已在 `GlobalUsings.cs` 全域引入 `Xunit`、`AwesomeAssertions`、`NSubstitute` 與 `Microsoft.Extensions.Logging`，所以本文的程式碼區塊不用重複寫這些 using。

#### 實用範例

以下範例展示如何在複雜的業務邏輯測試中使用 `ITestOutputHelper` 來追蹤測試執行過程：

```csharp
public class ProductServiceTests
{
    private readonly ITestOutputHelper _output;

    public ProductServiceTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void CalculateDiscount_VIP客戶購買高價商品_應回傳20百分比折扣()
    {
        // Arrange
        var customer = new Customer { Type = CustomerType.VIP, PurchaseHistory = 15000 };
        var product = new Product { Price = 1000, Category = "Electronics" };

        _output.WriteLine($"Testing VIP customer: {customer.Type}, History: {customer.PurchaseHistory}");
        _output.WriteLine($"Product: {product.Category}, Price: {product.Price}");

        var service = new ProductService();

        // Act
        var discount = service.CalculateDiscount(customer, product);

        // Assert
        _output.WriteLine($"Calculated discount: {discount}%");
        discount.Should().Be(20); // VIP(10%) + 高價商品(5%) + 購買歷史(5%) = 20%
    }
}
```

#### 輸出結果示例

當測試執行時，這些輸出會出現在測試結果中：

```text
Testing VIP customer: VIP, History: 15000
Product: Electronics, Price: 1000
Calculated discount: 20%
```

這種輸出在測試失敗時特別有用，能夠快速了解測試執行時的狀態和資料。

### 生命週期管理與注意事項

**正確的生命週期管理**：

- `ITestOutputHelper` 的執行個體與每個測試方法綁定
- 每個測試方法執行時都會取得新的執行個體
- 不可在靜態方法或跨測試方法間共用

**常見誤區**：

```csharp
// X 錯誤：嘗試靜態存取
public static class TestHelper
{
    private static ITestOutputHelper _output; // 錯誤：靜態存取
    
    public static void LogInfo(string message)
    {
        _output.WriteLine(message); // 這會失敗
    }
}

// O 正確：透過相依性注入
public class TestHelper
{
    private readonly ITestOutputHelper _output;
    
    public TestHelper(ITestOutputHelper output)
    {
        _output = output;
    }
    
    public void LogInfo(string message)
    {
        _output.WriteLine(message);
    }
}
```

### 結構化輸出格式設計

為了提高測試輸出的可讀性，建議採用結構化的輸出格式：

```csharp
public class StructuredOutputTests
{
    private readonly ITestOutputHelper _output;

    public StructuredOutputTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void ProcessOrder_包含多項商品_應計算正確總額()
    {
        // Arrange
        LogSection("=== 測試設置 ===");
        var order = new Order
        {
            Items = new[]
            {
                new OrderItem { ProductName = "筆記型電腦", Price = 30000, Quantity = 1 },
                new OrderItem { ProductName = "滑鼠", Price = 800, Quantity = 2 },
                new OrderItem { ProductName = "鍵盤", Price = 1500, Quantity = 1 }
            }
        };

        LogOrderDetails(order);

        // Act
        LogSection("=== 執行測試 ===");
        var startTime = DateTime.Now;
        var totalAmount = order.Items.Sum(item => item.Price * item.Quantity);
        var endTime = DateTime.Now;

        LogPerformance(startTime, endTime);

        // Assert
        LogSection("=== 驗證結果 ===");
        _output.WriteLine($"計算總額: {totalAmount:C}");
        _output.WriteLine($"預期總額: {33100:C}");

        totalAmount.Should().Be(33100); // 30000 + 800*2 + 1500 = 33100
        LogSection("=== 測試完成 ===");
    }

    private void LogSection(string title)
    {
        _output.WriteLine(title);
    }

    private void LogOrderDetails(Order order)
    {
        _output.WriteLine("訂單明細:");
        foreach (var item in order.Items)
        {
            _output.WriteLine($"  - {item.ProductName}: {item.Price:C} x {item.Quantity}");
        }
    }

    private void LogPerformance(DateTime start, DateTime end)
    {
        var duration = end - start;
        _output.WriteLine($"執行時間: {duration.TotalMilliseconds:F2} ms");
    }
}
```

測試執行輸出結果：

```text
=== 測試設置 ===
訂單明細:
  - 筆記型電腦: NT$30,000.00 x 1
  - 滑鼠: NT$800.00 x 2
  - 鍵盤: NT$1,500.00 x 1
=== 執行測試 ===
執行時間: 1.86 ms
=== 驗證結果 ===
計算總額: NT$33,100.00
預期總額: NT$33,100.00
=== 測試完成 ===
```

### 效能測試中的時間點記錄

在效能測試中，時間點記錄是重要的診斷資訊：

```csharp
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public async Task ProcessLargeDataSet_處理一萬筆資料_應在五秒內完成()
    {
        // Arrange
        var dataSet = GenerateLargeDataSet(10000);
        var processor = new DataProcessor();

        var stopwatch = Stopwatch.StartNew();
        var checkpoints = new List<(string Stage, TimeSpan Elapsed)>();

        // Act & Monitor
        _output.WriteLine("開始處理大型資料集...");

        stopwatch.Restart();
        await processor.LoadData(dataSet);
        checkpoints.Add(("資料載入", stopwatch.Elapsed));
        _output.WriteLine($"資料載入完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        await processor.ValidateData();
        checkpoints.Add(("資料驗證", stopwatch.Elapsed));
        _output.WriteLine($"資料驗證完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        var result = await processor.ProcessData();
        checkpoints.Add(("資料處理", stopwatch.Elapsed));
        _output.WriteLine($"資料處理完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

        stopwatch.Stop();

        // Assert & Report
        _output.WriteLine("\n=== 效能報告 ===");
        foreach (var (stage, elapsed) in checkpoints)
        {
            _output.WriteLine($"{stage}: {elapsed.TotalMilliseconds:F2} ms");
        }

        var totalTime = stopwatch.Elapsed;
        _output.WriteLine($"總執行時間: {totalTime.TotalMilliseconds:F2} ms");

        // 驗證效能要求（例如：5秒內完成）
        totalTime.Should().BeLessThan(TimeSpan.FromSeconds(5));
        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(10000);
    }

    private static IEnumerable<string> GenerateLargeDataSet(int count)
    {
        return Enumerable.Range(1, count).Select(i => $"Data-{i:D6}");
    }
}
```

## ILogger 測試策略

### 從自訂記錄介面到標準 ILogger

實際專案通常使用 `Microsoft.Extensions.Logging.ILogger`，而不是自訂記錄介面。以下用電商訂單處理服務示範測試方式：

```csharp
public class OrderProcessingService
{
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    public OrderProcessingService(
        ILogger<OrderProcessingService> logger,
        IInventoryService inventoryService,
        IPaymentService paymentService)
    {
        _logger = logger;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
    }

    public OrderResult ProcessOrder(Order order)
    {
        _logger.LogInformation("開始處理訂單 {OrderId} for customer {CustomerId}", order.Id, order.CustomerId);

        // 檢查庫存
        var stockAvailable = _inventoryService.CheckStock(order.ProductId, order.Quantity);
        if (!stockAvailable)
        {
            _logger.LogWarning("商品 {ProductId} 庫存不足，數量需求：{RequestedQuantity}",
                               order.ProductId, order.Quantity);
            return new OrderResult { Success = false, ErrorMessage = "庫存不足" };
        }

        // 處理付款
        var paymentResult = _paymentService.ProcessPayment(order.TotalAmount);
        if (!paymentResult.Success)
        {
            _logger.LogError("訂單 {OrderId} 付款失敗：{ErrorMessage}",
                             order.Id, paymentResult.ErrorMessage);
            return new OrderResult { Success = false, ErrorMessage = "付款失敗" };
        }

        _logger.LogInformation("訂單 {OrderId} 處理完成，金額：{Amount}", order.Id, order.TotalAmount);
        return new OrderResult { Success = true, OrderId = order.Id };
    }
}
```

### 記錄層級驗證技巧

#### 挑戰：擴充方法的測試問題

`ILogger.LogError()` 是擴充方法，NSubstitute 無法直接攔截，需要攔截底層的 `Log<TState>` 方法：

```csharp
// X 這種方式會失敗
[Fact]
public void ProcessOrder_付款失敗_應記錄錯誤_錯誤示範()
{
    // Arrange
    var logger = Substitute.For<ILogger<OrderProcessingService>>();
    // ...setup...

    // Act
    var result = sut.ProcessOrder(order);

    // Assert - 這會拋出 RedundantArgumentMatcherException
    logger.Received().LogError(Arg.Is<string>(x => x.Contains("付款失敗")));
}

// O 正確的方式：攔截底層方法
[Fact]
public void ProcessOrder_付款失敗_應記錄錯誤_正確示範()
{
    // Arrange
    var logger = Substitute.For<ILogger<OrderProcessingService>>();
    // ...setup...

    // Act
    var result = sut.ProcessOrder(order);

    // Assert
    logger.Received().Log(
        LogLevel.Error,
        Arg.Any<EventId>(),
        Arg.Any<object>(),
        Arg.Any<Exception>(),
        Arg.Any<Func<object, Exception?, string>>());
}
```

### 避免對記錄框架的直接相依

為了簡化測試並避免複雜的 `Log<TState>` 驗證，我們可以建立抽象層：

```csharp
public abstract class AbstractLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Log(logLevel, exception, formatter(state, exception));
    }

    public abstract void Log(LogLevel logLevel, Exception? ex, string information);
}
```

使用抽象 Logger 簡化測試：

```csharp
public class OrderProcessingServiceTests
{
    private readonly AbstractLogger<OrderProcessingService> _logger;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    public OrderProcessingServiceTests()
    {
        _logger = Substitute.For<AbstractLogger<OrderProcessingService>>();
        _inventoryService = Substitute.For<IInventoryService>();
        _paymentService = Substitute.For<IPaymentService>();
    }

    [Fact]
    public void ProcessOrder_正常處理_應記錄開始與完成訊息()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD001",
            CustomerId = "CUST001",
            ProductId = "PROD001",
            Quantity = 2,
            TotalAmount = 1000
        };

        _inventoryService.CheckStock(order.ProductId, order.Quantity).Returns(true);
        _paymentService.ProcessPayment(order.TotalAmount)
                       .Returns(new PaymentResult { Success = true });

        var sut = new OrderProcessingService(_logger, _inventoryService, _paymentService);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        result.Success.Should().BeTrue();

        // 驗證記錄了開始處理訊息
        _logger.Received().Log(
            logLevel: LogLevel.Information,
            ex: null,
            information: Arg.Is<string>(msg => msg.Contains("開始處理訂單") && msg.Contains("ORD001")));

        // 驗證記錄了完成訊息
        _logger.Received().Log(
            logLevel: LogLevel.Information,
            ex: null,
            information: Arg.Is<string>(msg => msg.Contains("處理完成") && msg.Contains("1000")));
    }

    [Fact]
    public void ProcessOrder_庫存不足_應記錄警告訊息()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD002",
            ProductId = "PROD002",
            Quantity = 5
        };

        _inventoryService.CheckStock(order.ProductId, order.Quantity).Returns(false);

        var sut = new OrderProcessingService(_logger, _inventoryService, _paymentService);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("庫存不足");

        _logger.Received().Log(
            logLevel: LogLevel.Warning,
            ex: null,
            information: Arg.Is<string>(msg => msg.Contains("庫存不足") && msg.Contains("PROD002")));
    }

    [Fact]
    public void ProcessOrder_付款失敗_應記錄錯誤訊息()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD003",
            ProductId = "PROD003",
            Quantity = 1,
            TotalAmount = 500
        };

        _inventoryService.CheckStock(order.ProductId, order.Quantity).Returns(true);
        _paymentService.ProcessPayment(order.TotalAmount)
                       .Returns(new PaymentResult { Success = false, ErrorMessage = "信用卡驗證失敗" });

        var sut = new OrderProcessingService(_logger, _inventoryService, _paymentService);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("付款失敗");

        _logger.Received().Log(
            logLevel: LogLevel.Error,
            ex: null,
            information: Arg.Is<string>(msg => msg.Contains("付款失敗") && msg.Contains("ORD003")));
    }
}
```

測試裡用到的 `Order`、`OrderResult`、`PaymentResult` 模型與 `IInventoryService`、`IPaymentService` 介面，都定義在範例專案的 `Day08.TestingLoggingOutput.Core` 裡（`Models/` 與 `Interface/` 目錄），完整定義請直接看 `samples/day08`。

## 實務整合應用

### DI 容器中的 Logger 注入與測試

實際應用通常由 DI 容器注入 Logger，測試時要另外設定：

```csharp
public class OrderProcessingIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public OrderProcessingIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;

        var services = new ServiceCollection();

        // 設定測試用的 Logger
        services.AddLogging(builder => { builder.AddProvider(new XUnitLoggerProvider(testOutputHelper)); });

        // 註冊業務服務
        services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
        services.AddScoped<IPaymentService, MockPaymentService>();
        services.AddScoped<OrderProcessor>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProcessOrderAsync_完整訂單流程_應記錄所有步驟()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();

        var order = new Order
        {
            Id = "ORDER-" + Guid.NewGuid().ToString("N")[..8],
            CustomerId = "CUST001",
            Items = new[]
            {
                new OrderItem { ProductId = "P001", ProductName = "測試商品", Quantity = 2, Price = 100 }
            }
        };

        _output.WriteLine($"=== 測試訂單處理流程 ===");
        _output.WriteLine($"訂單編號: {order.Id}");
        _output.WriteLine($"客戶編號: {order.CustomerId}");

        // Act
        var result = await processor.ProcessOrderAsync(order);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OrderId.Should().Be(order.Id);
        result.TotalAmount.Should().Be(200); // 2 * 100

        _output.WriteLine($"=== 測試完成 ===");
        _output.WriteLine($"處理結果: {(result.Success ? "成功" : "失敗")}");
        _output.WriteLine($"訂單金額: {result.TotalAmount:C}");

        // Logger 輸出會顯示在測試結果中，包含完整的處理流程
    }
}
```

測試執行輸出結果：

```text
=== 測試訂單處理流程 ===
訂單編號: ORDER-c9a67618
客戶編號: CUST001
[11:15:02.774] [Information] [Day08.TestingLoggingOutput.Core.Services.OrderProcessor] 開始非同步處理訂單 ORDER-c9a67618
[11:15:02.787] [Information] [Day08.TestingLoggingOutput.Tests.Integration.InMemoryOrderRepository] 儲存訂單 ORDER-c9a67618
[11:15:02.849] [Information] [Day08.TestingLoggingOutput.Tests.Integration.InMemoryOrderRepository] 訂單 ORDER-c9a67618 儲存成功
[11:15:02.850] [Information] [Day08.TestingLoggingOutput.Core.Services.OrderProcessor] 訂單 ORDER-c9a67618 已儲存
[11:15:02.851] [Information] [Day08.TestingLoggingOutput.Tests.Integration.MockPaymentService] 處理付款，金額：NT$200.00
[11:15:02.851] [Information] [Day08.TestingLoggingOutput.Tests.Integration.MockPaymentService] 付款成功，交易編號：7ef2e914
[11:15:02.851] [Information] [Day08.TestingLoggingOutput.Core.Services.OrderProcessor] 訂單 ORDER-c9a67618 處理完成
=== 測試完成 ===
處理結果: 成功
訂單金額: NT$200.00
```

### 記錄內容的斷言策略

針對記錄內容進行精確斷言，同時確認敏感資料沒有進到記錄裡：

```csharp
/// <summary>
/// 結構化記錄測試範例
/// </summary>
public class PaymentServiceTests
{
    [Fact]
    public void ProcessPayment_付款失敗交易_應記錄結構化資料()
    {
        // Arrange
        var paymentRequest = new PaymentRequest
        {
            Amount = 1000,
            Currency = "TWD",
            CardNumber = "****-****-****-1234"
        };

        var mockLogger = new TestLogger<PaymentService>();
        var service = new PaymentService(mockLogger);

        // Act
        var result = service.ProcessPayment(paymentRequest);

        // Assert
        result.Success.Should().BeFalse();

        // 驗證記錄內容
        var errorLogs = mockLogger.GetLogs(LogLevel.Error);
        errorLogs.Count.Should().Be(1);

        var errorLog = errorLogs.First();
        errorLog.Message.Should().Contain("Payment processing failed");

        // 驗證敏感資料未被記錄
        errorLog.Message.Should().NotContain("1234");
    }
}
```

**TestLogger** - 測試用 Logger，支援記錄收集與驗證

```csharp
public class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _logs = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new NoOpDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    /// <typeparam name="TState"></typeparam>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
            State = state as IEnumerable<KeyValuePair<string, object>>,
            Exception = exception
        });
    }

    /// <summary>
    /// 取得記錄
    /// </summary>
    /// <param name="level">記錄層級</param>
    /// <returns></returns>
    public IList<LogEntry> GetLogs(LogLevel? level = null)
    {
        return level.HasValue ? _logs.Where(l => l.Level == level).ToList() : _logs.ToList();
    }

    /// <summary>
    /// 清除所有記錄
    /// </summary>
    public void ClearLogs()
    {
        _logs.Clear();
    }
}

/// <summary>
/// 記錄項目
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 記錄層級
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 記錄狀態
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? State { get; set; }

    /// <summary>
    /// 例外資訊
    /// </summary>
    public Exception? Exception { get; set; }
}
```

### 非同步記錄的測試挑戰

非同步記錄需要特別的測試處理：

```csharp
/// <summary>
/// 非同步記錄測試範例
/// </summary>
public class AsyncLoggingTests
{
    [Fact]
    public async Task ProcessAsync_非同步處理_應記錄開始和完成訊息()
    {
        // Arrange
        var mockLogger = new ConcurrentTestLogger<AsyncLoggingService>();
        var service = new AsyncLoggingService(mockLogger);

        // Act
        await service.ProcessAsync("test-data");

        // 等待背景記錄完成
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert
        var logs = mockLogger.GetLogs();
        logs.Count.Should().BeGreaterThanOrEqualTo(1);
        logs.Should().Contain(l => l.Message.Contains("開始處理資料"));
    }
}
```

留意這裡的 `Task.Delay` 多帶了一個 `TestContext.Current.CancellationToken`。xUnit v3 提供 `TestContext.Current` 讓測試程式碼取得目前測試的執行情境，其中的 `CancellationToken` 會在測試被取消（例如逾時）時發出訊號。只要呼叫的方法接受 CancellationToken，就建議把這個 token 帶進去，測試取消時才能立即中斷等待，而不是傻等 `Task.Delay` 跑完；v3 內建的 analyzer 也會用 **xUnit1051** 規則提醒你這件事。

另外，背景記錄（`Task.Run` 裡的那一筆）什麼時候寫進來沒有保證，所以斷言只驗證同步路徑一定會有的「開始處理資料」；如果連背景那筆也要驗，等待就得改用輪詢或同步機制，而不是賭一個固定的延遲時間。

```csharp
/// <summary>
/// class AsyncLoggingService - 非同步記錄服務（用於測試）
/// </summary>
public class AsyncLoggingService
{
    private readonly ILogger<AsyncLoggingService>? _logger;

    /// <summary>
    /// AsyncLoggingService 建構子
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AsyncLoggingService(ILogger<AsyncLoggingService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 處理非同步資料
    /// </summary>
    /// <param name="data">要處理的資料</param>
    public async Task ProcessAsync(string data)
    {
        _logger?.LogInformation("開始處理資料: {Data}", data);

        // 模擬非同步處理
        await Task.Delay(100);

        // 背景記錄
        _ = Task.Run(() => _logger?.LogInformation("資料處理完成: {Data}", data));
    }
}
```

**ConcurrentTestLogger** - 並行測試用 Logger

```csharp
public class ConcurrentTestLogger<T> : ILogger<T>
{
    private readonly ConcurrentBag<LogEntry> _logs = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new NoOpDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    /// <typeparam name="TState"></typeparam>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
            State = state as IEnumerable<KeyValuePair<string, object>>,
            Exception = exception
        });
    }

    /// <summary>
    /// 取得記錄
    /// </summary>
    /// <param name="level">記錄層級</param>
    /// <returns></returns>
    public IList<LogEntry> GetLogs(LogLevel? level = null)
    {
        var allLogs = _logs.ToList();
        return level.HasValue ? allLogs.Where(l => l.Level == level).ToList() : allLogs;
    }
}
```

## 診斷工具整合

### 為何需要 XUnitLogger 與 CompositeLogger

在實際測試中，我們經常面臨一個兩難問題：

1. **Mock Logger 驗證**：我們需要驗證程式碼是否正確呼叫了記錄方法
2. **測試診斷需求**：當測試失敗時，我們希望在測試輸出中看到實際的記錄訊息

一般的做法是二選一，但這兩件事可以同時做到，作法就是自己接一個 XUnitLogger 和 CompositeLogger。

#### 問題背景

ASP.NET Core 測試預設看不到 Logger 訊息，失敗時少了一條除錯線索；直接使用真實 Logger 也不方便驗證呼叫行為。

#### 解決方案設計理念

- **XUnitLogger**：將 ILogger 的訊息導向 xUnit 的測試輸出
- **CompositeLogger**：組合多個 Logger，同時支援行為驗證與測試輸出

### 參考資料

這個解決方案參考了多個社群的最佳實務：

1. [Unit-testing ILogger in ASP.NET Core](https://whuysentruit.medium.com/unit-testing-ilogger-in-asp-net-core-9a2d066d0fb8)

2. [如何在單元測試中優雅地 Mock ILogger | Opass: A Life Well Lived](https://www.opasschang.com/docs/how-to-mock-ilogger-elegantly-in-unit-test)

3. [How to get ASP.NET Core logs in the output of xUnit tests - Meziantou's blog](https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm)

### XUnitLogger 與 CompositeLogger 實作

當我們既要驗證記錄行為，又要在測試輸出中看到記錄訊息時，可以使用組合模式：

#### XUnitLogger 實作目的

`XUnitLogger` 會將 `ILogger` 的輸出重新導向到 xUnit 測試輸出，測試執行時就能看到實際的記錄訊息。除錯複雜業務邏輯時，這些訊息通常比單看失敗的斷言更有線索。

#### CompositeLogger 組合模式

`CompositeLogger` 採用組合設計模式，允許我們同時使用多個 Logger 實作。典型的使用情境是結合 Mock Logger（用於行為驗證）和 XUnit Logger（用於測試輸出診斷）。

```csharp
public class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName;
    private readonly LoggerExternalScopeProvider _scopeProvider;

    public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider)
    {
        _testOutputHelper = testOutputHelper;
        _categoryName = typeof(T).Name;
        _scopeProvider = scopeProvider;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider.Push(state);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logLine += $"\n{exception}";
        }

        _testOutputHelper.WriteLine(logLine);
    }
}

public class CompositeLogger<T> : ILogger<T>
{
    private readonly ILogger<T>[] _loggers;

    public CompositeLogger(params ILogger<T>[] loggers)
    {
        _loggers = loggers;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _loggers.Any(logger => logger.IsEnabled(logLevel));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var scopes = _loggers.Select(logger => logger.BeginScope(state)).ToArray();
        return new CompositeDisposable(scopes);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}

public class CompositeDisposable : IDisposable
{
    private readonly IDisposable?[] _disposables;

    public CompositeDisposable(IDisposable?[] disposables)
    {
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
    }
}
```

測試組合 Logger：

```csharp
public class OrderProcessingAdvancedTests
{
    private readonly AbstractLogger<OrderProcessingService> _mockLogger;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<OrderProcessingService> _compositeLogger;
    
    public OrderProcessingAdvancedTests(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
        _mockLogger = Substitute.For<AbstractLogger<OrderProcessingService>>();

        var xunitLogger = new XUnitLogger<OrderProcessingService>(testOutputHelper, new LoggerExternalScopeProvider());
        _compositeLogger = new CompositeLogger<OrderProcessingService>(_mockLogger, xunitLogger);
    }

    [Fact]
    public void ProcessOrder_付款失敗_應記錄錯誤並輸出到測試結果()
    {
        // Arrange
        var inventoryService = Substitute.For<IInventoryService>();
        var paymentService = Substitute.For<IPaymentService>();

        var order = new Order
        {
            Id = "ORD004",
            ProductId = "PROD004",
            Quantity = 1,
            TotalAmount = 2000
        };

        inventoryService.CheckStock(order.ProductId, order.Quantity).Returns(true);
        paymentService.ProcessPayment(order.TotalAmount)
                      .Returns(new PaymentResult { Success = false, ErrorMessage = "餘額不足" });

        var sut = new OrderProcessingService(_compositeLogger, inventoryService, paymentService);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        result.Success.Should().BeFalse();

        // 驗證 Mock Logger（行為驗證）
        _mockLogger.Received().Log(
            logLevel: LogLevel.Error,
            ex: null,
            information: Arg.Is<string>(msg => msg.Contains("付款失敗") && msg.Contains("ORD004")));

        // XUnit Logger 會自動將訊息輸出到測試結果中，方便除錯
        // 輸出格式：[HH:mm:ss.fff] [Error] [OrderProcessingService] 訂單 ORD004 付款失敗：餘額不足
    }
}
```

測試執行輸出結果：

```text
[11:30:03.826] [Information] [OrderProcessingService] 開始處理訂單 ORD004 for customer 
[11:30:03.838] [Error] [OrderProcessingService] 訂單 ORD004 付款失敗：餘額不足
```

### 測試失敗時的快速問題定位

建立標準化的診斷輸出模式：

DiagnosticTestBase - 診斷測試基底類別

```csharp
public class DiagnosticTestBase
{
    protected readonly ITestOutputHelper Output;

    protected DiagnosticTestBase(ITestOutputHelper testOutputHelper)
    {
        Output = testOutputHelper;
    }

    protected void LogTestContext(string testName, object? testData = null)
    {
        Output.WriteLine($"=== {testName} ===");
        Output.WriteLine($"執行時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        if (testData != null)
        {
            Output.WriteLine($"測試資料: {JsonSerializer.Serialize(testData, new JsonSerializerOptions { WriteIndented = true })}");
        }

        Output.WriteLine("");
    }

    protected void LogException(Exception ex, string context = "")
    {
        Output.WriteLine($"=== 例外發生 {context} ===");
        Output.WriteLine($"例外類型: {ex.GetType().Name}");
        Output.WriteLine($"例外訊息: {ex.Message}");
        Output.WriteLine($"堆疊追蹤:\n{ex.StackTrace}");
        Output.WriteLine("");
    }

    protected void LogAssertionFailure(string expected, string actual, string fieldName)
    {
        Output.WriteLine($"=== 斷言失敗 ===");
        Output.WriteLine($"欄位: {fieldName}");
        Output.WriteLine($"預期值: {expected}");
        Output.WriteLine($"實際值: {actual}");
        Output.WriteLine("");
    }
}
```

ProductServiceDiagnosticTests - 商品服務診斷測試範例

```csharp
public class ProductServiceDiagnosticTests : DiagnosticTestBase
{
    /// <summary>
    /// ProductServiceDiagnosticTests 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
    /// <returns></returns>
    public ProductServiceDiagnosticTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public void CalculateTotalPrice_複雜折扣情境_應處理所有折扣計算()
    {
        try
        {
            // Arrange
            var testData = new
            {
                Customer = new { Type = "VIP", PurchaseHistory = 50000 },
                Items = new[]
                {
                    new { Name = "筆電", Price = 30000, Quantity = 1 },
                    new { Name = "滑鼠", Price = 1000, Quantity = 2 }
                },
                CouponCode = "SUMMER2024"
            };

            LogTestContext(nameof(CalculateTotalPrice_複雜折扣情境_應處理所有折扣計算), testData);

            var service = new ProductService();
            var customer = new Customer { Type = CustomerType.VIP, PurchaseHistory = 50000 };
            var items = new[]
            {
                new OrderItem { ProductName = "筆電", Price = 30000, Quantity = 1 },
                new OrderItem { ProductName = "滑鼠", Price = 1000, Quantity = 2 }
            };
            var couponCode = "SUMMER2024";

            // Act
            Output.WriteLine("開始執行價格計算...");
            var result = service.CalculateTotalPrice(customer, items, couponCode);
            Output.WriteLine($"計算結果: {result.TotalPrice:C}");

            // Assert
            var expectedPrice = 27200m; // 原價 32000 - VIP折扣 4800 - 優惠券折扣 3200 = 24000

            if (result.TotalPrice != expectedPrice)
            {
                LogAssertionFailure($"{expectedPrice:C}", $"{result.TotalPrice:C}", "TotalPrice");

                // 輸出詳細的計算過程
                Output.WriteLine("=== 計算明細 ===");
                Output.WriteLine($"原始金額: {result.OriginalAmount:C}");
                Output.WriteLine($"VIP 折扣: {result.VipDiscount:C}");
                Output.WriteLine($"優惠券折扣: {result.CouponDiscount:C}");
                Output.WriteLine($"最終金額: {result.TotalPrice:C}");
            }

            // 預期值：32000 - 4800 - 3200 = 24000
            result.TotalPrice.Should().Be(24000);
        }
        catch (Exception ex)
        {
            LogException(ex, "價格計算測試");
            throw;
        }
    }
}
```

測試執行輸出結果：

```text
=== CalculateTotalPrice_複雜折扣情境_應處理所有折扣計算 ===
執行時間: 2025-08-17 11:32:46.332
測試資料: {
  "Customer": {
    "Type": "VIP",
    "PurchaseHistory": 50000
  },
  "Items": [
    {
      "Name": "\u7B46\u96FB",
      "Price": 30000,
      "Quantity": 1
    },
    {
      "Name": "\u6ED1\u9F20",
      "Price": 1000,
      "Quantity": 2
    }
  ],
  "CouponCode": "SUMMER2024"
}

開始執行價格計算...
計算結果: NT$24,000.00
=== 斷言失敗 ===
欄位: TotalPrice
預期值: NT$27,200.00
實際值: NT$24,000.00

=== 計算明細 ===
原始金額: NT$32,000.00
VIP 折扣: NT$4,800.00
優惠券折扣: NT$3,200.00
最終金額: NT$24,000.00
```

## 今日重點回顧

### DO - 建議做法

1. **適當使用 ITestOutputHelper**
   - 在複雜測試中記錄重要步驟
   - 效能測試中記錄時間點
   - 測試失敗時提供診斷資訊

2. **Logger 測試策略**
   - 使用抽象層簡化測試
   - 驗證重要的記錄行為
   - 結合 Mock 與實際輸出

3. **結構化輸出**
   - 採用一致的輸出格式
   - 包含時間戳記與分類
   - 提供足夠的上下文資訊

### DON'T - 避免做法

1. **不要過度使用輸出**
   - 避免在每個測試中都大量輸出
   - 不要記錄過於細節的資訊
   - 避免影響測試執行效能

2. **不要硬編碼記錄驗證**
   - 避免驗證完整的記錄訊息
   - 不要依賴記錄訊息的確切格式
   - 避免測試內部實作細節

3. **不要忽略生命週期**
   - 不要在靜態方法中使用 ITestOutputHelper
   - 不要跨測試方法共用執行個體
   - 避免在非同步測試中遺漏等待

## 本日小結

本篇把測試輸出與記錄分成幾種用法：

### ITestOutputHelper 核心技術

- **正確的注入方式**：從建構式注入 ITestOutputHelper，避免靜態存取陷阱
- **生命週期管理**：理解每個測試方法都有獨立的執行個體，不可跨測試方法共用
- **結構化輸出設計**：建立一致的輸出格式，包含章節標題、時間戳記與分類資訊
- **效能測試整合**：在效能測試中記錄重要時間點，提供詳細的執行軌跡

### ILogger 測試策略與挑戰

- **擴充方法的測試限制**：LogError 等擴充方法無法直接 Mock，需要攔截底層 Log 方法
- **AbstractLogger 抽象層**：建立簡化的抽象層，避免複雜的泛型方法驗證
- **行為驗證技巧**：掌握記錄層級、訊息內容與呼叫次數的驗證方法
- **結構化記錄測試**：驗證記錄內容的同時確保敏感資料不被記錄

### 進階診斷工具整合

- **CompositeLogger 模式**：同時支援行為驗證與測試輸出的組合設計
- **XUnitLogger 實作**：將 ILogger 訊息導向測試輸出，提升除錯效率
- **DI 容器整合**：在整合測試中正確設定 Logger 提供者
- **非同步記錄處理**：使用 ConcurrentTestLogger 處理背景記錄的測試挑戰

### 實務應用建議

- **測試診斷標準化**：建立統一的診斷輸出模式，包含測試資料、例外資訊與斷言失敗詳情
- **問題定位技巧**：用結構化輸出定位測試失敗的原因
- **最佳實務平衡**：在測試效率與診斷能力之間找到適當平衡點

把記錄接進測試輸出後，失敗時可以直接看到執行脈絡，不必只靠斷言訊息猜原因。

明天我們將瞭解單元測試對於 Private 與 Internal 的測試策略。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day08>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第八天。明天會介紹 Day 09：測試私有與內部成員 - Private 與 Internal 的測試策略。**
