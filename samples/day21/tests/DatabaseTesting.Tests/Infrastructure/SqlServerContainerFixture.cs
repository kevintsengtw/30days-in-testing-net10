namespace DatabaseTesting.Tests.Infrastructure;

/// <summary>
/// SQL Server 容器 Fixture，負責管理測試用的 SQL Server 容器生命週期
/// </summary>
public class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerContainerFixture()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                     .WithPassword("Test123456!")
                     .WithCleanUp(true)
                     .Build();
    }

    /// <summary>
    /// 取得連線字串
    /// </summary>
    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// 初始化容器
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // Testcontainers MsSql module 內建等待策略：StartAsync 會等到 SQL Server
        // 真正可接受連線才返回，不需要再用固定 Task.Delay 猜測就緒時間。
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        Console.WriteLine($"SQL Server 容器已啟動，連線字串：{ConnectionString}");
    }

    /// <summary>
    /// 清理容器
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        Console.WriteLine("SQL Server 容器已清理");
    }
}

/// <summary>
/// Collection Definition，用於共享 SQL Server 容器
/// </summary>
[CollectionDefinition(nameof(SqlServerCollectionFixture))]
public class SqlServerCollectionFixture : ICollectionFixture<SqlServerContainerFixture>
{
    // 此類別只是用來定義 Collection，不需要實作內容
}