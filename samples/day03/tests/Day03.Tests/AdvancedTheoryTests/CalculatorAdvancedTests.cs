namespace Day03.Tests.AdvancedTheoryTests;

/// <summary>
/// class CalculatorAdvancedTests - 用於測試計算器的進階功能。
/// </summary>
public class CalculatorAdvancedTests
{
    private readonly Calculator _calculator;

    /// <summary>
    /// CalculatorAdvancedTests 的建構函式
    /// </summary>
    public CalculatorAdvancedTests()
    {
        this._calculator = new Calculator();
    }

    // 使用靜態屬性提供測試資料
    public static IEnumerable<object[]> AddTestData =>
        new List<object[]>
        {
            new object[] { 1, 2, 3 },
            new object[] { -1, 1, 0 },
            new object[] { 0, 0, 0 },
            new object[] { 100, 200, 300 },
            new object[] { int.MaxValue, 1, (long)int.MaxValue + 1 } // 溢位測試
        };

    [Theory]
    [MemberData(nameof(AddTestData))]
    public void Add_使用MemberData_應回傳正確結果(int a, int b, long expected)
    {
        // Act
        var result = this._calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }
}
