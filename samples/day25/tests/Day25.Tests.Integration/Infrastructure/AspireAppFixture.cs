using Projects;
using StackExchange.Redis;

namespace Day25.Tests.Integration.Infrastructure;

/// <summary>
/// 在整個測試 collection 期間共用同一個 Aspire AppHost。
/// </summary>
public sealed class AspireAppFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;
    private ConnectionMultiplexer? _redisConnection;
    private DatabaseManager? _databaseManager;
    private string? _postgresConnectionString;
    private string? _redisConnectionString;

    public DistributedApplication App =>
        _app ?? throw new InvalidOperationException("Aspire 應用程式尚未初始化");

    public HttpClient HttpClient =>
        _httpClient ?? throw new InvalidOperationException("HTTP client 尚未初始化");

    public DatabaseManager DatabaseManager =>
        _databaseManager ?? throw new InvalidOperationException("資料庫管理員尚未初始化");

    public IDatabase RedisDatabase =>
        (_redisConnection ?? throw new InvalidOperationException("Redis 尚未初始化")).GetDatabase();

    public string PostgresConnectionString =>
        _postgresConnectionString ?? throw new InvalidOperationException("PostgreSQL 連線字串尚未初始化");

    public string RedisConnectionString =>
        _redisConnectionString ?? throw new InvalidOperationException("Redis 連線字串尚未初始化");

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellationToken = timeout.Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Day25_AppHost>(cancellationToken);

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("postgres", cancellationToken);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("redis", cancellationToken);

        _postgresConnectionString = await _app.GetConnectionStringAsync("productdb", cancellationToken)
            ?? throw new InvalidOperationException("無法取得 productdb 連線字串");
        _redisConnectionString = await _app.GetConnectionStringAsync("redis", cancellationToken)
            ?? throw new InvalidOperationException("無法取得 Redis 連線字串");

        _databaseManager = new DatabaseManager(_postgresConnectionString);
        await _databaseManager.InitializeAsync(cancellationToken);

        var redisOptions = ConfigurationOptions.Parse(_redisConnectionString);
        redisOptions.AllowAdmin = true;
        redisOptions.ConnectTimeout = 10_000;
        redisOptions.AsyncTimeout = 10_000;
        _redisConnection = await ConnectionMultiplexer
            .ConnectAsync(redisOptions)
            .WaitAsync(cancellationToken);
        await RedisDatabase.PingAsync().WaitAsync(cancellationToken);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("day25-api", cancellationToken);
        _httpClient = _app.CreateHttpClient("day25-api", "http");

        using var response = await _httpClient.GetAsync("/health", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 每個測試前同時重設 PostgreSQL 與 Redis，避免跨案例污染。
    /// </summary>
    public async Task CleanStateAsync(CancellationToken cancellationToken)
    {
        await DatabaseManager.CleanDatabaseAsync(cancellationToken);

        var redisConnection = _redisConnection
            ?? throw new InvalidOperationException("Redis 尚未初始化");

        foreach (var endpoint in redisConnection.GetEndPoints())
        {
            await redisConnection
                .GetServer(endpoint)
                .FlushDatabaseAsync()
                .WaitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_redisConnection is not null)
        {
            await _redisConnection.DisposeAsync();
        }

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
