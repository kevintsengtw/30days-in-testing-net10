namespace Day04.Domain.Models;

/// <summary>
/// class Order - 用於表示訂單的模型。
/// </summary>
public class Order
{
    /// <summary>
    /// 訂單ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 客戶名稱
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// 訂單日期
    /// </summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 訂單項目
    /// </summary>
    public List<OrderItem> Items { get; set; } = new();

    /// <summary>
    /// 運送地址
    /// </summary>
    public Address? ShippingAddress { get; set; }

    /// <summary>
    /// 訂單總金額
    /// </summary>
    public decimal TotalAmount => Items.Sum(item => item.Price * item.Quantity);
}

/// <summary>
/// class OrderItem - 用於表示訂單項目的模型。
/// </summary>
public class OrderItem
{
    /// <summary>
    /// 訂單項目ID
    /// </summary>
    /// <value></value>
    public int ProductId { get; set; }

    /// <summary>
    /// 訂單項目名稱
    /// </summary>
    /// <value></value>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 訂單項目數量
    /// </summary>
    /// <value></value>
    public int Quantity { get; set; }

    /// <summary>
    /// 訂單項目價格
    /// </summary>
    /// <value></value>
    public decimal Price { get; set; }
}

/// <summary>
/// class Address - 用於表示地址的模型。
/// </summary>
public class Address
{
    /// <summary>
    /// 地址街道
    /// </summary>
    /// <value></value>
    public string Street { get; set; } = string.Empty;

    /// <summary>
    /// 城市
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 州/省
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 郵遞區號
    /// </summary>
    public string ZipCode { get; set; } = string.Empty;
}