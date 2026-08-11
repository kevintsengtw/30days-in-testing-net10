using System.Net;

namespace Day04.Domain.Models;

/// <summary>
/// class CreateUserRequest - 用於創建使用者的請求模型。
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// 使用者電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 使用者年齡
    /// </summary>
    public int Age { get; set; }
}

/// <summary>
/// class ApiResponse - 用於API回應的模型。
/// </summary>
/// <typeparam name="T"></typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// API回應的狀態碼
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>
    /// API回應的資料
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// API回應的處理時間
    /// </summary>
    /// <value></value>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// API回應的錯誤訊息
    /// </summary>
    /// <value></value>
    public string? ErrorMessage { get; set; }
}