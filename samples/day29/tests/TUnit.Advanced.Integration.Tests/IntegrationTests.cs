namespace TUnit.Advanced.Integration.Tests;

/// <summary>
/// 為整合測試設定測試環境與服務。
/// </summary>
public sealed class TestingWebApplicationFactory : TestWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            {
                services.AddLogging();
            });
        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// 使用 TUnit.AspNetCore 執行 ASP.NET Core 整合測試。
/// </summary>
[NotInParallel("WebApplicationFactory")]
public class WebApiIntegrationTests : WebApplicationTest<TestingWebApplicationFactory, Program>
{
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task WeatherForecast_Get_應回傳正確格式的資料()
    {
        // Act
        var response = await Client.GetAsync("/weatherforecast");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(0);
    }

    [Test]
    [Property("Category", "Integration")]
    public async Task WeatherForecast_ResponseHeaders_應包含ContentType標頭()
    {
        // Act
        var response = await Client.GetAsync("/weatherforecast");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        // 檢查實際會存在的 Content-Type 標頭
        var contentType = response.Content.Headers.ContentType?.MediaType;
        await Assert.That(contentType).IsEqualTo("application/json");
    }

    [Test]
    [Property("Category", "Integration")]
    [Timeout(10000)] // 10 秒超時保護
    public async Task WeatherForecast_逾時保護_應能正常回應(CancellationToken cancellationToken)
    {
        // Act
        using var response = await Client.GetAsync("/weatherforecast", cancellationToken);

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    [Property("Category", "Smoke")]
    public async Task WeatherForecast_端點可用性_應能正常回應()
    {
        // 基本的冒煙測試：確保端點可用

        // Act
        var response = await Client.GetAsync("/weatherforecast");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(10); // 確保有實際內容
    }

    [Test]
    [Property("Category", "Concurrency")]
    [Timeout(30000)]
    public async Task WeatherForecast_並行請求_應能正確處理(CancellationToken cancellationToken)
    {
        // Arrange
        const int concurrentRequests = 50;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        for (var i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Client.GetAsync("/weatherforecast", cancellationToken));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(responses.Length).IsEqualTo(concurrentRequests);
        await Assert.That(responses.All(r => r.IsSuccessStatusCode)).IsTrue();

        // 清理
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    [Property("Category", "Health")]
    public async Task HealthCheck_應回傳健康狀態()
    {
        // 測試應用程式的健康狀態
        // 由於範例 API 沒有 /health 端點，我們測試 WeatherForecast 端點來確認 API 健康

        var response = await Client.GetAsync("/weatherforecast");

        // Assert - 確保 API 可以正常回應
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(0);
    }

}

