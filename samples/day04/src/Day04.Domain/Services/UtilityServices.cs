namespace Day04.Domain.Services;

/// <summary>
/// class Calculator - 用於執行數學計算的服務。
/// </summary>
public class Calculator
{
    /// <summary>
    /// 加法
    /// </summary>
    /// <param name="a">第一個數字</param>
    /// <param name="b">第二個數字</param>
    /// <returns>兩個數字的和</returns>
    public int Add(int a, int b)
    {
        return a + b;
    }

    /// <summary>
    /// 除法
    /// </summary>
    /// <param name="a">第一個數字</param>
    /// <param name="b">第二個數字</param>
    /// <returns>兩個數字的商</returns>
    public double Divide(int a, int b)
    {
        if (b == 0)
        {
            throw new DivideByZeroException("除數不能為零");
        }

        return (double)a / b;
    }

    /// <summary>
    /// 乘法
    /// </summary>
    /// <param name="a">第一個數字</param>
    /// <param name="b">第二個數字</param>
    /// <returns>兩個數字的積</returns>
    public int Multiply(int a, int b)
    {
        return a * b;
    }

    /// <summary>
    /// 減法
    /// </summary>
    /// <param name="a">第一個數字</param>
    /// <param name="b">第二個數字</param>
    /// <returns>兩個數字的差</returns>
    public int Subtract(int a, int b)
    {
        return a - b;
    }
}

/// <summary>
/// class ValidationService - 用於驗證輸入的服務。
/// </summary>
public class ValidationService
{
    /// <summary>
    /// 驗證電子郵件格式
    /// </summary>
    /// <param name="email">電子郵件地址</param>
    /// <returns>是否有效</returns>
    public bool ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("電子郵件不能為空", nameof(email));
        }

        if (!email.Contains('@'))
        {
            throw new ArgumentException("電子郵件必須包含 @ 符號", nameof(email));
        }

        return true;
    }
}

/// <summary>
/// class ApiService - 用於調用外部 API 的服務。
/// </summary>
public class ApiService
{
    /// <summary>
    /// 獲取資料
    /// </summary>
    /// <param name="endpoint">API 端點</param>
    /// <returns>API 回傳的資料</returns>
    public async Task<string> GetDataAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint 不能為空", nameof(endpoint));
        }

        if (endpoint.Contains("invalid"))
        {
            throw new HttpRequestException("404 - 找不到 Endpoint");
        }

        // 模擬 API 呼叫
        await Task.Delay(200);
        return $"Data from {endpoint}";
    }
}