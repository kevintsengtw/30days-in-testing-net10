using Day08.TestingLoggingOutput.Core.Interface;
using Day08.TestingLoggingOutput.Core.Models;

namespace Day08.TestingLoggingOutput.Core.Services;

/// <summary>
/// class OrderProcessor - 訂單處理器（用於展示更複雜的情境）
/// </summary>
public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentService _paymentService;

    /// <summary>
    /// OrderProcessor 建構子
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orderRepository">The order repository.</param>
    /// <param name="paymentService">The payment service.</param>
    public OrderProcessor(
        ILogger<OrderProcessor> logger,
        IOrderRepository orderRepository,
        IPaymentService paymentService)
    {
        _logger = logger;
        _orderRepository = orderRepository;
        _paymentService = paymentService;
    }

    /// <summary>
    /// 非同步處理訂單
    /// </summary>
    /// <param name="order">訂單資訊</param>
    /// <returns>處理結果</returns>
    public async Task<OrderResult> ProcessOrderAsync(Order order)
    {
        _logger.LogInformation("開始非同步處理訂單 {OrderId}", order.Id);

        try
        {
            // 計算訂單總金額
            order.TotalAmount = order.Items.Sum(item => item.Price * item.Quantity);

            // 儲存訂單
            await _orderRepository.SaveOrderAsync(order);
            _logger.LogInformation("訂單 {OrderId} 已儲存", order.Id);

            // 處理付款
            var paymentResult = _paymentService.ProcessPayment(order.TotalAmount);
            if (!paymentResult.Success)
            {
                _logger.LogError("訂單 {OrderId} 付款失敗", order.Id);
                return new OrderResult
                {
                    Success = false,
                    ErrorMessage = "付款失敗",
                    OrderId = order.Id
                };
            }

            _logger.LogInformation("訂單 {OrderId} 處理完成", order.Id);
            return new OrderResult
            {
                Success = true,
                OrderId = order.Id,
                TotalAmount = order.TotalAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理訂單 {OrderId} 時發生錯誤", order.Id);
            return new OrderResult
            {
                Success = false,
                ErrorMessage = "系統錯誤",
                OrderId = order.Id
            };
        }
    }
}
