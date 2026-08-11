namespace Day05.Domain.Models;

/// <summary>
/// class TreeNode - 樹狀結構節點
/// </summary>
public class TreeNode
{
    /// <summary>
    /// 節點值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 父節點
    /// </summary>
    public TreeNode? Parent { get; set; }

    /// <summary>
    /// 子節點
    /// </summary>
    public TreeNode[] Children { get; set; } = [];
}

/// <summary>
/// class Product - 產品
/// </summary>
public class Product
{
    /// <summary>
    /// 產品識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 產品名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 產品價格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 產品建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 產品更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// class Order - 訂單
/// </summary>
public class Order
{
    /// <summary>
    /// 訂單識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 客戶名稱
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// 訂單總金額
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 訂單狀態
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 訂單建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 訂單更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 訂單處理時間
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// 訂單項目
    /// </summary>
    public OrderItem[] Items { get; set; } = [];

    /// <summary>
    /// 審計資訊
    /// </summary>
    public AuditInfo? AuditInfo { get; set; }
}

/// <summary>
/// class OrderItem - 訂單項目
/// </summary>
public class OrderItem
{
    /// <summary>
    /// 訂單項目識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 產品識別碼
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// 產品名稱
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 訂單項目數量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 訂單項目單價
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 訂單項目新增時間
    /// </summary>
    public DateTime AddedAt { get; set; }

    /// <summary>
    /// 訂單項目修改時間
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// class AuditInfo - 審計資訊
/// </summary>
public class AuditInfo
{
    /// <summary>
    /// 建立者
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 修改者
    /// </summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// 修改時間
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// class User - 使用者
/// </summary>
public class User
{
    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 使用者電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 使用者角色
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 使用者權限
    /// </summary>
    public string[] Permissions { get; set; } = [];

    /// <summary>
    /// 使用者建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 使用者更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 使用者資料行版本
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// 使用者個人資料
    /// </summary>
    public UserProfile? Profile { get; set; }
}

/// <summary>
/// class UserProfile - 使用者個人資料
/// </summary>
public class UserProfile
{
    /// <summary>
    /// 名字
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// 姓氏
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// 出生日期
    /// </summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string Address { get; set; } = string.Empty;
}

/// <summary>
/// class UserEntity - 使用者實體
/// </summary>
public class UserEntity
{
    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 使用者電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 使用者建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 使用者更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 使用者資料行版本
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 最後修改者
    /// </summary>
    public string? LastModifiedBy { get; set; }
}

/// <summary>
/// class DataRecord - 資料記錄
/// </summary>
public class DataRecord
{
    /// <summary>
    /// 資料識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 資料值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 是否已處理
    /// </summary>
    public bool IsProcessed { get; set; }
}

/// <summary>
/// class ComplexObject - 複雜物件
/// </summary>
public class ComplexObject
{
    /// <summary>
    /// 重要屬性1
    /// </summary>
    public string ImportantProperty1 { get; set; } = string.Empty;

    /// <summary>
    /// 重要屬性2
    /// </summary>
    public string ImportantProperty2 { get; set; } = string.Empty;

    /// <summary>
    /// 關鍵資料
    /// </summary>
    public string CriticalData { get; set; } = string.Empty;

    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 產生的識別碼
    /// </summary>
    public Guid GeneratedId { get; set; }
}

/// <summary>
/// class PaymentRequest - 付款請求
/// </summary>
public class PaymentRequest
{
    /// <summary>
    /// 付款金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 付款幣別
    /// </summary>
    public string Currency { get; set; } = "TWD";

    /// <summary>
    /// 付款方式
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;
}

/// <summary>
/// class PaymentResult - 付款結果
/// </summary>
public class PaymentResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤代碼
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// 交易識別碼
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;
}