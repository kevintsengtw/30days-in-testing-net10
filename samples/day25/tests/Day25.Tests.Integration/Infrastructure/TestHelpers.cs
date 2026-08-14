namespace Day25.Tests.Integration.Infrastructure;

public static class TestHelpers
{
    public static ProductCreateRequest CreateProductRequest(string name, decimal price)
    {
        return new ProductCreateRequest
        {
            Name = name,
            Price = price
        };
    }

    public static async Task SeedProductsAsync(
        DatabaseManager databaseManager,
        int count,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(databaseManager.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        for (var i = 1; i <= count; i++)
        {
            const string sql = """
                INSERT INTO products (id, name, price, created_at, updated_at)
                VALUES (@id, @name, @price, @createdAt, @updatedAt)
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("name", $"測試產品 {i}");
            command.Parameters.AddWithValue("price", 100.00m + i);
            command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
            command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task<Guid> SeedSpecificProductAsync(
        DatabaseManager databaseManager,
        string name,
        decimal price,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(databaseManager.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var productId = Guid.NewGuid();
        const string sql = """
            INSERT INTO products (id, name, price, created_at, updated_at)
            VALUES (@id, @name, @price, @createdAt, @updatedAt)
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", productId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("price", price);
        command.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return productId;
    }
}
