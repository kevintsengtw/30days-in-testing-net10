using Day01.Core;

namespace Day01.Core.Tests;

/// <summary>
/// PriceCalculator 測試類別
/// 展示 FIRST 原則中的自我驗證和明確的錯誤訊息
/// </summary>
public class PriceCalculatorTests
{
    private readonly PriceCalculator _priceCalculator;

    public PriceCalculatorTests()
    {
        _priceCalculator = new PriceCalculator();
    }

    #region Calculate 方法測試 - 展示 Self-Validating 原則

    [Fact] // Self-Validating: 提供明確的驗證和錯誤訊息
    public void Calculate_輸入100元和10Percent折扣_應回傳90元()
    {
        // Arrange
        var basePrice = 100m;
        var discount = 0.1m;
        var expected = 90m;

        // Act
        var result = _priceCalculator.Calculate(basePrice, discount);

        // Assert - 失敗時顯示清楚的期望值和實際值
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Calculate_輸入負數價格_應拋出ArgumentException()
    {
        // Arrange
        var basePrice = -100m;
        var discount = 0.1m;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _priceCalculator.Calculate(basePrice, discount));
        Assert.Equal("基礎價格不能為負數 (Parameter 'basePrice')", exception.Message);
        Assert.Equal("basePrice", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.1)] // 負數折扣
    [InlineData(1.1)]  // 超過100%折扣
    [InlineData(2.0)]  // 200%折扣
    public void Calculate_輸入無效折扣比例_應拋出ArgumentException(decimal invalidDiscount)
    {
        // Arrange
        var basePrice = 100m;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _priceCalculator.Calculate(basePrice, invalidDiscount));
        Assert.Equal("折扣比例必須在 0 到 1 之間 (Parameter 'discount')", exception.Message);
        Assert.Equal("discount", exception.ParamName);
    }

    [Theory] // 測試各種有效的價格和折扣組合
    [InlineData(100, 0.0, 100)]   // 無折扣
    [InlineData(100, 0.1, 90)]    // 10%折扣
    [InlineData(100, 0.5, 50)]    // 50%折扣
    [InlineData(100, 1.0, 0)]     // 100%折扣（免費）
    [InlineData(250, 0.2, 200)]   // 20%折扣
    [InlineData(999.99, 0.15, 849.9915)] // 15%折扣，測試小數
    public void Calculate_輸入各種有效組合_應回傳正確結果(decimal basePrice, decimal discount, decimal expected)
    {
        // Act
        var result = _priceCalculator.Calculate(basePrice, discount);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region CalculateWithTax 方法測試

    [Fact]
    public void CalculateWithTax_輸入100元和5Percent稅率_應回傳105元()
    {
        // Arrange
        var basePrice = 100m;
        var taxRate = 0.05m;
        var expected = 105m;

        // Act
        var result = _priceCalculator.CalculateWithTax(basePrice, taxRate);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateWithTax_輸入負數價格_應拋出ArgumentException()
    {
        // Arrange
        var basePrice = -100m;
        var taxRate = 0.05m;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _priceCalculator.CalculateWithTax(basePrice, taxRate));
        Assert.Equal("基礎價格不能為負數 (Parameter 'basePrice')", exception.Message);
        Assert.Equal("basePrice", exception.ParamName);
    }

    [Fact]
    public void CalculateWithTax_輸入負數稅率_應拋出ArgumentException()
    {
        // Arrange
        var basePrice = 100m;
        var taxRate = -0.05m;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _priceCalculator.CalculateWithTax(basePrice, taxRate));
        Assert.Equal("稅率不能為負數 (Parameter 'taxRate')", exception.Message);
        Assert.Equal("taxRate", exception.ParamName);
    }

    [Theory]
    [InlineData(100, 0.0, 100)]     // 無稅率
    [InlineData(100, 0.05, 105)]    // 5%稅率
    [InlineData(100, 0.1, 110)]     // 10%稅率
    [InlineData(250, 0.08, 270)]    // 8%稅率
    [InlineData(999.99, 0.125, 1124.98875)] // 12.5%稅率 - 修正精度
    public void CalculateWithTax_輸入各種有效組合_應回傳正確結果(decimal basePrice, decimal taxRate, decimal expected)
    {
        // Act
        var result = _priceCalculator.CalculateWithTax(basePrice, taxRate);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 邊界值測試

    [Fact]
    public void Calculate_輸入0元價格_應正常處理()
    {
        // Arrange
        var basePrice = 0m;
        var discount = 0.1m;
        var expected = 0m;

        // Act
        var result = _priceCalculator.Calculate(basePrice, discount);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateWithTax_輸入0元價格_應正常處理()
    {
        // Arrange
        var basePrice = 0m;
        var taxRate = 0.05m;
        var expected = 0m;

        // Act
        var result = _priceCalculator.CalculateWithTax(basePrice, taxRate);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
