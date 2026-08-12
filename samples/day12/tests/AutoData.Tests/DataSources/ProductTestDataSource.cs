namespace AutoData.Tests.DataSources;

/// <summary>
/// 產品測試資料來源
/// </summary>
public class ProductTestDataSource : BaseTestData
{
    public static IEnumerable<object[]> BasicProducts()
    {
        yield return new object[] { "iPhone", 35900m, true };
        yield return new object[] { "MacBook", 89900m, true };
        yield return new object[] { "iPad", 18900m, false };
    }
    
    public static IEnumerable<object[]> ElectronicsFromCsv()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "products.csv");
        if (!File.Exists(testDataPath))
        {
            // 如果在輸出目錄找不到，嘗試從專案目錄找
            testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestData", "products.csv");
        }

        if (!File.Exists(testDataPath))
        {
            // 如果還是找不到，回傳預設資料
            yield return new object[] { 1, "iPhone 15", "3C產品", 35900m, true };
            yield return new object[] { 2, "MacBook Pro", "3C產品", 89900m, true };
            yield break;
        }

        var csvContent = File.ReadAllText(testDataPath);
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // 跳過標題行
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length >= 5 && values[2].Trim('"') == "3C產品")
            {
                yield return new object[] 
                { 
                    int.Parse(values[0]),
                    values[1].Trim('"'),
                    values[2].Trim('"'),
                    decimal.Parse(values[3]),
                    bool.Parse(values[4])
                };
            }
        }
    }
    
    public static IEnumerable<object[]> SportsProducts()
    {
        yield return new object[] { "Nike Air", 4200m, true };
        yield return new object[] { "Adidas Ultra", 5800m, true };
    }
}
