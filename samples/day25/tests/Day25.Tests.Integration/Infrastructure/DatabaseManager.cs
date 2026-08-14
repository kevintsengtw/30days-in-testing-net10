namespace Day25.Tests.Integration.Infrastructure;

/// <summary>
/// 管理 Aspire 提供的 PostgreSQL 測試資料庫。
/// </summary>
public sealed class DatabaseManager
{
    private readonly string _connectionString;
    private Respawner? _respawner;

    public DatabaseManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    /// <summary>
    /// 每個 AppHost session 只建立一次資料庫、資料表與 Respawner。
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTablesExistAsync(connection, cancellationToken);

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = new Table[] { "__EFMigrationsHistory" },
            SchemasToInclude = new[] { "public" },
            DbAdapter = DbAdapter.Postgres
        });
    }

    public async Task CleanDatabaseAsync(CancellationToken cancellationToken)
    {
        var respawner = _respawner
            ?? throw new InvalidOperationException("Respawner 尚未初始化");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await respawner.ResetAsync(connection);
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.Database
            ?? throw new InvalidOperationException("連線字串沒有 database name");
        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string checkDatabaseSql = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
        await using var checkCommand = new NpgsqlCommand(checkDatabaseSql, connection);
        checkCommand.Parameters.AddWithValue("databaseName", databaseName);

        if (await checkCommand.ExecuteScalarAsync(cancellationToken) is not null)
        {
            return;
        }

        var quotedDatabaseName = $"\"{databaseName.Replace("\"", "\"\"")}\"";
        await using var createCommand = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName}", connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureTablesExistAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string createProductTableSql = """
            CREATE TABLE IF NOT EXISTS products (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name VARCHAR(100) NOT NULL,
                price DECIMAL(10,2) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var command = new NpgsqlCommand(createProductTableSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
