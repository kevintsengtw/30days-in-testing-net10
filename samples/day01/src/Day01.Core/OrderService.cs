namespace Day01.Core;

/// <summary>
/// 訂單狀態列舉
/// </summary>
public enum OrderStatus
{
    Created,
    Processed,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// 訂單模型類別
/// </summary>
public class Order
{
    public string Prefix { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;

    /// <summary>
    /// 處理訂單，將狀態更新為已處理
    /// </summary>
    public void Process()
    {
        Status = OrderStatus.Processed;
    }
}

/// <summary>
/// 訂單服務類別
/// 用於示範 FIRST 原則中的可重複性
/// </summary>
public class OrderService
{
    /// <summary>
    /// 處理訂單並產生訂單號碼
    /// </summary>
    /// <param name="order">要處理的訂單</param>
    /// <returns>包含處理結果的訂單物件</returns>
    public Order ProcessOrder(Order order)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        // 產生訂單號碼
        var processedOrder = new Order
        {
            Prefix = order.Prefix,
            Number = order.Number,
            Amount = order.Amount,
            Status = OrderStatus.Processed
        };

        return processedOrder;
    }

    /// <summary>
    /// 取得完整的訂單號碼
    /// </summary>
    /// <param name="order">訂單物件</param>
    /// <returns>格式化的訂單號碼</returns>
    public string GetOrderNumber(Order order)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        return $"{order.Prefix}-{order.Number}";
    }
}
