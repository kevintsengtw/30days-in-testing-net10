namespace Day04.Domain.Models;

/// <summary>
/// interface IUser - 用於表示用戶的接口。
/// </summary>
public interface IUser
{
    /// <summary>
    /// 用戶ID
    /// </summary>
    /// <value></value>
    int Id { get; set; }

    /// <summary>
    /// 用戶名稱
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 用戶電子郵件
    /// </summary>
    string Email { get; set; }

    /// <summary>
    /// 用戶是否活躍
    /// </summary>
    bool IsActive { get; set; }
}

/// <summary>
/// class User - 用於表示用戶的模型。
/// </summary>
public class User : IUser
{
    /// <summary>
    /// 用戶ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用戶名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 用戶電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 用戶年齡
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// 用戶是否活躍
    /// </summary>
    /// <value></value>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 用戶創建時間
    /// </summary>
    /// <value></value>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 用戶的個人資料
    /// </summary>
    public UserProfile? Profile { get; set; }
}

/// <summary>
/// class UserProfile - 用於表示用戶的個人資料。
/// </summary>
public class UserProfile
{
    /// <summary>
    /// 用戶年齡
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// 用戶所在城市
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 用戶電話
    /// </summary>
    public string? Phone { get; set; }
}