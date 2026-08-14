namespace Day25.Tests.Integration;

/// <summary>
/// 驗證 Aspire 提供的容器連線與實際服務版本。
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class VerifyAspireContainers : IntegrationTestBase
{
    public VerifyAspireContainers(AspireAppFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task 驗證_Aspire_容器連線字串()
    {
        var postgresConnectionString = Fixture.PostgresConnectionString;
        var redisConnectionString = Fixture.RedisConnectionString;

        postgresConnectionString.Should().Contain("Host=localhost");
        postgresConnectionString.Should().Contain("Port=");
        postgresConnectionString.Should().NotContain("Port=5432");

        redisConnectionString.Should().Contain("localhost:");
        redisConnectionString.Should().NotContain(":6379");

        var redisLatency = await Fixture.RedisDatabase
            .PingAsync()
            .WaitAsync(TestContext.Current.CancellationToken);
        redisLatency.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task 驗證_可以實際連線到PostgreSQL()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(Fixture.PostgresConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SHOW server_version";
        var version = await command.ExecuteScalarAsync(cancellationToken);

        version.Should().NotBeNull();
        version!.ToString().Should().StartWith("18.3");
    }
}
