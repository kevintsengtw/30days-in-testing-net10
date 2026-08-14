using System.Net;
using Day23.Application.Abstractions;
using Day23.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Day23.Tests.Integration.Controllers;

/// <summary>
/// IExceptionHandler 整合測試。
///
/// 這裡的每個案例都刻意讓例外「離開 controller / service」，真正穿越
/// <c>app.UseExceptionHandler()</c> 的 middleware，進入註冊的 IExceptionHandler：
///   - ValidationException  → FluentValidationExceptionHandler
///   - KeyNotFound / Argument / 其他未預期例外 → GlobalExceptionHandler
///
/// 反向驗證（<see cref="移除ExceptionHandler註冊後_KeyNotFound不再對應為404"/>）證明：
/// 一旦破壞 handler 註冊，這些行為就不再成立，代表測試確實測到 handler 而非 controller 直接回應。
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ExceptionHandlerTests : IntegrationTestBase
{
    public ExceptionHandlerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    // ---------- GlobalExceptionHandler：KeyNotFoundException → 404 ----------

    [Fact]
    public async Task GetById_當產品不存在_由GlobalExceptionHandler對應為404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act：service 找不到產品會擲出 KeyNotFoundException，穿越 controller 進入 handler
        var response = await HttpClient.GetAsync($"/Products/{nonExistentId}", TestContext.Current.CancellationToken);

        // Assert：Title「資源不存在」為 GlobalExceptionHandler 專屬，controller 不會產生此回應
        response.Should().Be404NotFound()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/404");
                    problem.Title.Should().Be("資源不存在");
                    problem.Status.Should().Be(404);
                    problem.Detail.Should().Contain($"找不到 ID 為 {nonExistentId} 的產品");
                });
    }

    [Fact]
    public async Task Update_當產品不存在_由GlobalExceptionHandler對應為404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new { Name = "更新的產品名稱", Price = 150.00m };

        // Act
        var response = await HttpClient.PutAsJsonAsync($"/Products/{nonExistentId}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be404NotFound()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/404");
                    problem.Title.Should().Be("資源不存在");
                    problem.Status.Should().Be(404);
                    problem.Detail.Should().Contain($"找不到 ID 為 {nonExistentId} 的產品");
                });
    }

    [Fact]
    public async Task Delete_當產品不存在_由GlobalExceptionHandler對應為404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await HttpClient.DeleteAsync($"/Products/{nonExistentId}", TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be404NotFound()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/404");
                    problem.Title.Should().Be("資源不存在");
                    problem.Status.Should().Be(404);
                    problem.Detail.Should().Contain($"找不到 ID 為 {nonExistentId} 的產品");
                });
    }

    // ---------- GlobalExceptionHandler：ArgumentException → 400 ----------

    [Fact]
    public async Task Query_當排序欄位非法_由GlobalExceptionHandler對應為400()
    {
        // Act：service 對非法 sort 欄位擲出 ArgumentException，穿越 controller 進入 handler
        var response = await HttpClient.GetAsync("/Products?sort=not-a-column", TestContext.Current.CancellationToken);

        // Assert：Title「參數錯誤」為 GlobalExceptionHandler 對 ArgumentException 的專屬對應
        response.Should().Be400BadRequest()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/400");
                    problem.Title.Should().Be("參數錯誤");
                    problem.Status.Should().Be(400);
                    problem.Detail.Should().Contain("排序欄位必須為");
                });
    }

    // ---------- FluentValidationExceptionHandler：ValidationException → 400 ----------

    [Fact]
    public async Task Create_當請求無效_由FluentValidationExceptionHandler對應為ValidationProblemDetails()
    {
        // Arrange：名稱空白 + 價格為負，兩個欄位都會驗證失敗
        var invalidRequest = new { Name = "", Price = -1m };

        // Act：service 以 ValidateAndThrowAsync 擲出 ValidationException，進入專屬 handler
        var response = await HttpClient.PostAsJsonAsync("/Products", invalidRequest, TestContext.Current.CancellationToken);

        // Assert：Detail 與 Instance 是 FluentValidationExceptionHandler 才會填入的欄位，
        //         用來區別「真的走到 handler」與「MVC 內建自動 400」
        response.Should().Be400BadRequest()
                .And.Satisfy<ValidationProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.1");
                    problem.Title.Should().Be("One or more validation errors occurred.");
                    problem.Status.Should().Be(400);
                    problem.Detail.Should().Contain("驗證錯誤");
                    problem.Errors.Should().ContainKey("Name");
                    problem.Errors.Should().ContainKey("Price");
                    problem.Errors["Name"].Should().Contain("產品名稱不能為空");
                    problem.Errors["Price"].Should().Contain("產品價格必須大於 0");
                });
    }

    // ---------- GlobalExceptionHandler：未預期例外 → fallback 500 ----------

    [Fact]
    public async Task 未預期例外_由GlobalExceptionHandler對應為500()
    {
        // Arrange：以會擲出未預期例外的 repository 覆寫 DI，模擬基礎設施失敗。
        // 衍生 factory 與 client 都以 using 明確釋放，不依賴父 factory 的最終清理。
        await using var faultyFactory = Factory
                                       .WithWebHostBuilder(b => b.ConfigureServices(services =>
                                       {
                                           services.RemoveAll<IProductRepository>();
                                           services.AddScoped<IProductRepository, ThrowingProductRepository>();
                                       }));
        using var faultyClient = faultyFactory.CreateClient();

        // Act
        var response = await faultyClient.GetAsync($"/Products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert：落到 switch 的 fallback 分支
        response.Should().Be500InternalServerError()
                .And.Satisfy<ProblemDetails>(problem =>
                {
                    problem.Type.Should().Be("https://httpstatuses.com/500");
                    problem.Title.Should().Be("內部伺服器錯誤");
                    problem.Status.Should().Be(500);
                });
    }

    // ---------- 反向驗證：破壞 handler 註冊，行為必須改變 ----------

    [Fact]
    public async Task 移除ExceptionHandler註冊後_KeyNotFound不再對應為404()
    {
        // Arrange：移除所有 IExceptionHandler 註冊，只保留 UseExceptionHandler + ProblemDetails。
        // 衍生 factory 與 client 都以 using 明確釋放。
        await using var noHandlerFactory = Factory
                                          .WithWebHostBuilder(b => b.ConfigureServices(services =>
                                          {
                                              services.RemoveAll<IExceptionHandler>();
                                          }));
        using var noHandlerClient = noHandlerFactory.CreateClient();

        // Act：同樣的 KeyNotFoundException，這次沒有 GlobalExceptionHandler 接手
        var response = await noHandlerClient.DeleteAsync($"/Products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert：不再是 handler 的 404，而是預設中介軟體的 500
        //         —— 證明前面那些 404 確實來自 GlobalExceptionHandler，而非 controller
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ---------- 路由約束（非 handler 測試，僅作對照）----------

    [Fact]
    public async Task GetById_當ID格式錯誤_由路由約束回傳404()
    {
        // Arrange：{id:guid} 路由約束不匹配時，請求在進入 controller 前就被路由拒絕，
        //          與 IExceptionHandler 無關，這裡只是釐清邊界。
        const string invalidId = "invalid-guid-format";

        // Act
        var response = await HttpClient.GetAsync($"/Products/{invalidId}", TestContext.Current.CancellationToken);

        // Assert
        response.Should().Be404NotFound();
    }
}
