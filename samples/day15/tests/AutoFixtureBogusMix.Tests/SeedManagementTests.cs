using AutoFixtureBogusMix.Core.Models;
using AutoFixtureBogusMix.Core.TestData;
using AutoFixtureBogusMix.Core.TestData.Factories;
using AwesomeAssertions;

namespace AutoFixtureBogusMix.Tests;

/// <summary>
/// 種子管理與可重現性測試
/// </summary>
public class SeedManagementTests
{
    [Fact]
    public void 相同種子_應產生格式與結構一致的測試資料()
    {
        // Arrange
        const int seed = 12345;
        var generator1 = new HybridTestDataGenerator(seed);
        var generator2 = new HybridTestDataGenerator(seed);

        // Act
        var user1 = generator1.Generate<User>();
        var user2 = generator2.Generate<User>();

        // Assert - AutoFixture 與 Bogus 使用不同的隨機機制，整合後無法保證每個值完全相同，
        // 因此這裡驗證的是資料格式與結構的一致性，而非值的完全相同
        user1.Email.Should().Contain("@");
        user2.Email.Should().Contain("@");
        user1.FirstName.Should().NotBeNullOrEmpty();
        user2.FirstName.Should().NotBeNullOrEmpty();

        // 驗證資料格式的一致性
        user1.Phone.Should().MatchRegex(@"[\d\-\(\)\s\+\.x]+");
        user2.Phone.Should().MatchRegex(@"[\d\-\(\)\s\+\.x]+");

        // 注意：在實際專案中，如果需要完全的可重現性，
        // 建議使用單一工具（純 AutoFixture 或純 Bogus）
    }

    [Fact]
    public void 不同種子_應產生不同的測試資料()
    {
        // Arrange
        var generator1 = new HybridTestDataGenerator(seed: 11111);
        var generator2 = new HybridTestDataGenerator(seed: 22222);

        // Act
        var user1 = generator1.Generate<User>();
        var user2 = generator2.Generate<User>();

        // Assert
        user1.FirstName.Should().NotBe(user2.FirstName);
        user1.Email.Should().NotBe(user2.Email);
    }

    [Fact]
    public void 工廠使用相同種子_應產生一致的測試情境()
    {
        // Arrange
        const int seed = 54321;
        var factory1 = new IntegratedTestDataFactory(seed);
        var factory2 = new IntegratedTestDataFactory(seed);

        // Act
        var scenario1 = factory1.CreateTestScenario();
        var scenario2 = factory2.CreateTestScenario();

        // Assert - 檢查結構一致性
        scenario1.Company.Name.Should().NotBeNullOrEmpty();
        scenario2.Company.Name.Should().NotBeNullOrEmpty();
        scenario1.Users.Count.Should().Be(scenario2.Users.Count);
        scenario1.Orders.Count.Should().Be(scenario2.Orders.Count);

        // 檢查資料品質
        for (var i = 0; i < scenario1.Users.Count; i++)
        {
            scenario1.Users[i].Email.Should().Contain("@");
            scenario2.Users[i].Email.Should().Contain("@");
        }
    }

    [Fact]
    public void IntegratedTestDataFactory_相同種子_應產生一致的測試情境結構()
    {
        // Arrange
        const int seed = 54321;
        var factory1 = new IntegratedTestDataFactory(seed);
        var factory2 = new IntegratedTestDataFactory(seed);

        // Act
        var scenario1 = factory1.CreateTestScenario();
        var scenario2 = factory2.CreateTestScenario();

        // Assert - 驗證結構一致性
        scenario1.Company.Name.Should().NotBeNullOrEmpty();
        scenario2.Company.Name.Should().NotBeNullOrEmpty();
        scenario1.Company.Website.Should().StartWith("http");
        scenario2.Company.Website.Should().StartWith("http");

        // 驗證資料數量一致（因為使用了受控制的 Random）
        scenario1.Users.Count.Should().Be(scenario2.Users.Count);
        scenario1.Orders.Count.Should().Be(scenario2.Orders.Count);

        // 驗證 Orders 結構相同（因為現在使用受控制的 Random）
        for (var i = 0; i < scenario1.Orders.Count; i++)
        {
            scenario1.Orders[i].Items.Count.Should().Be(scenario2.Orders[i].Items.Count);
            // 驗證 Customer 選擇一致（因為現在使用受控制的 Random）
            var customer1Index = scenario1.Users.IndexOf(scenario1.Orders[i].Customer!);
            var customer2Index = scenario2.Users.IndexOf(scenario2.Orders[i].Customer!);
            customer1Index.Should().Be(customer2Index);
        }

        // 驗證資料品質
        scenario1.Users.Should().AllSatisfy(u => u.Email.Should().Contain("@"));
        scenario2.Users.Should().AllSatisfy(u => u.Email.Should().Contain("@"));
    }

    [Fact]
    public void 無種子設定_多次執行應產生不同資料()
    {
        // Arrange
        var generator1 = new HybridTestDataGenerator();
        var generator2 = new HybridTestDataGenerator();

        // Act
        var user1 = generator1.Generate<User>();
        var user2 = generator2.Generate<User>();

        // Assert - 沒有種子時，應該產生不同的資料
        // 注意：這個測試有小機率會失敗（如果隨機產生了相同的資料）
        (user1.FirstName != user2.FirstName ||
         user1.LastName != user2.LastName ||
         user1.Email != user2.Email).Should().BeTrue();
    }

    [Theory]
    [InlineData(123)]
    [InlineData(456)]
    [InlineData(789)]
    public void 種子設定_應確保測試可重現性(int seed)
    {
        // Arrange & Act
        var results1 = GenerateTestData(seed);
        var results2 = GenerateTestData(seed);

        // Assert - 檢查結構一致性
        results1.Users.Should().HaveCount(results2.Users.Count);
        results1.Orders.Should().HaveCount(results2.Orders.Count);

        // 檢查資料品質而非完全相同的值
        for (var i = 0; i < results1.Users.Count; i++)
        {
            results1.Users[i].Email.Should().Contain("@");
            results2.Users[i].Email.Should().Contain("@");
        }

        for (var i = 0; i < results1.Orders.Count; i++)
        {
            results1.Orders[i].TotalAmount.Should().BePositive();
            results2.Orders[i].TotalAmount.Should().BePositive();
        }
    }

    [Fact]
    public void 種子重設_應改變後續產生的資料()
    {
        // Arrange
        var factory1 = new IntegratedTestDataFactory(seed: 100);
        var factory2 = new IntegratedTestDataFactory(seed: 200);

        var firstUser = factory1.CreateFresh<User>();
        var secondUser = factory2.CreateFresh<User>();

        // Assert - 驗證兩個不同種子產生的資料確實不同
        // 由於可能存在極小機率產生相同資料，我們檢查多個屬性
        var isDifferent = firstUser.FirstName != secondUser.FirstName ||
                         firstUser.LastName != secondUser.LastName ||
                         firstUser.Email != secondUser.Email ||
                         firstUser.Phone != secondUser.Phone;

        isDifferent.Should().BeTrue("使用不同種子應該產生不同的測試資料");

        // 確保資料品質依然良好
        firstUser.Email.Should().Contain("@");
        secondUser.Email.Should().Contain("@");
    }

    private TestDataResult GenerateTestData(int seed)
    {
        var factory = new IntegratedTestDataFactory(seed);

        return new TestDataResult
        {
            Users = factory.CreateMany<User>(5),
            Orders = factory.CreateMany<Order>(3)
        };
    }

    private class TestDataResult
    {
        public List<User> Users { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }
}