using Day01.Core;

namespace Day01.Core.Tests;

/// <summary>
/// 計算器測試類別
/// 展示 FIRST 原則的完整應用
/// </summary>
public class CalculatorTests
{
    private readonly Calculator _calculator;

    public CalculatorTests()
    {
        _calculator = new Calculator();
    }

    #region Add 方法測試 - 展示基本的 FIRST 原則

    [Fact] // Fast: 不依賴外部資源，執行快速
    public void Add_輸入1和2_應回傳3()
    {
        // Arrange
        var a = 1;
        var b = 2;
        var expected = 3;

        // Act
        var result = _calculator.Add(a, b);

        // Assert - Self-Validating: 明確的驗證和錯誤訊息
        Assert.Equal(expected, result);
    }

    [Fact] // Independent: 不依賴其他測試的結果
    public void Add_輸入負數和正數_應回傳正確結果()
    {
        // Arrange
        var a = -5;
        var b = 3;
        var expected = -2;

        // Act
        var result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact] // Repeatable: 每次執行都會得到相同結果
    public void Add_輸入0和0_應回傳0()
    {
        // Arrange
        var a = 0;
        var b = 0;
        var expected = 0;

        // Act
        var result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Theory 測試 - 一次測試多個案例

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(100, -50, 50)]
    public void Add_輸入各種數值組合_應回傳正確結果(int a, int b, int expected)
    {
        // Act
        var result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Divide 方法測試 - 展示例外處理

    [Fact]
    public void Divide_輸入10和2_應回傳5()
    {
        // Arrange
        var dividend = 10m;
        var divisor = 2m;
        var expected = 5m;

        // Act
        var result = _calculator.Divide(dividend, divisor);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact] // 測試例外情況
    public void Divide_輸入10和0_應拋出DivideByZeroException()
    {
        // Arrange
        var dividend = 10m;
        var divisor = 0m;

        // Act & Assert
        var exception = Assert.Throws<DivideByZeroException>(() => _calculator.Divide(dividend, divisor));
        Assert.Equal("除數不能為零", exception.Message);
    }

    [Theory]
    [InlineData(10, 2, 5)]
    [InlineData(15, 3, 5)]
    [InlineData(7, 2, 3.5)]
    [InlineData(-10, 2, -5)]
    public void Divide_輸入各種有效數值_應回傳正確結果(decimal dividend, decimal divisor, decimal expected)
    {
        // Act
        var result = _calculator.Divide(dividend, divisor);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Multiply 方法測試

    [Fact]
    public void Multiply_輸入3和4_應回傳12()
    {
        // Arrange
        var a = 3;
        var b = 4;
        var expected = 12;

        // Act
        var result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(-2, 3, -6)]
    [InlineData(-2, -3, 6)]
    public void Multiply_輸入各種數值組合_應回傳正確結果(int a, int b, int expected)
    {
        // Act
        var result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
