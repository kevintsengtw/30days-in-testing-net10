namespace AutoFixtureBogusMix.Core.Models;

/// <summary>
/// 使用者實體
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age { get; set; }
    public Address? HomeAddress { get; set; }
    public Company? Company { get; set; }
    public List<Order> Orders { get; set; } = new();
}

/// <summary>
/// 地址實體
/// </summary>
public class Address
{
    public Guid Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// 公司實體
/// </summary>
public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address? Address { get; set; }
    public List<User> Employees { get; set; } = new();
}

/// <summary>
/// 訂單實體
/// </summary>
public class Order
{
    public Guid Id { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public User Customer { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; }
}

/// <summary>
/// 訂單項目實體
/// </summary>
public class OrderItem
{
    public Guid Id { get; set; }
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

/// <summary>
/// 產品實體
/// </summary>
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// 訂單狀態列舉
/// </summary>
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}