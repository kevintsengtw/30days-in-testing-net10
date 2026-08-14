---
day: 28
title: "Day 28 - TUnit 進階：資料來源、生命週期與 Dependency Injection"
sample: samples/day28
target_framework: net10.0
packages:
  - AutoFixture
  - Microsoft.Extensions.DependencyInjection
  - Microsoft.Extensions.Logging.Abstractions
  - TUnit
---

# Day 28 - TUnit 進階：資料來源、生命週期與 Dependency Injection

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [範例專案](#範例專案)
- [先選對資料來源](#先選對資料來源)
- [MethodDataSource](#methoddatasource)
- [ClassDataSource](#classdatasource)
- [Matrix Tests](#matrix-tests)
- [測試生命週期](#測試生命週期)
- [Dependency Injection](#dependency-injection)
- [Properties 與測試過濾](#properties-與測試過濾)
- [怎麼執行本篇範例](#怎麼執行本篇範例)
- [本篇驗證結果](#本篇驗證結果)
- [重點整理](#重點整理)
- [明日預告](#明日預告)
- [參考資源](#參考資源)

<!-- /toc -->

## 前言

Day27 用 `[Test]`、`[Arguments]` 與 TUnit Assertions 建立了第一批測試。常數資料足以處理簡單案例，但真實專案很快會遇到三個問題：測試資料不是 attribute 能接受的常數、fixture 建立成本不低，以及測試需要由 DI container 組裝服務。

今天把這三件事拆開處理。除了 attribute 語法，更要釐清資料與物件由誰建立、是否共享，以及預設並行執行時是否安全。

## 本篇內容

- 使用 `MethodDataSource` 提供動態與複雜測試資料
- 使用 `ClassDataSource<T>` 注入 fixture，控制共享範圍
- 使用 Matrix Tests 產生參數組合
- 使用 class／test hooks 管理生命週期
- 透過 Data Source Generator 接上 Microsoft DI
- 用 `[Property]` 與 tree node filter 選擇測試

## 範例專案

```text
samples/day28/
├── Day28.TUnitAdvanced.sln
├── Directory.Packages.props
├── global.json
├── src/TUnit.Advanced.Core/
└── tests/
    ├── TUnit.Advanced.DataDriven.Tests/
    └── TUnit.Advanced.Lifecycle.Tests/
```

本篇使用 .NET 10、TUnit 1.65.0、AutoFixture 4.18.1 與 Microsoft.Extensions.DependencyInjection 10.0.10。套件版本由 `samples/day28/Directory.Packages.props` 管理。

## 先選對資料來源

| 需求 | 建議選擇 |
| --- | --- |
| 少量常數 | `[Arguments]` |
| 動態計算、tuple、複雜物件、外部檔案 | `[MethodDataSource]` |
| 注入具有行為或生命週期的 fixture | `[ClassDataSource<T>]` |
| 驗證多個維度的完整排列組合 | `[MatrixDataSource]` |

這些工具沒有誰比較進階就一定比較好。選擇的標準是資料來源與測試意圖，不是 attribute 數量。

## MethodDataSource

`[Arguments]` 的參數必須是編譯期常數。遇到 tuple、record、動態產生的資料或檔案內容，就改用 static method。

```csharp
[Test]
[MethodDataSource(nameof(GetDiscountTestData))]
public async Task CalculatePercentageDiscount_使用不同參數_應正確計算折扣金額(
    decimal orderAmount,
    decimal discountPercent,
    decimal expected)
{
    // Arrange
    var calculator = new DiscountCalculator(new MockDiscountRepository(), new MockLogger<DiscountCalculator>());
    var order = new Order
    {
        OrderId = "TEST001",
        Items = [new OrderItem { UnitPrice = orderAmount, Quantity = 1 }]
    };
    var discountCode = $"PERCENT{discountPercent:F0}";

    // Act
    var actual = await calculator.CalculateDiscountAsync(order, discountCode);

    // Assert
    await Assert.That(actual).IsEqualTo(expected);
}

/// <summary>
/// 提供折扣計算的測試資料
/// </summary>
public static IEnumerable<(decimal orderAmount, decimal discountPercent, decimal expected)> GetDiscountTestData()
{
    yield return (1000m, 10m, 100m);
    yield return (2000m, 15m, 300m);
    yield return (500m, 20m, 100m);
    yield return (0m, 10m, 0m);
    yield return (1000m, 0m, 0m);
}
```

這個範例回傳 value tuple，每筆資料都會產生獨立測試案例。若回傳的是可變 reference type，官方建議回傳 `Func<T>` 或 `IEnumerable<Func<T>>`，讓每個案例取得新的 instance，避免平行測試共用同一個可變物件。

### 從檔案讀資料時要留意發現階段

測試資料來源會參與 test discovery。從 JSON 或其他檔案載入時：

- 把檔案設定為複製到 output directory。
- 使用可預測的相對路徑。
- discovery 階段不要依賴尚未啟動的 container 或遠端服務。
- 大量資料不要全部塞進 discovery；必要時改成少量識別碼，測試執行時再載入內容。

資料來源失敗時，問題可能發生在測試開始之前。診斷時先區分「沒有產生 test case」與「test case 執行失敗」。

## ClassDataSource

`ClassDataSource<T>` 不是另一種 row data。它會建立物件並注入測試方法或測試類別，適合包裝 fixture、client、factory 或昂貴資源。

```csharp
public sealed class ShippingFixture
{
    public ShippingCalculator Calculator { get; } = new();
}

public class InjectableClassDataSourceTests
{
    [Test]
    [ClassDataSource<ShippingFixture>]
    public async Task CalculateShipping_注入新的Fixture_應正確計算運費(ShippingFixture fixture)
    {
        var order = new Order
        {
            CustomerLevel = CustomerLevel.VIP會員,
            Items = [new OrderItem { UnitPrice = 500m, Quantity = 1 }]
        };

        var shippingFee = fixture.Calculator.CalculateShippingFee(order);

        await Assert.That(shippingFee).IsEqualTo(40m);
    }
}
```

未指定 `Shared` 時，每次都建立新 instance。需要共享時可以選擇：

- `SharedType.PerClass`
- `SharedType.PerAssembly`
- `SharedType.PerTestSession`
- `SharedType.Keyed`

共享範圍越大，啟動成本可能越低，但並行干擾風險也越高。共享 fixture 最好維持唯讀；若測試會改狀態，就要提供隔離策略或同步機制。

fixture 若需要非同步初始化與清理，可以實作 `IAsyncInitializer`、`IAsyncDisposable`。這比把昂貴資源偷偷放進 static field 更容易看出生命週期。

## Matrix Tests

Matrix Tests 會取每個參數提供的值，建立所有可能組合。以下範例由 4 種會員等級乘上 4 個訂單金額，產生 16 個案例：

```csharp
[Test]
[MatrixDataSource]
public async Task CalculateShipping_客戶等級與金額組合_應遵循運費規則(
    [Matrix(0, 1, 2, 3)] CustomerLevel customerLevel, // 0=一般會員, 1=VIP會員, 2=白金會員, 3=鑽石會員
    [Matrix(100, 500, 1000, 2000)] decimal orderAmount)
{
    // Arrange
    var calculator = new ShippingCalculator();
    var order = new Order
    {
        CustomerLevel = customerLevel,
        Items = [new OrderItem { UnitPrice = orderAmount, Quantity = 1 }]
    };

    // Act
    var shippingFee = calculator.CalculateShippingFee(order);
    var isFreeShipping = calculator.IsEligibleForFreeShipping(order);

    // Assert - 驗證運費邏輯的一致性
    if (isFreeShipping)
    {
        await Assert.That(shippingFee).IsEqualTo(0m);
    }
    else
    {
        await Assert.That(shippingFee).IsGreaterThan(0m);
    }

    // 驗證特定規則
    switch (customerLevel)
    {
        case CustomerLevel.鑽石會員:
            await Assert.That(shippingFee).IsEqualTo(0m); // 鑽石會員永遠免運
            break;

        case CustomerLevel.VIP會員 or CustomerLevel.白金會員:
            if (orderAmount < 1000m)
            {
                await Assert.That(shippingFee).IsEqualTo(40m); // VIP+ 運費半價
            }

            break;

        case CustomerLevel.一般會員:
            if (orderAmount < 1000m)
            {
                await Assert.That(shippingFee).IsEqualTo(80m); // 一般會員標準運費
            }

            break;
    }
}
```

組合數的公式很直接：

```text
總案例數 = 維度一數量 × 維度二數量 × ... × 維度 N 數量
```

三個參數各 10 個值會展開成 1,000 個案例。Matrix 適合驗證維度之間的互動；如果只需要幾個代表案例，`MethodDataSource` 通常更清楚。

使用 Matrix 時先做三件事：

1. 在 code review 直接寫出預期案例數。
2. 用 `MatrixExclusion` 排除業務上無效的組合。
3. 避免把網路、資料庫與大型 fixture 直接乘進高維度 Matrix。

## 測試生命週期

TUnit 的 hooks 分成 Test、Class、Assembly 與 TestSession 等範圍。這個範例展示建構式、`[Before(Test)]`、`[After(Test)]`、`[Before(Class)]` 與 `[After(Class)]`：

```csharp
public class LifecycleTests
{
    private readonly StringBuilder _logBuilder;
    private static readonly List<string> ClassLog = [];

    public LifecycleTests()
    {
        Console.WriteLine("2. 建構式執行 - 測試實例建立");
        _logBuilder = new StringBuilder();
        _logBuilder.AppendLine("建構式執行");
    }

    [Before(Class)]
    public static async Task BeforeClass()
    {
        Console.WriteLine("1. BeforeClass 執行 - 類別層級初始化");
        ClassLog.Add("BeforeClass 執行");
        await Task.Delay(10); // 模擬非同步初始化
    }

    [Before(Test)]
    public async Task BeforeTest()
    {
        Console.WriteLine("3. BeforeTest 執行 - 測試前置設定");
        _logBuilder.AppendLine("BeforeTest 執行");
        await Task.Delay(5); // 模擬非同步設定
    }

    [Test]
    public async Task FirstTest_應按正確順序執行生命週期方法()
    {
        Console.WriteLine($"4. FirstTest 執行 - 驗證生命週期順序 [{DateTime.Now:HH:mm:ss.fff}]");
        _logBuilder.AppendLine("FirstTest 執行");

        var log = _logBuilder.ToString();
        await Assert.That(log).Contains("建構式執行");
        await Assert.That(log).Contains("BeforeTest 執行");
        await Assert.That(ClassLog).Contains("BeforeClass 執行");
    }

    [Test]
    public async Task SecondTest_應有獨立的實例()
    {
        Console.WriteLine($"4. SecondTest 執行 - 驗證實例獨立性 [{DateTime.Now:HH:mm:ss.fff}]");
        _logBuilder.AppendLine("SecondTest 執行");

        // 每個測試都有新的實例，所以建構式會重新執行
        var log = _logBuilder.ToString();
        await Assert.That(log).Contains("建構式執行");
        await Assert.That(log).Contains("BeforeTest 執行");
    }

    [After(Test)]
    public async Task AfterTest()
    {
        Console.WriteLine("5. AfterTest 執行 - 測試後清理");
        _logBuilder.AppendLine("AfterTest 執行");
        await Task.Delay(5); // 模擬非同步清理
    }

    [After(Class)]
    public static async Task AfterClass()
    {
        Console.WriteLine("6. AfterClass 執行 - 類別層級清理");
        ClassLog.Add("AfterClass 執行");
        await Task.Delay(10); // 模擬非同步清理
    }
}
```

幾個容易踩到的點：

- Class、Assembly 與 TestSession hooks 必須是 static。
- Test hooks 可以是 instance method。
- 每個測試預設有自己的測試類別 instance。
- `[Before(Test)]` 適合每個案例都需要的設定，不要拿來藏整個 DI container。
- `[After(Test)]` 即使是清理流程，也應保留例外資訊，不要默默吞掉失敗。

不要只靠 `Console.WriteLine` 猜執行順序。真正有順序相依的資源，應以 fixture、hook scope 與測試驗證表達；一般測試仍要能獨立執行。

## Dependency Injection

TUnit 不綁定特定 DI container。它提供 `DependencyInjectionDataSourceAttribute<TScope>`，由專案決定如何建立 scope，以及如何依型別解析物件。

```csharp
public class MicrosoftDependencyInjectionDataSourceAttribute : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider ServiceProvider = CreateSharedServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata dataGeneratorMetadata)
    {
        return ServiceProvider.CreateScope();
    }

    public override object? Create(IServiceScope scope, Type type)
    {
        return scope.ServiceProvider.GetService(type);
    }

    private static IServiceProvider CreateSharedServiceProvider()
    {
        return new ServiceCollection()
               .AddSingleton<IOrderRepository, MockOrderRepository>()
               .AddSingleton<IDiscountCalculator, MockDiscountCalculator>()
               .AddSingleton<IShippingCalculator, MockShippingCalculator>()
               .AddSingleton<ILogger<OrderService>, MockLogger<OrderService>>()
               .AddTransient<OrderService>()
               .BuildServiceProvider();
    }
}

/// <summary>
/// 展示 TUnit 真正的相依性注入功能
/// </summary>
[MicrosoftDependencyInjectionDataSource]
public class DependencyInjectionTests(OrderService orderService)
{
    [Test]
    public async Task CreateOrder_使用TUnit相依性注入_應正確運作()
    {
        // Arrange - 依賴已經透過 TUnit DI 自動注入
        var items = new List<OrderItem>
        {
            new() { ProductId = "PROD001", ProductName = "測試商品", UnitPrice = 100m, Quantity = 2 }
        };

        // Act
        var order = await orderService.CreateOrderAsync("CUST001", CustomerLevel.VIP會員, items);

        // Assert
        await Assert.That(order).IsNotNull();
        await Assert.That(order.CustomerId).IsEqualTo("CUST001");
        await Assert.That(order.CustomerLevel).IsEqualTo(CustomerLevel.VIP會員);
        await Assert.That(order.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TUnitDependencyInjection_驗證自動注入_服務應為正確類型()
    {
        // Assert - 驗證 TUnit 已正確注入 OrderService 實例
        await Assert.That(orderService).IsNotNull();
        await Assert.That(orderService.GetType().Name).IsEqualTo("OrderService");
    }
}
```

這種做法適合下列情境：

- 測試建構方式應接近 production DI。
- 多個測試類別需要同一套註冊規則。
- scoped service 必須在每個測試案例結束後釋放。

但 DI 不會自動讓測試變好。純函式或只有一兩個 dependency 的 unit test，手動建立物件往往更直接。只有當「組裝物件」本身已成為重複且容易出錯的工作，才值得引入測試用 container。

### ServiceProvider 的生命週期

範例以 static root provider 建立 scope。正式專案還要處理：

- root provider 在 test session 結束時如何 dispose。
- singleton 是否含有可變狀態。
- scoped service 是否真的每個 test case 一份。
- 測試替身的 call history 是否跨案例殘留。

預設並行執行下，共享 singleton 尤其需要小心。

## Properties 與測試過濾

`[Property]` 可以為測試加上 Category、Priority、Feature 等 metadata：

```csharp
[Test]
[Property("Category", "Database")]
[Property("Priority", "High")]
public async Task DatabaseTest_高優先級_應能透過屬性過濾()
{
    // 這個測試可以透過 Category=Database 或 Priority=High 來過濾執行
    var connectionName = "test_database";
    await Assert.That(connectionName).StartsWith("test_");
}

[Test]
[Property("Category", "Unit")]
[Property("Priority", "Medium")]
public async Task UnitTest_中等優先級_基本驗證()
{
    var values = new[] { 1, 1 };
    var sum = values.Sum();

    await Assert.That(sum).IsEqualTo(2);
}

[Test]
[Property("Category", "Integration")]
[Property("Priority", "Low")]
[Property("Environment", "Development")]
public async Task IntegrationTest_低優先級_僅開發環境執行()
{
    // 可以透過多個屬性組合來精確過濾測試
    var message = string.Join(' ', "Hello", "World");

    await Assert.That(message).Contains("World");
}
```

TUnit 使用 MTP tree node filter，不使用 VSTest 的 `--filter` 語法。在 .NET 10 SDK 可執行：

```powershell
dotnet test --project tests/TUnit.Advanced.Lifecycle.Tests `
  --treenode-filter "/*/*/*/*[Category=Integration]"
```

篩選只是選擇執行範圍，不應改變測試結果。單獨執行任一測試，仍應得到與完整 suite 相同的判斷。

## 怎麼執行本篇範例

```powershell
cd samples/day28
dotnet test --solution Day28.TUnitAdvanced.sln
```

也可以分開驗證兩個測試專案：

```powershell
dotnet test --project tests/TUnit.Advanced.DataDriven.Tests
dotnet test --project tests/TUnit.Advanced.Lifecycle.Tests
```

## 本篇驗證結果

加入真正的 `ClassDataSource<T>` 範例後，兩個測試專案合計：

```text
總計：149
成功：149
失敗：0
```

測試數較多的主因是 Matrix Tests 展開組合，不代表測試品質自然提高。判讀報告時要看每個案例是否代表有意義的業務組合。

## 重點整理

- `MethodDataSource` 適合動態 row data；reference type 要避免共用 instance。
- `ClassDataSource<T>` 適合注入具有行為或生命週期的 fixture。
- Matrix Tests 會乘法展開，必須控制組合數。
- hook scope 要配合資源 scope，不能依賴測試順序。
- TUnit DI 是 Data Source Generator 的延伸，不是內建 container。
- 共享物件遇上預設並行執行時，要先處理隔離與 disposal。

## 明日預告

Day29 會把 TUnit 放進更接近 production 的環境：Retry、Timeout、測試過濾、`TUnit.AspNetCore`、`TestWebApplicationFactory<T>`，以及 PostgreSQL、Redis、Kafka 的 Testcontainers 組合。

## 參考資源

- [Method Data Sources](https://tunit.dev/docs/test-authoring/method-data-source/)
- [Injectable Class Data Source](https://tunit.dev/docs/test-authoring/class-data-source/)
- [Matrix Tests](https://tunit.dev/docs/test-authoring/matrix-tests/)
- [Dependency Injection](https://tunit.dev/docs/test-lifecycle/dependency-injection/)
- [Test Lifecycle](https://tunit.dev/docs/writing-tests/lifecycle/)
- [Properties 與 TestContext](https://tunit.dev/docs/writing-tests/test-context/#custom-properties)
- [Test Filters](https://tunit.dev/docs/execution/test-filters/)

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十八天。**
