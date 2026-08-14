using System.Diagnostics;

namespace Day20.Core.Integration.Tests.Helpers;

/// <summary>
/// 資料庫測試輔助類別
/// </summary>
public static class DatabaseTestHelper
{
    /// <summary>
    /// 建立測試用使用者建立請求
    /// </summary>
    public static UserCreateRequest CreateTestUserRequest(string suffix = "")
    {
        return new UserCreateRequest
        {
            Username = $"testuser{suffix}",
            Email = $"test{suffix}@example.com",
            FullName = $"測試使用者{suffix}",
            Age = 25
        };
    }

    /// <summary>
    /// 建立測試用使用者建立請求 (指定年齡)
    /// </summary>
    public static UserCreateRequest CreateTestUserRequest(string suffix, int age)
    {
        return new UserCreateRequest
        {
            Username = $"testuser{suffix}",
            Email = $"test{suffix}@example.com",
            FullName = $"測試使用者{suffix}",
            Age = age
        };
    }

    /// <summary>
    /// 建立多個測試用使用者建立請求
    /// </summary>
    public static IEnumerable<UserCreateRequest> CreateMultipleTestUserRequests(int count, string prefix = "batch")
    {
        for (var i = 0; i < count; i++)
        {
            yield return new UserCreateRequest
            {
                Username = $"{prefix}_user_{i}_{Guid.NewGuid().ToString("N")[..8]}",
                Email = $"{prefix}.user.{i}@example.com",
                FullName = $"批次測試使用者 {i}",
                Age = 20 + (i % 50) // 年齡在 20-69 之間
            };
        }
    }

    /// <summary>
    /// 驗證使用者是否符合建立請求
    /// </summary>
    public static void AssertUserMatches(User user, UserCreateRequest request)
    {
        user.Should().NotBeNull();
        user.Username.Should().Be(request.Username);
        user.Email.Should().Be(request.Email);
        user.FullName.Should().Be(request.FullName);
        user.Age.Should().Be(request.Age);
        user.IsActive.Should().BeTrue(); // 預設為啟用
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// 建立測試用使用者更新請求
    /// </summary>
    public static UserUpdateRequest CreateTestUserUpdateRequest(string suffix = "_updated")
    {
        return new UserUpdateRequest
        {
            Email = $"updated{suffix}@example.com",
            FullName = $"更新的測試使用者{suffix}",
            Age = 30,
            IsActive = true
        };
    }

    /// <summary>
    /// 等待容器就緒
    /// </summary>
    public static async Task WaitForContainerReady(Func<Task<bool>> healthCheck,
                                                   TimeSpan timeout = default)
    {
        if (timeout == default)
        {
            timeout = TimeSpan.FromMinutes(2);
        }

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (await healthCheck())
                {
                    return;
                }
            }
            catch
            {
                // 忽略健康檢查失敗
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"容器在 {timeout.TotalSeconds} 秒內未就緒");
    }

    /// <summary>
    /// 產生唯一的測試 ID
    /// </summary>
    public static string GenerateTestId()
    {
        return $"test_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}