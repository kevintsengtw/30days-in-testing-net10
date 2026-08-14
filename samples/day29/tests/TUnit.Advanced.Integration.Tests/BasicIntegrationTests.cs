namespace TUnit.Advanced.Integration.Tests;

/// <summary>
/// 基本整合測試範例（不需要外部容器）
/// 展示 TUnit 的基礎整合測試能力
/// </summary>
public class BasicIntegrationTests
{
    [Test]
    [Property("Category", "Integration")]
    [Property("Type", "Basic")]
    [DisplayName("基本整合測試：系統可用性檢查")]
    public async Task BasicIntegration_系統可用性_應正常回應()
    {
        // Arrange
        var testValue = "Integration Test";
        var expectedLength = testValue.Length;

        // Act
        var actualLength = testValue.Length;
        var isNotEmpty = !string.IsNullOrEmpty(testValue);

        // Assert
        await Assert.That(actualLength).IsEqualTo(expectedLength);
        await Assert.That(isNotEmpty).IsTrue();

        Console.WriteLine($"基本整合測試通過：{testValue}");
    }

    [Test]
    [Property("Category", "Integration")]
    [Property("Type", "Configuration")]
    [DisplayName("設定驗證：環境變數和基礎設定")]
    public async Task ConfigurationValidation_環境設定_應符合預期()
    {
        // Arrange & Act
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        var machineName = Environment.MachineName;
        var currentDirectory = Environment.CurrentDirectory;

        // Assert
        await Assert.That(environment).IsNotNull();
        await Assert.That(machineName).IsNotNull();
        await Assert.That(currentDirectory).IsNotNull();

        Console.WriteLine($"環境資訊驗證完成：");
        Console.WriteLine($"  Environment: {environment}");
        Console.WriteLine($"  Machine: {machineName}");
        Console.WriteLine($"  Directory: {currentDirectory}");
    }

    [Test]
    [Property("Category", "Integration")]
    [Property("Type", "ExecutionControl")]
    [Timeout(5000)]
    [DisplayName("逾時保護：基本操作應完成所有計算")]
    public async Task BasicOperation_逾時保護_應完成所有計算(CancellationToken cancellationToken)
    {
        // Arrange
        var iterations = 1000;

        // Act
        var results = new List<int>();
        for (var i = 0; i < iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(i * 2);
        }

        // Assert
        await Assert.That(results.Count).IsEqualTo(iterations);
        await Assert.That(results[^1]).IsEqualTo((iterations - 1) * 2);
    }
}
