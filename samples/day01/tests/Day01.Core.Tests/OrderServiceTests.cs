using Day01.Core;

namespace Day01.Core.Tests;

/// <summary>
/// OrderService 測試類別
/// 展示 FIRST 原則中的可重複性和自我驗證
/// </summary>
public class OrderServiceTests
{
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _orderService = new OrderService();
    }

    #region ProcessOrder 方法測試 - 展示 Repeatable 原則

    [Fact] // Repeatable: 使用固定的測試資料，不依賴環境
    public void ProcessOrder_輸入有效訂單_應回傳處理後訂單()
    {
        // Arrange - 使用固定的測試資料，不依賴環境
        var orderPrefix = "TEST";
        var orderNumber = "12345";
        var amount = 100m;

        var order = new Order 
        { 
            Prefix = orderPrefix, 
            Number = orderNumber,
            Amount = amount
        };

        // Act
        var result = _orderService.ProcessOrder(order);

        // Assert - 每次執行都會得到相同結果
        Assert.Equal(orderPrefix, result.Prefix);
        Assert.Equal(orderNumber, result.Number);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(OrderStatus.Processed, result.Status);
    }

    [Fact]
    public void ProcessOrder_輸入null_應拋出ArgumentNullException()
    {
        // Arrange
        Order? order = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => _orderService.ProcessOrder(order!));
        Assert.Equal("order", exception.ParamName);
    }

    #endregion

    #region GetOrderNumber 方法測試 - 展示清楚的驗證訊息

    [Fact] // Self-Validating: 提供清楚的驗證訊息
    public void GetOrderNumber_輸入有效訂單_應回傳格式化訂單號碼()
    {
        // Arrange
        var orderPrefix = "ORD";
        var orderNumber = "001";
        var expectedOrderNumber = "ORD-001";

        var order = new Order 
        { 
            Prefix = orderPrefix, 
            Number = orderNumber 
        };

        // Act
        var result = _orderService.GetOrderNumber(order);

        // Assert - 失敗時會顯示期望值和實際值，便於除錯
        Assert.Equal(expectedOrderNumber, result);
    }

    [Fact]
    public void GetOrderNumber_輸入null_應拋出ArgumentNullException()
    {
        // Arrange
        Order? order = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => _orderService.GetOrderNumber(order!));
        Assert.Equal("order", exception.ParamName);
    }

    [Theory] // 測試多種組合，確保格式化邏輯正確
    [InlineData("TEST", "123", "TEST-123")]
    [InlineData("ORD", "456", "ORD-456")]
    [InlineData("INV", "789", "INV-789")]
    [InlineData("", "123", "-123")]
    [InlineData("TEST", "", "TEST-")]
    public void GetOrderNumber_輸入各種前綴和號碼組合_應回傳正確格式(string prefix, string number, string expected)
    {
        // Arrange
        var order = new Order { Prefix = prefix, Number = number };

        // Act
        var result = _orderService.GetOrderNumber(order);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 展示 Independent 原則的測試

    [Fact] // Independent: 這個測試不會影響其他測試
    public void ProcessOrder_多次呼叫_每次都應該回傳新的物件實例()
    {
        // Arrange
        var order1 = new Order { Prefix = "A", Number = "1", Amount = 100 };
        var order2 = new Order { Prefix = "B", Number = "2", Amount = 200 };

        // Act
        var result1 = _orderService.ProcessOrder(order1);
        var result2 = _orderService.ProcessOrder(order2);

        // Assert - 確保每次都回傳新的獨立物件
        Assert.NotSame(result1, result2);
        Assert.NotSame(order1, result1);
        Assert.NotSame(order2, result2);

        // 驗證各自的內容都正確
        Assert.Equal("A", result1.Prefix);
        Assert.Equal("1", result1.Number);
        Assert.Equal(100, result1.Amount);

        Assert.Equal("B", result2.Prefix);
        Assert.Equal("2", result2.Number);
        Assert.Equal(200, result2.Amount);
    }

    #endregion
}
