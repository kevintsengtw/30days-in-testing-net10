namespace BookStore.Tests.Infrastructure;

/// <summary>
/// .NET Aspire 應用程式測試基礎設施
/// </summary>
public class AspireAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellationToken = cancellationTokenSource.Token;

        // 建立 .NET Aspire Testing 主機
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.BookStore_AppHost>(cancellationToken);

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        // Running 只代表程序已啟動；健康狀態才代表 SQL Server 可接受連線。
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("sql", cancellationToken);

        _connectionString = await _app.GetConnectionStringAsync("bookstore-db", cancellationToken)
            ?? throw new InvalidOperationException("無法取得 bookstore-db 連線字串");

        // 只在 fixture 初始化時建立一次資料庫，避免並行測試重複建立資料庫。
        await using var context = CreateDbContext(enableRetryOnFailure: true);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// 取得資料庫內容
    /// </summary>
    public ValueTask<BookStoreDbContext> GetDbContextAsync()
    {
        return ValueTask.FromResult(CreateDbContext(enableRetryOnFailure: true));
    }

    /// <summary>
    /// 取得資料庫內容（同步版本）
    /// </summary>
    public BookStoreDbContext GetDbContext()
    {
        return CreateDbContext(enableRetryOnFailure: true);
    }

    /// <summary>
    /// 取得不使用重試策略的資料庫內容（用於交易測試）
    /// </summary>
    public ValueTask<BookStoreDbContext> GetDbContextWithoutRetryAsync()
    {
        return ValueTask.FromResult(CreateDbContext(enableRetryOnFailure: false));
    }

    /// <summary>
    /// 清理資料庫內容
    /// </summary>
    public async Task CleanDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext(enableRetryOnFailure: true);

        // 清理所有書籍資料
        await context.Books.ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private BookStoreDbContext CreateDbContext(bool enableRetryOnFailure)
    {
        if (_connectionString == null)
            throw new InvalidOperationException("應用程式尚未初始化");

        var optionsBuilder = new DbContextOptionsBuilder<BookStoreDbContext>();
        optionsBuilder.UseSqlServer(_connectionString, sqlServerOptions =>
        {
            if (enableRetryOnFailure)
                sqlServerOptions.EnableRetryOnFailure();
        });

        return new BookStoreDbContext(optionsBuilder.Options);
    }
}
