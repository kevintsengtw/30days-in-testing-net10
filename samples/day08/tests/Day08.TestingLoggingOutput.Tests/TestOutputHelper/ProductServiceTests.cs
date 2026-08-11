using System.Diagnostics;
using System.Text.Json;
using Day08.TestingLoggingOutput.Core.Models;
using Day08.TestingLoggingOutput.Core.Services;

namespace Day08.TestingLoggingOutput.Tests.TestOutputHelper;

/// <summary>
/// class ProductServiceTests - ITestOutputHelper 基礎使用範例
/// </summary>
public class ProductServiceTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// ProductServiceTests 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
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

    [Fact]
    public void CalculateDiscount_一般客戶購買低價商品_應回傳0百分比折扣()
    {
        // Arrange
        var customer = new Customer { Type = CustomerType.Regular, PurchaseHistory = 1000 };
        var product = new Product { Price = 500, Category = "Accessories" };

        _output.WriteLine($"Testing Regular customer: {customer.Type}, History: {customer.PurchaseHistory}");
        _output.WriteLine($"Product: {product.Category}, Price: {product.Price}");

        var service = new ProductService();

        // Act
        var discount = service.CalculateDiscount(customer, product);

        // Assert
        _output.WriteLine($"Calculated discount: {discount}%");
        discount.Should().Be(0);
    }
}

/// <summary>
/// class StructuredOutputTests - 結構化輸出格式測試範例
/// </summary>
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

    /// <summary>
    /// 記錄區段標題
    /// </summary>
    /// <param name="title">區段標題</param>
    private void LogSection(string title)
    {
        _output.WriteLine(title);
    }

    /// <summary>
    /// 記錄訂單明細
    /// </summary>
    /// <param name="order">訂單</param>
    private void LogOrderDetails(Order order)
    {
        _output.WriteLine("訂單明細:");
        foreach (var item in order.Items)
        {
            _output.WriteLine($"  - {item.ProductName}: {item.Price:C} x {item.Quantity}");
        }
    }

    /// <summary>
    /// 記錄效能資訊
    /// </summary>
    /// <param name="start">開始時間</param>
    /// <param name="end">結束時間</param>
    private void LogPerformance(DateTime start, DateTime end)
    {
        var duration = end - start;
        _output.WriteLine($"執行時間: {duration.TotalMilliseconds:F2} ms");
    }
}

/// <summary>
/// class PerformanceTests - 效能測試與時間點記錄範例
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// PerformanceTests 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
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

    /// <summary>
    /// 產生大型資料集
    /// </summary>
    /// <param name="count">資料筆數</param>
    /// <returns></returns>
    private static IEnumerable<string> GenerateLargeDataSet(int count)
    {
        return Enumerable.Range(1, count).Select(i => $"Data-{i:D6}");
    }
}

/// <summary>
/// class DiagnosticTestBase - 診斷測試基底類別
/// </summary>
public class DiagnosticTestBase
{
    protected readonly ITestOutputHelper Output;

    /// <summary>
    /// DiagnosticTestBase 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
    protected DiagnosticTestBase(ITestOutputHelper testOutputHelper)
    {
        Output = testOutputHelper;
    }

    /// <summary>
    /// 記錄測試上下文
    /// </summary>
    /// <param name="testName">測試名稱</param>
    /// <param name="testData">測試資料</param>
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

    /// <summary>
    /// 記錄例外資訊
    /// </summary>
    /// <param name="ex">例外物件</param>
    /// <param name="context">上下文資訊</param>
    protected void LogException(Exception ex, string context = "")
    {
        Output.WriteLine($"=== 例外發生 {context} ===");
        Output.WriteLine($"例外類型: {ex.GetType().Name}");
        Output.WriteLine($"例外訊息: {ex.Message}");
        Output.WriteLine($"堆疊追蹤:\n{ex.StackTrace}");
        Output.WriteLine("");
    }

    /// <summary>
    /// 記錄斷言失敗
    /// </summary>
    /// <param name="expected">預期值</param>
    /// <param name="actual">實際值</param>
    /// <param name="fieldName">欄位名稱</param>
    protected void LogAssertionFailure(string expected, string actual, string fieldName)
    {
        Output.WriteLine($"=== 斷言失敗 ===");
        Output.WriteLine($"欄位: {fieldName}");
        Output.WriteLine($"預期值: {expected}");
        Output.WriteLine($"實際值: {actual}");
        Output.WriteLine("");
    }
}

/// <summary>
/// class ProductServiceDiagnosticTests - 商品服務診斷測試範例
/// </summary>
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