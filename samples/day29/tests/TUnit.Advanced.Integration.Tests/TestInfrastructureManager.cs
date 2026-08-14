namespace TUnit.Advanced.Integration.Tests;

/// <summary>
/// 測試基礎設施管理器
/// 提供統一的容器管理和依賴注入
/// </summary>
public class TestInfrastructureManager
{
    /// <summary>
    /// 取得完整的應用程式設定
    /// </summary>
    private Dictionary<string, string> GetTestConfiguration()
    {
        return new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = GlobalTestInfrastructureSetup.PostgreSqlContainer!.GetConnectionString(),
            ["ConnectionStrings:Redis"] = GlobalTestInfrastructureSetup.RedisContainer!.GetConnectionString(),
            ["Environment"] = "Testing"
        };
    }

    [Test]
    [Property("Category", "Infrastructure")]
    [DisplayName("基礎設施管理器：設定產生驗證")]
    public async Task InfrastructureManager_設定產生_應提供完整設定()
    {
        // Act
        var configuration = GetTestConfiguration();

        // Assert
        await Assert.That(configuration).IsNotNull();
        await Assert.That(configuration.ContainsKey("ConnectionStrings:DefaultConnection")).IsTrue();
        await Assert.That(configuration.ContainsKey("ConnectionStrings:Redis")).IsTrue();
        await Assert.That(configuration.ContainsKey("Environment")).IsTrue();

        await Assert.That(configuration["Environment"]).IsEqualTo("Testing");
        await Assert.That(configuration["ConnectionStrings:DefaultConnection"]).Contains("test_db");
        await Assert.That(configuration["ConnectionStrings:Redis"]).Contains("127.0.0.1");

        // 輸出設定資訊以供檢視
        Console.WriteLine("Generated test configuration:");
        foreach (var kvp in configuration)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }

    [Test]
    [Property("Category", "Infrastructure")]
    [DisplayName("基礎設施管理器：容器健康狀態檢查")]
    public async Task InfrastructureManager_容器健康檢查_所有服務應正常運作()
    {
        // Act & Assert
        await Assert.That(GlobalTestInfrastructureSetup.PostgreSqlContainer!.State).IsEqualTo(TestcontainersStates.Running);
        await Assert.That(GlobalTestInfrastructureSetup.RedisContainer!.State).IsEqualTo(TestcontainersStates.Running);

        // 驗證連線字串有效性
        var dbConnection = GlobalTestInfrastructureSetup.PostgreSqlContainer.GetConnectionString();
        var redisConnection = GlobalTestInfrastructureSetup.RedisContainer.GetConnectionString();

        await Assert.That(dbConnection).IsNotNull();
        await Assert.That(redisConnection).IsNotNull();

        Console.WriteLine("Infrastructure health check passed:");
        Console.WriteLine($"  Database: {GlobalTestInfrastructureSetup.PostgreSqlContainer.State} - {dbConnection}");
        Console.WriteLine($"  Redis: {GlobalTestInfrastructureSetup.RedisContainer.State} - {redisConnection}");
    }

    [Test]
    [Property("Category", "Infrastructure")]
    [Timeout(5000)]
    [DisplayName("基礎設施管理器：應產生完整測試設定")]
    public async Task InfrastructureManager_產生設定_應包含所有連線資訊(CancellationToken cancellationToken)
    {
        // Act
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = GetTestConfiguration();

        // Assert
        await Assert.That(configuration).IsNotNull();
        await Assert.That(configuration.Count).IsEqualTo(3);
        await Assert.That(configuration.Keys).Contains("ConnectionStrings:DefaultConnection");
        await Assert.That(configuration.Keys).Contains("ConnectionStrings:Redis");
        await Assert.That(configuration.Keys).Contains("Environment");
        await Assert.That(configuration["Environment"]).IsEqualTo("Testing");
    }
}
