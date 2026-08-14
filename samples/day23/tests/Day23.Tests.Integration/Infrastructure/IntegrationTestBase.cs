namespace Day23.Tests.Integration.Infrastructure;

/// <summary>
/// 整合測試基底類別 - 使用 Collection Fixture 共享容器
/// </summary>
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient HttpClient;
    protected readonly DatabaseManager DatabaseManager;
    protected readonly IFlurlClient FlurlClient;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        DatabaseManager = new DatabaseManager(factory.PostgresContainer.GetConnectionString());

        // 設定 Flurl 用戶端
        FlurlClient = new FlurlClient(HttpClient);
    }

    public virtual async ValueTask InitializeAsync()
    {
        // 初始化資料庫結構
        await DatabaseManager.InitializeDatabaseAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        // 清理資料庫資料
        await DatabaseManager.CleanDatabaseAsync();

        FlurlClient.Dispose();
    }
}