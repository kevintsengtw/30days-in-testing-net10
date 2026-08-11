using Day08.TestingLoggingOutput.Core.Interface;
using Day08.TestingLoggingOutput.Core.Models;
using Day08.TestingLoggingOutput.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Day08.TestingLoggingOutput.Tests.Integration;

/// <summary>
/// xUnit Logger 提供者，用於整合測試
/// </summary>
public class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    /// <summary>
    /// XUnitLoggerProvider 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
    public XUnitLoggerProvider(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLoggerGeneric(_testOutputHelper, categoryName, _scopeProvider);
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// 泛型 xUnit Logger 實作
/// </summary>
public class XUnitLoggerGeneric : ILogger
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName;
    private readonly LoggerExternalScopeProvider _scopeProvider;

    /// <summary>
    /// XUnitLoggerGeneric 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
    /// <param name="categoryName">記錄器類別名稱</param>
    /// <param name="scopeProvider">範圍提供者</param>
    public XUnitLoggerGeneric(ITestOutputHelper testOutputHelper, string categoryName, LoggerExternalScopeProvider scopeProvider)
    {
        _testOutputHelper = testOutputHelper;
        _categoryName = categoryName;
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

    /// <summary>
    /// 記錄日誌
    /// </summary>
    /// <typeparam name="TState">日誌狀態類型</typeparam>
    /// <param name="logLevel">日誌級別</param>
    /// <param name="eventId">事件識別碼</param>
    /// <param name="state">日誌狀態</param>
    /// <param name="exception">例外資訊</param>
    /// <param name="formatter">格式化函數</param>
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

/// <summary>
/// class InMemoryOrderRepository - 記憶體中的訂單儲存庫實作
/// </summary>
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly ILogger<InMemoryOrderRepository>? _logger;
    private readonly Dictionary<string, Order> _orders = new();

    /// <summary>
    /// InMemoryOrderRepository 建構子
    /// </summary>
    /// <param name="logger">The logger.</param>
    public InMemoryOrderRepository(ILogger<InMemoryOrderRepository>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 儲存訂單
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns></returns>
    public async Task<bool> SaveOrderAsync(Order order)
    {
        _logger?.LogInformation("儲存訂單 {OrderId}", order.Id);

        await Task.Delay(50); // 模擬 I/O 操作

        _orders[order.Id] = order;

        _logger?.LogInformation("訂單 {OrderId} 儲存成功", order.Id);
        return true;
    }

    /// <summary>
    /// 根據訂單 ID 查詢訂單
    /// </summary>
    /// <param name="orderId">訂單 ID</param>
    /// <returns>訂單資訊</returns>
    public async Task<Order?> GetOrderByIdAsync(string orderId)
    {
        _logger?.LogInformation("查詢訂單 {OrderId}", orderId);

        await Task.Delay(30); // 模擬 I/O 操作

        var found = _orders.TryGetValue(orderId, out var order);

        if (found)
        {
            _logger?.LogInformation("找到訂單 {OrderId}", orderId);
        }
        else
        {
            _logger?.LogWarning("訂單 {OrderId} 不存在", orderId);
        }

        return order;
    }
}

/// <summary>
/// class MockPaymentService - Mock 付款服務實作
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService>? _logger;

    /// <summary>
    /// MockPaymentService 建構子
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MockPaymentService(ILogger<MockPaymentService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 處理付款
    /// </summary>
    /// <param name="amount">付款金額</param>
    /// <returns>付款結果</returns>
    public PaymentResult ProcessPayment(decimal amount)
    {
        _logger?.LogInformation("處理付款，金額：{Amount:C}", amount);

        // 模擬成功付款
        var result = new PaymentResult
        {
            Success = true,
            TransactionId = Guid.NewGuid().ToString("N")[..8]
        };

        _logger?.LogInformation("付款成功，交易編號：{TransactionId}", result.TransactionId);
        return result;
    }

    /// <summary>
    /// 處理付款
    /// </summary>
    /// <param name="request">付款請求</param>
    /// <returns>付款結果</returns>
    public PaymentResult ProcessPayment(PaymentRequest request)
    {
        return ProcessPayment(request.Amount);
    }
}

/// <summary>
/// 訂單處理整合測試
/// </summary>
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

    [Fact]
    public async Task ProcessOrderAsync_空訂單項目_應正確處理()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();

        var order = new Order
        {
            Id = "EMPTY-" + Guid.NewGuid().ToString("N")[..8],
            CustomerId = "CUST002",
            Items = Array.Empty<OrderItem>()
        };

        _output.WriteLine($"=== 測試空訂單處理 ===");
        _output.WriteLine($"訂單編號: {order.Id}");

        // Act
        var result = await processor.ProcessOrderAsync(order);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TotalAmount.Should().Be(0);

        _output.WriteLine($"=== 測試完成 ===");
        _output.WriteLine($"處理結果: 成功");
        _output.WriteLine($"訂單金額: {result.TotalAmount:C}");
    }
}