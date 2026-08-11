namespace Day03.Tests.AdvancedTheoryTests;

/// <summary>
/// class CsvTestData - 用於提供 CSV 測試資料的類別。
/// </summary>
public class CsvTestData : IEnumerable<object[]>
{
    public CsvTestData()
    {
        // 需要無參數建構函式用於 ClassData
    }

    public IEnumerator<object[]> GetEnumerator()
    {
        var csvFilePath = Path.Combine("TestData", "calculations.csv");

        if (!File.Exists(csvFilePath))
        {
            // 如果檔案不存在，提供預設測試資料
            yield return [1, 2, 3];
            yield return [5, 5, 10];
            yield break;
        }

        var lines = File.ReadAllLines(csvFilePath);
        foreach (var line in lines.Skip(1)) // 跳過標題行
        {
            var values = line.Split(',');
            if (values.Length >= 3 &&
                int.TryParse(values[0], out var a) &&
                int.TryParse(values[1], out var b) &&
                int.TryParse(values[2], out var expected))
            {
                yield return [a, b, expected];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}

/// <summary>
/// class CsvDataTests - 用於測試 CSV 資料的功能。
/// </summary>
public class CsvDataTests
{
    [Theory]
    [ClassData(typeof(CsvTestData))]
    public void Add_使用CSV資料_應回傳正確結果(int a, int b, int expected)
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }
}
