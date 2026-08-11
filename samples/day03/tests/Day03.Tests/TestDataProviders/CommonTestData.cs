namespace Day03.Tests.TestDataProviders;

/// <summary>
/// class CommonTestData - 提供測試所需的共用測試資料。
/// </summary>
public static class CommonTestData
{
    /// <summary>
    /// 提供有效的使用者測試資料。
    /// </summary>
    public static IEnumerable<object[]> GetValidUserData()
    {
        yield return
        [
            new User { Name = "John Doe", Email = "john@example.com", Age = 30 }
        ];
        yield return
        [
            new User { Name = "Jane Smith", Email = "jane@company.com", Age = 25 }
        ];
    }

    /// <summary>
    /// 提供無效的使用者測試資料。
    /// </summary>
    public static IEnumerable<object[]> GetInvalidUserData()
    {
        yield return
        [
            new User { Name = "", Email = "john@example.com", Age = 30 } // 空名稱
        ];
        yield return
        [
            new User { Name = "John", Email = "invalid-email", Age = 30 } // 無效 Email
        ];
        yield return
        [
            new User { Name = "John", Email = "john@example.com", Age = -1 } // 無效年齡
        ];
    }
}