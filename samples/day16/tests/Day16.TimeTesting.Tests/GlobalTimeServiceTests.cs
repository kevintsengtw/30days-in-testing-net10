namespace Day16.TimeTesting.Tests;

/// <summary>
/// GlobalTimeService 的測試，展示時區處理的最佳實踐
/// </summary>
public class GlobalTimeServiceTests
{
    [Theory]
    [InlineData("UTC", "2024-03-15 10:00:00")]
    [InlineData("Tokyo Standard Time", "2024-03-15 19:00:00")]
    [InlineData("Eastern Standard Time", "2024-03-15 06:00:00")]
    public void GetTimeInTimeZone_不同時區_應回傳正確時間(string timeZoneId, string expectedTimeStr)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var baseUtcTime = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        fakeTimeProvider.SetUtcNow(baseUtcTime);

        var service = new GlobalTimeService(fakeTimeProvider);
        var expectedTime = DateTime.Parse(expectedTimeStr);

        // Act
        var result = service.GetTimeInTimeZone(timeZoneId);

        // Assert
        result.DateTime.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTimeInTimeZone_使用FakeTimeProvider_應回傳可控制的時間()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var testUtcTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc); // 夏令時間
        fakeTimeProvider.SetUtcNow(testUtcTime);

        var service = new GlobalTimeService(fakeTimeProvider);

        // Act
        var utcResult = service.GetTimeInTimeZone("UTC");
        var tokyoResult = service.GetTimeInTimeZone("Tokyo Standard Time");

        // Assert
        utcResult.DateTime.Should().Be(testUtcTime);
        tokyoResult.DateTime.Should().Be(testUtcTime.AddHours(9)); // 東京比UTC快9小時
    }

    [Fact]
    public void ConvertLocalToUtc_使用本地時間_應正確轉換為UTC()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);

        var service = new GlobalTimeService(fakeTimeProvider);
        var localTime = new DateTime(2024, 3, 15, 14, 30, 0); // 本地時間下午2:30

        // Act
        var result = service.ConvertLocalToUtc(localTime);

        // Assert
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(localTime, TimeZoneInfo.Local);
        result.Should().Be(expectedUtc);
    }

    [Fact]
    public void ConvertUtcToTimeZone_轉換UTC到特定時區_應回傳正確時間()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var service = new GlobalTimeService(fakeTimeProvider);

        var utcTime = new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero);

        // Act
        var tokyoTime = service.ConvertUtcToTimeZone(utcTime, "Tokyo Standard Time");
        var estTime = service.ConvertUtcToTimeZone(utcTime, "Eastern Standard Time");

        // Assert
        tokyoTime.DateTime.Should().Be(new DateTime(2024, 3, 15, 19, 0, 0)); // UTC+9
        estTime.DateTime.Should().Be(new DateTime(2024, 3, 15, 6, 0, 0));   // UTC-4 (夏令時間考量)
    }

    [Fact]
    public void GetTimeInTimeZone_無效時區ID_應拋出例外()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var service = new GlobalTimeService(fakeTimeProvider);

        // Act & Assert
        var action = () => service.GetTimeInTimeZone("Invalid/TimeZone");
        action.Should().Throw<TimeZoneNotFoundException>();
    }

    [Fact]
    public void GlobalTimeService_多時區同時測試_應保持時間一致性()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var baseUtcTime = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc); // UTC 聖誕節午夜
        fakeTimeProvider.SetUtcNow(baseUtcTime);

        var service = new GlobalTimeService(fakeTimeProvider);

        // Act - 取得多個時區的時間
        var utcTime = service.GetTimeInTimeZone("UTC");
        var tokyoTime = service.GetTimeInTimeZone("Tokyo Standard Time");
        var londonTime = service.GetTimeInTimeZone("GMT Standard Time");
        var nyTime = service.GetTimeInTimeZone("Eastern Standard Time");

        // Assert - 驗證時區轉換的正確性
        utcTime.DateTime.Should().Be(new DateTime(2024, 12, 25, 0, 0, 0));  // UTC 午夜
        tokyoTime.DateTime.Should().Be(new DateTime(2024, 12, 25, 9, 0, 0)); // 東京上午9點
        londonTime.DateTime.Should().Be(new DateTime(2024, 12, 25, 0, 0, 0)); // 倫敦午夜（冬令時間）
        nyTime.DateTime.Should().Be(new DateTime(2024, 12, 24, 19, 0, 0));   // 紐約前一天晚上7點
    }
}