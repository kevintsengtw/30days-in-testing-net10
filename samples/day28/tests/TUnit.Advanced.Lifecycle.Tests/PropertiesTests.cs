namespace TUnit.Advanced.Lifecycle.Tests;

/// <summary>
/// 展示 Properties 屬性標記與測試過濾功能
/// </summary>
public class PropertiesTests
{
    [Test]
    [Property("Category", "Database")]
    [Property("Priority", "High")]
    public async Task DatabaseTest_高優先級_應能透過屬性過濾()
    {
        // 這個測試可以透過 Category=Database 或 Priority=High 來過濾執行
        var connectionName = "test_database";
        await Assert.That(connectionName).StartsWith("test_");
    }

    [Test]
    [Property("Category", "Unit")]
    [Property("Priority", "Medium")]
    public async Task UnitTest_中等優先級_基本驗證()
    {
        var values = new[] { 1, 1 };
        var sum = values.Sum();

        await Assert.That(sum).IsEqualTo(2);
    }

    [Test]
    [Property("Category", "Integration")]
    [Property("Priority", "Low")]
    [Property("Environment", "Development")]
    public async Task IntegrationTest_低優先級_僅開發環境執行()
    {
        // 可以透過多個屬性組合來精確過濾測試
        var message = string.Join(' ', "Hello", "World");

        await Assert.That(message).Contains("World");
    }
}
