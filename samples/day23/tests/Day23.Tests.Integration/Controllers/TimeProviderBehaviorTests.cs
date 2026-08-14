using Day23.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Day23.Tests.Integration.Controllers;

/// <summary>
/// TimeProvider（FakeTimeProvider）行為測試。
///
/// 延續 Day16 的 TimeProvider 教學：production code（ProductService）以注入的
/// <see cref="TimeProvider"/> 取得時間，integration 測試用 FakeTimeProvider 取代它，
/// 透過 FakeTimeProvider 建構式設定初始時間，並使用 <c>Advance</c> 推進時鐘，讓時間戳記在真實 HTTP 流程中可預測、可推進。
///
/// 每個測試都建立自己的 FakeTimeProvider 與對應 client（以 <c>WithWebHostBuilder</c> 覆寫 DI），
/// 避免共享時鐘被單一測試推進後污染其他測試的絕對時間斷言。
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TimeProviderBehaviorTests : IntegrationTestBase
{
    public TimeProviderBehaviorTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    // 以指定時鐘覆寫 DI，回傳衍生的 WebApplicationFactory；由呼叫端以 await using 明確釋放，
    // 不依賴父 factory 的最終清理。
    private WebApplicationFactory<Program> CreateFactoryWithClock(FakeTimeProvider clock)
    {
        return Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
    }

    [Fact]
    public async Task 建立產品_建立時間應為FakeTimeProvider的目前時間()
    {
        // Arrange：把時鐘固定在一個明確時刻
        var fixedNow = new DateTimeOffset(2030, 6, 15, 8, 30, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(fixedNow);
        await using var factory = CreateFactoryWithClock(clock);
        using var client = factory.CreateClient();
        var request = TestHelpers.CreateProductRequest("時間測試產品", 100m);

        // Act
        var response = await client.PostAsJsonAsync("/products", request, TestContext.Current.CancellationToken);

        // Assert：CreatedAt/UpdatedAt 完全由 FakeTimeProvider 決定
        response.Should().Be201Created()
                .And.Satisfy<ProductResponse>(product =>
                {
                    product.CreatedAt.Should().Be(fixedNow);
                    product.UpdatedAt.Should().Be(fixedNow);
                });
    }

    [Fact]
    public async Task 更新產品_UpdatedAt應隨Advance前進而CreatedAt不變()
    {
        // Arrange：在基準時間建立產品
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        await using var factory = CreateFactoryWithClock(clock);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/products",
            TestHelpers.CreateProductRequest("可推進時間的產品", 200m),
            TestContext.Current.CancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestContext.Current.CancellationToken);
        created.Should().NotBeNull();

        // Act：把時鐘往前推 2 小時，再更新
        clock.Advance(TimeSpan.FromHours(2));
        var updateResponse = await client.PutAsJsonAsync(
            $"/products/{created!.Id}",
            new ProductUpdateRequest { Name = "已更新", Price = 250m },
            TestContext.Current.CancellationToken);
        updateResponse.Should().Be204NoContent();

        // Assert：UpdatedAt 應剛好是 CreatedAt + 2 小時，CreatedAt 保持不變
        var getResponse = await client.GetAsync($"/products/{created.Id}", TestContext.Current.CancellationToken);
        getResponse.Should().Be200Ok()
                   .And.Satisfy<ProductResponse>(product =>
                   {
                       product.CreatedAt.Should().Be(start);
                       product.UpdatedAt.Should().Be(start.AddHours(2));
                   });
    }
}
