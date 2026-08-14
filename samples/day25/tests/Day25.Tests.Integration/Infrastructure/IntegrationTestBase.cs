namespace Day25.Tests.Integration.Infrastructure;

/// <summary>
/// 整合測試基底類別 - 使用 Aspire Testing 框架
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly AspireAppFixture Fixture;
    protected readonly HttpClient HttpClient;
    protected DatabaseManager DatabaseManager => Fixture.DatabaseManager;

    /// <summary>
    /// 建構式 - 初始化測試基底類別
    /// </summary>
    /// <param name="fixture"></param>
    protected IntegrationTestBase(AspireAppFixture fixture)
    {
        Fixture = fixture;
        HttpClient = fixture.HttpClient;
    }

    /// <summary>
    /// 每個測試執行前的初始化
    /// </summary>
    public ValueTask InitializeAsync()
    {
        return new ValueTask(Fixture.CleanStateAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 每個測試執行後的清理
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
