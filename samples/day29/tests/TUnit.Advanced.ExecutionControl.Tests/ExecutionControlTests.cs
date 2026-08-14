using System.Net;

namespace TUnit.Advanced.ExecutionControl.Tests;

/// <summary>
/// 測試屬性常數定義：確保屬性命名的一致性
/// </summary>
public static class TestProperties
{
    // 測試類別
    public const string CATEGORY_UNIT = "Unit";
    public const string CATEGORY_INTEGRATION = "Integration";
    public const string CATEGORY_E2E = "E2E";
    public const string CATEGORY_FLAKY = "Flaky";
    public const string CATEGORY_PERFORMANCE = "Performance";

    // 優先級
    public const string PRIORITY_CRITICAL = "Critical";
    public const string PRIORITY_HIGH = "High";
    public const string PRIORITY_MEDIUM = "Medium";
    public const string PRIORITY_LOW = "Low";

    // 測試套件
    public const string SUITE_SMOKE = "Smoke";
    public const string SUITE_REGRESSION = "Regression";
    public const string SUITE_NEWFEATURE = "NewFeature";
}

/// <summary>
/// 執行控制功能測試：展示 Retry、Timeout、DisplayName 和 Properties 的使用
/// </summary>
public class ExecutionControlTests
{
    private static int _networkAttempts;
    private static int _httpAttempts;

    [Test]
    [Retry(3)]
    [NotInParallel("RetryExamples")]
    [Property("Category", TestProperties.CATEGORY_FLAKY)]
    [Property("Priority", TestProperties.PRIORITY_HIGH)]
    public async Task NetworkCall_可能不穩定_使用重試機制()
    {
        // 用可重現的方式模擬前兩次暫時性失敗，避免測試本身帶有隨機性。
        var attempt = Interlocked.Increment(ref _networkAttempts);
        if (attempt < 3)
        {
            throw new HttpRequestException($"第 {attempt} 次呼叫發生暫時性錯誤");
        }

        await Assert.That(attempt).IsEqualTo(3);
    }

    [Test]
    [Timeout(5000)] // 5 秒超時
    [Property("Category", TestProperties.CATEGORY_PERFORMANCE)]
    [Property("Priority", TestProperties.PRIORITY_MEDIUM)]
    public async Task LongRunningOperation_應在時限內完成(CancellationToken cancellationToken)
    {
        // 模擬可能會很慢的操作
        await Task.Delay(1000, cancellationToken); // 1 秒操作，應該在 5 秒限制內

        var result = true; // 模擬操作結果
        await Assert.That(result).IsTrue();
    }

    [Test]
    [DisplayName("自訂測試名稱：驗證使用者註冊流程")]
    [Property("Category", TestProperties.CATEGORY_UNIT)]
    [Property("Priority", TestProperties.PRIORITY_CRITICAL)]
    public async Task UserRegistration_CustomDisplayName_測試名稱更易讀()
    {
        // 使用自訂顯示名稱讓測試報告更容易理解
        var email = "user@example.com";
        await Assert.That(email).Contains("@");
    }

    [Test]
    [Arguments("valid@email.com", true)]
    [Arguments("invalid-email", false)]
    [Arguments("", false)]
    [DisplayName("電子郵件驗證：{0} 應為 {1}")]
    [Property("Category", TestProperties.CATEGORY_UNIT)]
    [Property("Priority", TestProperties.PRIORITY_HIGH)]
    public async Task EmailValidation_參數化顯示名稱(string email, bool expectedValid)
    {
        // 顯示名稱會自動替換參數
        var isValid = !string.IsNullOrEmpty(email) && email.Contains("@");

        await Assert.That(isValid).IsEqualTo(expectedValid);
    }

    // 測試套件組織範例
    [Test]
    [Property("Suite", TestProperties.SUITE_SMOKE)]
    [Property("Priority", TestProperties.PRIORITY_CRITICAL)]
    public async Task SmokeTest_基本功能_必須通過()
    {
        // 冒煙測試：快速驗證基本功能
        var applicationHealthy = true; // 模擬應用程式健康檢查
        await Assert.That(applicationHealthy).IsTrue();
    }

    [Test]
    [Property("Suite", TestProperties.SUITE_REGRESSION)]
    [Property("Feature", "OrderProcessing")]
    [Property("Priority", TestProperties.PRIORITY_MEDIUM)]
    public async Task RegressionTest_訂單處理_既有功能正常()
    {
        // 回歸測試套件：確保既有功能沒有被破壞
        var orderProcessingWorks = true; // 模擬訂單處理功能
        await Assert.That(orderProcessingWorks).IsTrue();
    }

    [Test]
    [Property("Suite", TestProperties.SUITE_NEWFEATURE)]
    [Property("Version", "2.1")]
    [Property("Priority", TestProperties.PRIORITY_LOW)]
    public async Task NewFeature_版本2點1_新增功能驗證()
    {
        // 新功能測試套件：驗證新開發的功能
        var newFeatureWorking = true; // 模擬新功能狀態
        await Assert.That(newFeatureWorking).IsTrue();
    }

    [Test]
    [Retry(3)]
    [NotInParallel("RetryExamples")]
    [Property("Category", TestProperties.CATEGORY_FLAKY)]
    public async Task CallApi_暫時性HTTP錯誤時重試_第三次應成功()
    {
        using var httpClient = new HttpClient(new TransientFailureHandler());
        using var response = await httpClient.GetAsync("https://example.test/posts/1");

        // 前兩次的 503 會由 EnsureSuccessStatusCode 轉成 HttpRequestException，觸發 Retry。
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(content).Contains("\"id\":1");
        await Assert.That(Volatile.Read(ref _httpAttempts)).IsEqualTo(3);
    }

    [Test]
    [Timeout(30000)] // 30 秒超時，適合較複雜的操作
    [Property("Category", "Integration")]
    public async Task DatabaseMigration_大量資料處理_應在合理時間內完成(CancellationToken cancellationToken)
    {
        // 模擬資料庫遷移或大量資料處理
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            tasks.Add(ProcessDataBatch(i, cancellationToken));
        }

        await Task.WhenAll(tasks);
        await Assert.That(tasks.All(t => t.IsCompletedSuccessfully)).IsTrue();
    }

    private static async Task ProcessDataBatch(int batchNumber, CancellationToken cancellationToken)
    {
        // 模擬批次處理
        await Task.Delay(50, cancellationToken); // 每批次 50ms
    }

    [Test]
    [Timeout(5000)] // 只防止工作失控，不把 wall-clock 時間當成 SLA
    [Property("Category", "Performance")]
    public async Task SearchFunction_逾時保護_應完成搜尋(CancellationToken cancellationToken)
    {
        // 模擬搜尋功能
        var searchResults = await PerformSearch("test query", cancellationToken);

        await Assert.That(searchResults).IsNotNull();
        await Assert.That(searchResults.Count()).IsGreaterThan(0);
    }

    private static async Task<IEnumerable<string>> PerformSearch(string query, CancellationToken cancellationToken)
    {
        // 模擬搜尋邏輯
        await Task.Delay(100, cancellationToken);
        return new[] { "result1", "result2", "result3" };
    }

    private sealed class TransientFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = Interlocked.Increment(ref _httpAttempts);

            var response = attempt < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":1,\"title\":\"deterministic response\"}")
                };

            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    [Test]
    [Arguments(CustomerLevel.一般會員, 1000, 0)]
    [Arguments(CustomerLevel.VIP會員, 1000, 50)]
    [Arguments(CustomerLevel.白金會員, 1000, 100)]
    [Arguments(CustomerLevel.鑽石會員, 1000, 200)]
    [DisplayName("會員等級 {0} 購買 ${1} 應獲得 ${2} 折扣")]
    public async Task MemberDiscount_根據會員等級_計算正確折扣(CustomerLevel level, decimal amount, decimal expectedDiscount)
    {
        // 這樣的測試報告讀起來像業務需求
        // 為了測試展示，我們直接計算而不使用完整的依賴注入
        var discount = level switch
        {
            CustomerLevel.一般會員 => 0,
            CustomerLevel.VIP會員 => amount * 0.05m, // 5% 折扣
            CustomerLevel.白金會員 => amount * 0.10m,  // 10% 折扣
            CustomerLevel.鑽石會員 => amount * 0.20m,  // 20% 折扣
            _ => 0
        };

        await Assert.That(discount).IsEqualTo(expectedDiscount);
    }
}
