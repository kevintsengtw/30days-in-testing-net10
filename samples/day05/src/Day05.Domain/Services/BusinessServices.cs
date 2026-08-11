using Day05.Domain.Models;

namespace Day05.Domain.Services;

/// <summary>
/// class TreeService - 樹形結構服務
/// </summary>
public class TreeService
{
    /// <summary>
    /// 獲取樹形結構
    /// </summary>
    /// <param name="rootValue">根節點值</param>
    /// <returns>樹形結構根節點</returns>
    public TreeNode GetTree(string rootValue)
    {
        var parent = new TreeNode { Value = rootValue };
        var child1 = new TreeNode { Value = "Child1", Parent = parent };
        var child2 = new TreeNode { Value = "Child2", Parent = parent };
        parent.Children = [child1, child2];

        return parent;
    }
}

/// <summary>
/// class ProductService - 產品服務
/// </summary>
public class ProductService
{
    /// <summary>
    /// 創建產品
    /// </summary>
    /// <param name="name">產品名稱</param>
    /// <param name="price">產品價格</param>
    /// <returns>創建的產品</returns>
    public Product Create(string name, decimal price)
    {
        return new Product
        {
            Id = Random.Shared.Next(1, 1000),
            Name = name,
            Price = price,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}

/// <summary>
/// class OrderService - 訂單服務
/// </summary>
public class OrderService
{
    /// <summary>
    /// 創建訂單
    /// </summary>
    /// <param name="items">訂單項目</param>
    /// <returns>創建的訂單</returns>
    public Order CreateOrder(OrderItem[] items)
    {
        return new Order
        {
            Id = Random.Shared.Next(1, 1000),
            CustomerName = "Test Customer",
            TotalAmount = items.Sum(i => i.Price * i.Quantity),
            Status = "Pending",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = items,
            AuditInfo = new AuditInfo
            {
                CreatedBy = "system",
                CreatedAt = DateTime.Now,
                ModifiedBy = "system",
                ModifiedAt = DateTime.Now
            }
        };
    }

    /// <summary>
    /// 處理訂單
    /// </summary>
    /// <param name="orderId">訂單ID</param>
    /// <returns>處理後的訂單</returns>
    public Order ProcessOrder(int orderId)
    {
        // 模擬處理邏輯
        Thread.Sleep(100);

        return new Order
        {
            Id = orderId,
            CustomerName = "Test Customer",
            TotalAmount = 100.0m,
            Status = "Processing",
            CreatedAt = DateTime.Now.AddHours(-1),
            UpdatedAt = DateTime.Now,
            ProcessedAt = DateTime.Now,
            Items = [],
            AuditInfo = new AuditInfo
            {
                CreatedBy = "system",
                CreatedAt = DateTime.Now.AddHours(-1),
                ModifiedBy = "processor",
                ModifiedAt = DateTime.Now
            }
        };
    }

    /// <summary>
    /// 獲取訂單
    /// </summary>
    /// <param name="id">訂單ID</param>
    /// <returns>訂單</returns>
    public Order GetOrder(int id)
    {
        return new Order
        {
            Id = id,
            CustomerName = "John Doe",
            TotalAmount = 100.0m,
            Status = "Completed",
            CreatedAt = DateTime.Now.AddDays(-1),
            UpdatedAt = DateTime.Now,
            Items =
            [
                new OrderItem
                {
                    Id = 1,
                    ProductName = "Laptop",
                    Price = 100.0m,
                    Quantity = 1,
                    AddedAt = DateTime.Now.AddDays(-1),
                    ModifiedAt = DateTime.Now
                }
            ],
            AuditInfo = new AuditInfo
            {
                CreatedBy = "system",
                CreatedAt = DateTime.Now.AddDays(-1),
                ModifiedBy = "system",
                ModifiedAt = DateTime.Now
            }
        };
    }

    /// <summary>
    /// 批量處理訂單
    /// </summary>
    /// <param name="orders">訂單列表</param>
    /// <returns>處理後的訂單列表</returns>
    public List<Order> ProcessOrderBatch(List<Order> orders)
    {
        return orders.Select(o => new Order
        {
            Id = o.Id,
            CustomerName = o.CustomerName,
            TotalAmount = o.TotalAmount,
            Status = "Processed",
            CreatedAt = o.CreatedAt,
            UpdatedAt = DateTime.Now,
            ProcessedAt = DateTime.Now,
            Items = o.Items
        }).ToList();
    }
}