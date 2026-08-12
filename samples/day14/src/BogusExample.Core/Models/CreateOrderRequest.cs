namespace BogusExample.Core.Models;

/// <summary>
/// 建立訂單請求
/// </summary>
public class CreateOrderRequest
{
    /// <summary>
    /// 客戶姓名
    /// </summary>
    public string CustomerName { get; set; } = "";

    /// <summary>
    /// 客戶電子郵件
    /// </summary>
    public string CustomerEmail { get; set; } = "";

    /// <summary>
    /// 運送地址
    /// </summary>
    public string ShippingAddress { get; set; } = "";

    /// <summary>
    /// 訂單項目列表
    /// </summary>
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 建立訂單項目請求
/// </summary>
public class CreateOrderItemRequest
{
    /// <summary>
    /// 產品編號
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// 數量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 備註
    /// </summary>
    public string? Notes { get; set; }
}