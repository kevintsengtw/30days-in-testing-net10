namespace Day16.TimeTesting.Core;

/// <summary>
/// 全球時間服務，提供不同時區的時間轉換功能
/// </summary>
public class GlobalTimeService
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 建立全球時間服務實例
    /// </summary>
    /// <param name="timeProvider">時間提供者</param>
    public GlobalTimeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 取得指定時區的時間
    /// </summary>
    /// <param name="timeZoneId">時區 ID</param>
    /// <returns>指定時區的時間</returns>
    public DateTimeOffset GetTimeInTimeZone(string timeZoneId)
    {
        var utcNow = _timeProvider.GetUtcNow();
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTime(utcNow, targetTimeZone);
    }

    /// <summary>
    /// 將本地時間轉換為 UTC 時間
    /// </summary>
    /// <param name="localTime">本地時間</param>
    /// <returns>UTC 時間</returns>
    public DateTimeOffset ConvertLocalToUtc(DateTime localTime)
    {
        var localTimeZone = _timeProvider.LocalTimeZone;
        return TimeZoneInfo.ConvertTimeToUtc(localTime, localTimeZone);
    }

    /// <summary>
    /// 將 UTC 時間轉換為指定時區的時間
    /// </summary>
    /// <param name="utcTime">UTC 時間</param>
    /// <param name="targetTimeZoneId">目標時區 ID</param>
    /// <returns>轉換後的時間</returns>
    public DateTimeOffset ConvertUtcToTimeZone(DateTimeOffset utcTime, string targetTimeZoneId)
    {
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
        return TimeZoneInfo.ConvertTime(utcTime, targetTimeZone);
    }
}