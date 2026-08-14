namespace BookStore.Tests.Infrastructure;

/// <summary>
/// 在每個測試案例開始前清除共用資料，讓 Collection Fixture 可以重用容器且不互相污染。
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly AspireAppFixture _fixture;

    protected IntegrationTestBase(AspireAppFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async ValueTask InitializeAsync()
    {
        await _fixture.CleanDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
