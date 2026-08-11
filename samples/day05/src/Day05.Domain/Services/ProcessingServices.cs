using Day05.Domain.Models;

namespace Day05.Domain.Services;

/// <summary>
/// class SlowService - 緩慢服務
/// </summary>
public class SlowService
{
    /// <summary>
    /// 處理慢速任務
    /// </summary>
    public async Task ProcessAsync()
    {
        await Task.Delay(2000); // 模擬耗時操作
    }
}

/// <summary>
/// class CancellableService - 可取消服務
/// </summary>
public class CancellableService
{
    /// <summary>
    /// 執行可取消的長時間操作
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task LongRunningOperationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken); // 模擬長時間操作
    }
}

/// <summary>
/// class ParallelService - 並行服務
/// </summary>
public class ParallelService
{
    /// <summary>
    /// 處理項目
    /// </summary>
    /// <param name="itemId">項目ID</param>
    /// <returns>處理結果</returns>
    public async Task<string> ProcessItemAsync(int itemId)
    {
        await Task.Delay(100); // 模擬處理時間
        return $"Processed_{itemId}";
    }
}

/// <summary>
/// class DatabaseService - 數據庫服務
/// </summary>
public class DatabaseService
{
    /// <summary>
    /// 連接到數據庫
    /// </summary>
    /// <param name="connectionString">連接字串</param>
    public void Connect(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("invalid"))
        {
            throw new DatabaseConnectionException(
                "無效的連線字串",
                new ArgumentException("連線字串不可為無效值"));
        }
    }
}

/// <summary>
/// class BatchProcessingService - 批量處理服務
/// </summary>
public class BatchProcessingService
{
    /// <summary>
    /// 處理批量任務
    /// </summary>
    public void ProcessBatch(object[] items)
    {
        var exceptions = new List<ValidationException>();

        foreach (var item in items)
        {
            try
            {
                if (item is null)
                {
                    throw new ValidationException("項目不可為空值");
                }
            }
            catch (ValidationException ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count != 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}

/// <summary>
/// class PaymentService - 付款服務
/// </summary>
public class PaymentService
{
    /// <summary>
    /// 處理付款請求
    /// </summary>
    /// <param name="request">付款請求</param>
    /// <returns>付款結果</returns>
    public PaymentResult ProcessPayment(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "付款金額無效",
                ErrorCode = "INVALID_AMOUNT"
            };
        }

        return new PaymentResult
        {
            IsSuccess = true,
            TransactionId = Guid.NewGuid().ToString()
        };
    }
}

/// <summary>
/// class DataProcessor - 數據處理器
/// </summary>
public class DataProcessor
{
    /// <summary>
    /// 處理大型數據集
    /// </summary>
    /// <param name="dataset">數據集</param>
    /// <returns>處理後的數據集</returns>
    public List<DataRecord> ProcessLargeDataset(List<DataRecord> dataset)
    {
        return dataset.Select(d => new DataRecord
        {
            Id = d.Id,
            Value = d.Value,
            IsProcessed = true
        })
                      .ToList();
    }
}

/// <summary>
/// class ComplexService - 複雜服務
/// </summary>
public class ComplexService
{
    /// <summary>
    /// 處理複雜實體
    /// </summary>
    /// <returns>處理後的複雜物件</returns>
    public ComplexObject ProcessEntity()
    {
        return new ComplexObject
        {
            ImportantProperty1 = "Value1",
            ImportantProperty2 = "Value2",
            CriticalData = "CriticalValue",
            Timestamp = DateTime.Now,
            GeneratedId = Guid.NewGuid()
        };
    }

    /// <summary>
    /// 生成複雜物件
    /// </summary>
    /// <returns>新的複雜物件</returns>
    public ComplexObject GenerateComplexObject()
    {
        return new ComplexObject
        {
            ImportantProperty1 = "Value1",
            ImportantProperty2 = "Value2",
            CriticalData = "CriticalValue",
            Timestamp = DateTime.Now,
            GeneratedId = Guid.NewGuid()
        };
    }
}

/// <summary>
/// class HeavyComputationService - 重量級計算服務
/// </summary>
public class HeavyComputationService
{
    /// <summary>
    /// 處理大型數據集
    /// </summary>
    /// <param name="dataSet">數據集</param>
    /// <returns>處理結果</returns>
    public string ProcessLargeDataSet(List<DataRecord> dataSet)
    {
        // 模擬重量級計算
        Thread.Sleep(1000);
        return $"Processed {dataSet.Count} items";
    }
}