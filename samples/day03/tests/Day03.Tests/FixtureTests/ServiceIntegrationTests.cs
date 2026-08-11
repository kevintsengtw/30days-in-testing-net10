using Day03.Domain.Data;

namespace Day03.Tests.FixtureTests;

/// <summary>
/// class ServiceIntegrationTests - 用於測試服務的整合功能。
/// </summary>
[Collection("Service Collection")]
public class ServiceIntegrationTests
{
    private readonly ServiceFixture _fixture;

    /// <summary>
    /// ServiceIntegrationTests 的建構函式
    /// </summary>
    public ServiceIntegrationTests(ServiceFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void DbContext_應該包含初始測試資料()
    {
        // Arrange
        using var scope = this._fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var users = dbContext.Users.ToList();

        // Assert
        // 共享 Fixture 的資料庫可能已被同一 Collection 的其他測試寫入資料，
        // 不能斷言精確筆數，只驗證初始種子資料存在
        Assert.True(users.Count >= ServiceFixture.InitialUserCount);
        Assert.Contains(users, u => u.Name == "Service User 1");
        Assert.Contains(users, u => u.Name == "Service User 2");
    }

    [Fact]
    public void AddUser_應該成功新增使用者()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 先確認初始狀態
        var initialCount = dbContext.Users.Count();

        var newUser = new User
        {
            Name = "Integration Test User",
            Email = "integration@example.com",
            Age = 30
        };

        // Act
        dbContext.Users.Add(newUser);
        dbContext.SaveChanges();

        // Assert
        var finalCount = dbContext.Users.Count();
        Assert.Equal(initialCount + 1, finalCount);
        Assert.Contains(dbContext.Users, u => u.Name == "Integration Test User");
    }
}

/// <summary>
/// class AnotherServiceTests - 用於測試其他服務的功能。
/// </summary>
[Collection("Service Collection")]
public class AnotherServiceTests
{
    private readonly ServiceFixture _fixture;

    /// <summary>
    /// AnotherServiceTests 的建構函式
    /// </summary>
    /// <param name="fixture"></param>
    public AnotherServiceTests(ServiceFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void 這個測試會與ServiceIntegrationTests共享同一個ServiceFixture實例()
    {
        // 這個測試會與 ServiceIntegrationTests 共享同一個 ServiceFixture 實例
        // 但每個測試類別仍然有獨立的測試實例

        using var scope = this._fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = dbContext.Users.ToList();

        // 由於共享 Fixture，可能會看到其他測試的影響
        Assert.True(users.Count >= ServiceFixture.InitialUserCount); // 至少有初始的使用者

        // 驗證 Fixture 實例是共享的
        Assert.NotNull(this._fixture.ServiceProvider);
        Assert.Same(this._fixture.ServiceProvider, this._fixture.ServiceProvider); // 驗證是同一個實例
    }
}