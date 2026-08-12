namespace AutoData.Tests.DataSources;

/// <summary>
/// 可重用的測試資料集
/// </summary>
public class ReusableTestDataSets
{
    /// <summary>
    /// 可重用的產品分類資料
    /// </summary>
    public static class ProductCategories
    {
        public const string Electronics = "3C產品";
        public const string Fashion = "服飾配件";
        public const string Home = "居家生活";
        public const string Sports = "運動健身";
        
        public static IEnumerable<object[]> All()
        {
            yield return new object[] { Electronics, "TECH" };
            yield return new object[] { Fashion, "FASHION" };
            yield return new object[] { Home, "HOME" };
            yield return new object[] { Sports, "SPORTS" };
        }
    }
    
    /// <summary>
    /// 可重用的價格區間資料
    /// </summary>
    public static class PriceRanges
    {
        public static IEnumerable<object[]> Budget()
        {
            yield return new object[] { 100m, 500m };
            yield return new object[] { 500m, 1000m };
        }
        
        public static IEnumerable<object[]> Premium()
        {
            yield return new object[] { 5000m, 10000m };
            yield return new object[] { 10000m, 50000m };
        }
    }
}
