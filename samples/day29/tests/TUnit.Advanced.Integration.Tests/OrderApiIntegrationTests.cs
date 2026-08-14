using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TUnit.Advanced.Integration.Tests;

/// <summary>
/// 驗證 Order API 的成功、輸入錯誤與資源不存在 contract。
/// </summary>
[NotInParallel("WebApplicationFactory")]
public class OrderApiIntegrationTests : WebApplicationTest<TestingWebApplicationFactory, Program>
{
    [Test]
    [Property("Category", "E2E")]
    [DisplayName("Order API：有效訂單應回傳 201 Created")]
    public async Task CreateOrder_有效Request_應回傳Created與訂單內容()
    {
        using var client = Factory.CreateClient();
        var request = new CreateOrderRequest(
            "CUSTOMER-001",
            CustomerLevel.VIP會員,
            [new CreateOrderItemRequest("PRODUCT-001", "測試商品", 500m, 2)]);

        using var response = await client.PostAsJsonAsync("/orders", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");

        var order = await response.Content.ReadFromJsonAsync<Order>();
        await Assert.That(order).IsNotNull();
        await Assert.That(order!.CustomerId).IsEqualTo("CUSTOMER-001");
        await Assert.That(order.Items.Count).IsEqualTo(1);
        await Assert.That(response.Headers.Location?.ToString()).EndsWith(order.OrderId);
    }

    [Test]
    [Property("Category", "E2E")]
    [DisplayName("Order API：缺少必要欄位應回傳 400 ValidationProblemDetails")]
    public async Task CreateOrder_缺少必要欄位_應回傳ValidationProblemDetails()
    {
        using var client = Factory.CreateClient();
        var request = new CreateOrderRequest("", CustomerLevel.一般會員, []);

        using var response = await client.PostAsJsonAsync("/orders", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var problem = document.RootElement;
        await Assert.That(problem.GetProperty("status").GetInt32()).IsEqualTo(400);
        await Assert.That(problem.GetProperty("errors").TryGetProperty("CustomerId", out _)).IsTrue();
        await Assert.That(problem.GetProperty("errors").TryGetProperty("Items", out _)).IsTrue();
    }

    [Test]
    [Property("Category", "E2E")]
    [DisplayName("Order API：不存在的訂單應回傳 404 ProblemDetails")]
    public async Task GetOrder_不存在的OrderId_應回傳ProblemDetails()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync("/orders/ORDER-NOT-FOUND");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var problem = document.RootElement;
        await Assert.That(problem.GetProperty("status").GetInt32()).IsEqualTo(404);
        await Assert.That(problem.GetProperty("title").GetString()).IsEqualTo("Order not found");
    }
}
