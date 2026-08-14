namespace TUnit.Demo.Tests;

/// <summary>
/// 展示 TUnit 進階功能的測試類別
/// </summary>
public class TUnitAdvancedTests
{
    #region 並行控制測試

    [Test]
    [NotInParallel("DatabaseTests")]
    public async Task 資料庫測試1_不並行執行()
    {
        // 模擬資料庫操作
        await Task.Delay(100);
        var result = 1 + 1;
        await Assert.That(result).IsEqualTo(2);
    }

    [Test]
    [NotInParallel("DatabaseTests")]
    public async Task 資料庫測試2_不並行執行()
    {
        // 模擬資料庫操作
        await Task.Delay(100);
        var result = 2 + 2;
        await Assert.That(result).IsEqualTo(4);
    }

    [Test]
    public async Task 一般測試_可以並行執行()
    {
        await Task.Delay(50);
        var result = 1 + 1;
        await Assert.That(result).IsEqualTo(2);
    }

    #endregion
}
