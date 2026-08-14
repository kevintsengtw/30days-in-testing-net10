using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Day19.WebApplication.Controllers.Examples.Level2;

namespace Day19.WebApplication.Tests.Examples.Level2;

/// <summary>
/// Level 2 整合測試的自訂 WebApplicationFactory
/// 特色：使用 NSubstitute 替換服務依賴，專注於 Controller 和 Service 整合層的測試
/// </summary>
public class ServiceStubWebApplicationFactory : WebApplicationFactory<Program>
{
    public IOrderService OrderServiceStub { get; }
    public IInventoryService InventoryServiceStub { get; }
    public INotificationService NotificationServiceStub { get; }

    public ServiceStubWebApplicationFactory()
    {
        OrderServiceStub = Substitute.For<IOrderService>();
        InventoryServiceStub = Substitute.For<IInventoryService>();
        NotificationServiceStub = Substitute.For<INotificationService>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除原本的服務註冊
            var orderServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOrderService));
            if (orderServiceDescriptor != null)
            {
                services.Remove(orderServiceDescriptor);
            }

            var inventoryServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IInventoryService));
            if (inventoryServiceDescriptor != null)
            {
                services.Remove(inventoryServiceDescriptor);
            }

            var notificationServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(INotificationService));
            if (notificationServiceDescriptor != null)
            {
                services.Remove(notificationServiceDescriptor);
            }

            // 註冊 Stub 服務
            services.AddSingleton(OrderServiceStub);
            services.AddSingleton(InventoryServiceStub);
            services.AddSingleton(NotificationServiceStub);
        });
    }

    /// <summary>
    /// 重設所有 Stub 的狀態
    /// </summary>
    public void ResetStubs()
    {
        OrderServiceStub.ClearReceivedCalls();
        InventoryServiceStub.ClearReceivedCalls();
        NotificationServiceStub.ClearReceivedCalls();
    }
}
