using System.Collections.Concurrent;
using TUnit.Advanced.Core.Models;
using TUnit.Advanced.Core.Services;

namespace TUnit.Advanced.WebApi.Infrastructure;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Task<bool> SaveOrderAsync(Order order) =>
        Task.FromResult(_orders.TryAdd(order.OrderId, order));

    public Task<Order?> GetOrderByIdAsync(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task<bool> UpdateOrderAsync(Order order)
    {
        if (!_orders.ContainsKey(order.OrderId))
        {
            return Task.FromResult(false);
        }

        _orders[order.OrderId] = order;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteOrderAsync(string orderId) =>
        Task.FromResult(_orders.TryRemove(orderId, out _));

    public Task<List<Order>> GetOrdersByCustomerIdAsync(string customerId) =>
        Task.FromResult(_orders.Values.Where(order => order.CustomerId == customerId).ToList());
}

public sealed class EmptyDiscountRepository : IDiscountRepository
{
    public Task<DiscountRule?> GetDiscountRuleByCodeAsync(string discountCode) =>
        Task.FromResult<DiscountRule?>(null);

    public Task<List<DiscountRule>> GetActiveDiscountRulesAsync() =>
        Task.FromResult(new List<DiscountRule>());
}
