using AutoFixture.Core.Enums;

namespace AutoFixture.Tests.Comparison;

/// <summary>
/// 比較兩種方法的效能測試
/// </summary>
public class PerformanceComparisonTests : AutoFixtureTestBase
{
    [Fact]
    public void CompareDataPreparationTime_效能比較()
    {
        const int iterationCount = 1000;

        // Day 03 方式效能測試
        var day03Stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterationCount; i++)
        {
            // 手動建立資料
            var customer = new Customer
            {
                Id = i,
                Name = $"Customer-{i}",
                Email = $"customer{i}@example.com",
                Age = 25 + (i % 50),
                Type = (CustomerType)(i % 3)
            };

            var order = new Order
            {
                Id = i,
                Customer = customer,
                Status = OrderStatus.Completed
            };
        }
        day03Stopwatch.Stop();

        // Day 10 方式效能測試
        var fixture = this.CreateFixture();
        var day10Stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterationCount; i++)
        {
            // AutoFixture 自動產生
            var order = fixture.Build<Order>()
                               .With(x => x.Status, OrderStatus.Completed)
                               .Create();
        }
        day10Stopwatch.Stop();

        // 結果比較 (AutoFixture 可能較慢，但程式碼更簡潔)
        Console.WriteLine($"Day 03 方式: {day03Stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Day 10 方式: {day10Stopwatch.ElapsedMilliseconds}ms");

        // 兩種方式都應該在合理時間內完成
        day03Stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10 秒應該足夠
        day10Stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // AutoFixture 可能較慢，給予更多時間
    }
}