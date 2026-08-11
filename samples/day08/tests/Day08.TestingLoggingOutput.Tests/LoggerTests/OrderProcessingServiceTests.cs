using Day08.TestingLoggingOutput.Core.Interface;
using Day08.TestingLoggingOutput.Core.Logging;
using Day08.TestingLoggingOutput.Core.Models;
using Day08.TestingLoggingOutput.Core.Services;
using Day08.TestingLoggingOutput.Tests.Logging;

namespace Day08.TestingLoggingOutput.Tests.LoggerTests;

/// <summary>
/// 使用 AbstractLogger 的訂單處理服務測試
/// </summary>
public class OrderProcessingServiceTests
{
    private readonly AbstractLogger<OrderProcessingService> _logger;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    /// <summary>
    /// 訂單處理服務測試
    /// </summary>
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

/// <summary>
/// 使用 CompositeLogger 的進階測試
/// </summary>
public class OrderProcessingAdvancedTests
{
    private readonly AbstractLogger<OrderProcessingService> _mockLogger;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<OrderProcessingService> _compositeLogger;

    /// <summary>
    /// OrderProcessingAdvancedTests 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
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

/// <summary>
/// class PaymentService - 簡單的付款服務實作（用於測試）
/// </summary>
public class PaymentService
{
    private readonly ILogger<PaymentService>? _logger;

    /// <summary>
    /// PaymentService 建構子
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PaymentService(ILogger<PaymentService>? logger = null)
    {
        _logger = logger;
    }

    public PaymentResult ProcessPayment(PaymentRequest request)
    {
        _logger?.LogInformation("Processing payment for amount {Amount} {Currency}",
                                request.Amount, request.Currency);

        // 模擬付款失敗
        _logger?.LogError("Payment processing failed for amount {Amount}", request.Amount);

        return new PaymentResult
        {
            Success = false,
            ErrorMessage = "Insufficient funds"
        };
    }
}

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