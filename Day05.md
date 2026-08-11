---
day: 5
title: "Day 05 - AwesomeAssertions 進階技巧與複雜情境應用"
sample: samples/day05
target_framework: net10.0
packages:
  - AwesomeAssertions
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Microsoft.Testing.Extensions.TrxReport
---

# Day 05 - AwesomeAssertions 進階技巧與複雜情境應用

## 今日目標

Day 04 介紹了 AwesomeAssertions 的基礎用法。今天接著處理複雜物件比對、自訂 Assertions 擴充與動態欄位，讓斷言更貼近實際測試需求。

### 學習重點

- **複雜物件比對技巧**：掌握 Object Graph 比較與最佳化策略
- **自訂 Assertions 擴充**：建立領域特定的 Assertions 方法
- **動態欄位排除**：處理時間戳記與自動產生欄位
- **效能最佳化 Assertions**：處理大量資料的 Assertions 策略
- **實戰應用技巧**：錯誤訊息最佳化與團隊標準建立

### 前置準備

**關於 AwesomeAssertions**：

AwesomeAssertions 是 FluentAssertions 的社群分支版本，使用 Apache 2.0 授權。本章節使用 **AwesomeAssertions 9.5.0** 版本，該版本的 API 與 FluentAssertions 相容度很高，但有以下主要差異：

- **命名空間**：從 `FluentAssertions` 改為 `AwesomeAssertions`
- **API 命名**：`EquivalencyAssertionOptions` 改為 `EquivalencyOptions`
- **屬性路徑**：`SelectedMemberPath` 改為 `Path`

相關連結：

- [AwesomeAssertions - releases](https://github.com/AwesomeAssertions/AwesomeAssertions/releases)

本章節的所有測試範例都需要以下 using 指令：

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;
```

**注意**：需要輸出測試診斷資訊的測試類別（例如本範例的 `PerformanceOptimizedTests`）會使用 `ITestOutputHelper`，這是 xUnit 推薦的最佳實務，應避免使用 `Console.WriteLine()`。並非每個測試類別都需要輸出資訊，只有在確實要觀察執行過程時才注入它。

---

## 進階 Assertions 技巧與實戰應用

### Object Graph 進階比對技巧

#### 循環參考與複雜結構處理

```csharp
public class AdvancedObjectGraphTests
{
    [Fact]
    public void ObjectGraph_循環引用處理_應正常運作()
    {
        // 處理循環參考的物件比較
        var parent = new TreeNode { Value = "Root" };
        var child1 = new TreeNode { Value = "Child1", Parent = parent };
        var child2 = new TreeNode { Value = "Child2", Parent = parent };
        parent.Children = [child1, child2];

        var treeService = new TreeService();
        var actualTree = treeService.GetTree("Root");

        // 使用循環參考處理
        actualTree.Should().BeEquivalentTo(parent, options =>
            options.IgnoringCyclicReferences()
        );
    }

    [Fact]
    public void ObjectGraph_效能最佳化比較_應正常運作()
    {
        var largeObjectGraph = new ComplexObject
        {
            ImportantProperty1 = "Value1",
            ImportantProperty2 = "Value2",
            CriticalData = "CriticalValue",
            Timestamp = DateTime.Now,
            GeneratedId = Guid.NewGuid()
        };

        var complexService = new ComplexService();
        var actualGraph = complexService.ProcessEntity();

        // 效能最佳化的比較策略
        actualGraph.Should().BeEquivalentTo(largeObjectGraph, options =>
            options.WithStrictOrdering()
                   .WithTracing() // 啟用詳細追蹤，便於除錯
                   .Including(x => x.ImportantProperty1) // 只比較關鍵屬性
                   .Including(x => x.ImportantProperty2)
                   .Including(x => x.CriticalData)
        );
    }
}
```

---

## 進階非同步 Assertions 技巧

### 複雜非同步情境處理

#### 執行時間與效能 Assertions

```csharp
public class AdvancedAsyncAssertionTests
{
    [Fact]
    public async Task AsyncAssertion_執行時間_應正常運作()
    {
        var service = new SlowService();
        
        // 執行時間Assertions
        Func<Task> asyncAction = () => service.ProcessAsync();
        
        await asyncAction.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }
    
    [Fact]
    public async Task AsyncAssertion_CancellationToken_應正常運作()
    {
        var service = new CancellableService();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // 取消令牌Assertions
        Func<Task> asyncAction = () => service.LongRunningOperationAsync(cts.Token);
        
        await asyncAction.Should().ThrowAsync<OperationCanceledException>();
    }

    // 並行 (Concurrency)
    // 同一段時間內有多個工作在「進行中」，不一定同時執行，可能交替進行。重點是任務可以同時啟動，但不保證同時執行。
    
    [Fact]
    public async Task AsyncAssertion_並行處理_應正常運作()
    {
        var service = new ParallelService();
        var tasks = Enumerable.Range(1, 10)
            .Select(i => service.ProcessItemAsync(i))
            .ToArray();
        
        // 並行任務Assertions
        await Task.WhenAll(tasks);
        
        tasks.Should().AllSatisfy(task => 
        {
            task.Status.Should().Be(TaskStatus.RanToCompletion);
            task.Result.Should().NotBeNull();
        });
    }
}
```

### 例外 Assertions 進階技巧

#### 複雜例外情境處理

```csharp
public class AdvancedExceptionAssertionTests
{
    [Fact]
    public void Exception_巢狀例外_應拋出例外DatabaseConnectionException與ArgumentException()
    {
        var databaseService = new DatabaseService();
        
        // 巢狀例外 Assertions
        Action action = () => databaseService.Connect("invalid-connection-string");
        
        action.Should().Throw<DatabaseConnectionException>()
              .WithInnerException<ArgumentException>()
              .WithMessage("*連線字串不可為無效值*");
    }
    
    [Fact]
    public void Exception_聚合例外_應拋出例外AggregateException與ValidationException()
    {
        var batchService = new BatchProcessingService();
        var invalidItems = new object[] { null!, null!, null! };
        
        // 聚合例外Assertions
        Action action = () => batchService.ProcessBatch(invalidItems);
        
        action.Should().Throw<AggregateException>()
              .Which.InnerExceptions.Should().HaveCount(3)
              .And.AllBeOfType<ValidationException>();
    }
}
```

---

## 自訂 Assertions 擴充與最佳實務

### 建立專案特定的 Assertions 方法

#### 領域特定 Assertions

```csharp
// 為電商專案建立的自訂Assertions
public static class ECommerceAssertions
{
    public static AndConstraint<ObjectAssertions> BeValidProduct(this ObjectAssertions assertions)
    {
        var product = assertions.Subject as Product;
        
        product.Should().NotBeNull("預期為 Product 物件，但找到 {0}", assertions.Subject?.GetType());
        product!.Id.Should().BeGreaterThan(0, "預期 Product.Id 大於 0，但找到 {0}", product.Id);
        product.Name.Should().NotBeNullOrEmpty("預期 Product.Name 不為 null 或空值");
        product.Price.Should().BeGreaterThan(0, "預期 Product.Price 大於 0，但找到 {0}", product.Price);
            
        return new AndConstraint<ObjectAssertions>(assertions);
    }
    
    public static AndConstraint<ObjectAssertions> BeValidOrder(this ObjectAssertions assertions)
    {
        var order = assertions.Subject as Order;
        
        order.Should().NotBeNull("預期為 Order 物件");
        order!.Items.Should().NotBeNullOrEmpty("預期 Order 至少包含一個項目");
        order.TotalAmount.Should().BeGreaterThan(0, "預期 Order.TotalAmount 大於 0");
            
        return new AndConstraint<ObjectAssertions>(assertions);
    }

    public static void ShouldBeValidUser(this User user)
    {
        user.Should().NotBeNull();
        user.Id.Should().BeGreaterThan(0);
        user.Email.Should().NotBeNullOrEmpty();
    }

    public static void ShouldBeComplete(this UserProfile? profile)
    {
        profile.Should().NotBeNull();
        profile!.FirstName.Should().NotBeNullOrEmpty();
        profile.LastName.Should().NotBeNullOrEmpty();
    }
}

// 使用自訂Assertions
public class ProductServiceTests
{
    [Fact]
    public void CreateProduct_有效資料_應該回傳有效產品()
    {
        var productService = new ProductService();
        var product = productService.Create("Laptop", 999.99m);
        
        // 使用領域特定Assertions
        product.Should().BeValidProduct();
        product.Name.Should().Be("Laptop");
    }
    
    [Fact]
    public void ProcessOrder_有效項目_應該回傳有效訂單()
    {
        var orderService = new OrderService();
        var items = new[]
        {
            new OrderItem { ProductId = 1, Quantity = 2, Price = 50.0m },
            new OrderItem { ProductId = 2, Quantity = 1, Price = 100.0m }
        };
        
        var order = orderService.CreateOrder(items);
        
        // 使用領域特定Assertions
        order.Should().BeValidOrder();
        order.Items.Should().HaveCount(2);
    }
}
```

#### 條件式 Assertions 建構器

```csharp
public static class ConditionalAssertions
{
    public static ConditionalAssertion<T> When<T>(this T subject, bool condition)
    {
        return new ConditionalAssertion<T>(subject, condition);
    }
}

public class ConditionalAssertion<T>
{
    private readonly T _subject;
    private readonly bool _condition;
    
    public ConditionalAssertion(T subject, bool condition)
    {
        _subject = subject;
        _condition = condition;
    }
    
    public ConditionalAssertion<T> Then(Action<T> assertion)
    {
        if (_condition)
        {
            assertion(_subject);
        }

        return this;
    }

    public ConditionalAssertion<T> When(bool condition)
    {
        return new ConditionalAssertion<T>(_subject, condition);
    }

    public ConditionalAssertion<T> And => this;
}

// 使用條件式Assertions
public class ConditionalAssertionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProcessUser_依據角色_應有正確權限(bool isAdmin)
    {
        var userService = new UserService();
        var user = userService.ProcessUser(isAdmin);
        
        user.Should().NotBeNull();
        user.When(isAdmin)
            .Then(u => u.Role.Should().Be("admin"))
            .And.When(isAdmin)
            .Then(u => u.Permissions.Should().Contain("DELETE"));
            
        user.When(!isAdmin)
            .Then(u => u.Role.Should().Be("user"))
            .And.When(!isAdmin)
            .Then(u => u.Permissions.Should().NotContain("DELETE"));
    }
}
```

### 效能最佳化 Assertions

#### 大量資料 Assertions 最佳化

```csharp
public class PerformanceOptimizedAssertions
{
    [Fact]
    public void LargeCollection_大型集合Assertions_應高效率執行()
    {
        var dataProcessor = new DataProcessor();

        // 模擬大量資料
        var largeDataset = Enumerable.Range(1, 100000)
            .Select(i => new DataRecord { Id = i, Value = $"Record_{i}" })
            .ToList();
        
        var processed = dataProcessor.ProcessLargeDataset(largeDataset);
        
        // 最佳化：使用高效率的Assertions策略
        processed.Should().HaveCount(largeDataset.Count);
        
        // 抽樣驗證而非全量驗證
        var sampleSize = Math.Min(1000, processed.Count / 10);
        var sampleIndices = Enumerable.Range(0, sampleSize)
            .Select(i => Random.Shared.Next(processed.Count))
            .Distinct()
            .ToList();
            
        foreach (var index in sampleIndices)
        {
            processed[index].Should().NotBeNull();
            processed[index].Id.Should().BeGreaterThan(0);
        }
        
        // 統計驗證
        processed.Count(r => r.IsProcessed).Should().Be(processed.Count);
    }
    
    [Fact]
    public void ComplexObject_複雜物件比較_應使用選擇性比較()
    {
        var complexService = new ComplexService();
        var complexObject = complexService.GenerateComplexObject();
        var expectedTemplate = new ComplexObject
        {
            ImportantProperty1 = "Value1",
            ImportantProperty2 = "Value2",
            CriticalData = "CriticalValue"
        }; // 預期範本
        
        // 選擇性比較，避免不必要的深度比較
        complexObject.Should().BeEquivalentTo(expectedTemplate, options =>
            options.Including(x => x.ImportantProperty1)
                   .Including(x => x.ImportantProperty2)
                   .Including(x => x.CriticalData)
                   .Excluding(x => x.Timestamp)  // 排除時間戳記
                   .Excluding(x => x.GeneratedId) // 排除自動產生欄位
        );
    }
}
```

---

## 快速排除更新欄位技巧

### 動態屬性排除策略

#### 時間戳記與自動產生欄位處理

```csharp
public class DynamicFieldExclusionTests
{
    [Fact]
    public void EntityComparison_排除時間戳記_應正常運作()
    {
        var entityService = new EntityService();
        var originalEntity = new UserEntity
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com",
            CreatedAt = DateTime.Now.AddDays(-1),
            UpdatedAt = DateTime.Now.AddDays(-1),
            Version = 1
        };
        
        var updatedEntity = entityService.UpdateUser(1, new UpdateUserRequest 
        { 
            Name = "John Doe",
            Email = "john@example.com" 
        });
        
        // 排除自動更新的時間戳記和版本欄位
        updatedEntity.Should().BeEquivalentTo(originalEntity, options =>
            options.Excluding(e => e.UpdatedAt)    // 自動更新的時間
                   .Excluding(e => e.CreatedAt)    // 也排除建立時間
                   .Excluding(e => e.Version)      // 樂觀鎖版本號
                   .Excluding(e => e.LastModifiedBy) // 修改者記錄
        );
        
        // 單獨驗證動態欄位
        updatedEntity.UpdatedAt.Should().BeAfter(originalEntity.UpdatedAt);
        updatedEntity.Version.Should().Be(originalEntity.Version + 1);
    }
    
    [Fact]
    public void EntityComparison_使用屬性表達式_應正常運作()
    {
        var userService = new UserService();
        var user1 = userService.CreateUser("test@example.com");
        var user2 = userService.GetUser(user1.Id);
        
        // 使用屬性表達式排除欄位
        user2.Should().BeEquivalentTo(user1, options =>
            options.Excluding(u => u.CreatedAt)
                   .Excluding(u => u.UpdatedAt)
                   .Excluding(u => u.RowVersion)
        );
    }
}
```

#### 巢狀物件排除策略

```csharp
public class NestedObjectExclusionTests
{
    [Fact]
    public void ComplexEntity_排除巢狀時間戳記_應正常運作()
    {
        var order = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            CreatedAt = DateTime.Now,
            Items = new[]
            {
                new OrderItem 
                { 
                    Id = 1, 
                    ProductName = "Laptop", 
                    AddedAt = DateTime.Now,
                    ModifiedAt = DateTime.Now 
                }
            },
            AuditInfo = new AuditInfo
            {
                CreatedBy = "system",
                CreatedAt = DateTime.Now,
                ModifiedBy = "system",
                ModifiedAt = DateTime.Now
            }
        };
        
        var orderService = new OrderService();
        var retrievedOrder = orderService.GetOrder(1);
        
        // 排除所有時間戳記欄位(包含巢狀物件)
        retrievedOrder.Should().BeEquivalentTo(order, options =>
            options.Excluding(o => o.CreatedAt)
                   .Excluding(o => o.UpdatedAt)
                   .Excluding(o => o.TotalAmount) // 因為實際值可能不同
                   .Excluding(o => o.Status)      // 因為實際值可能不同
                   .Excluding(o => o.Items[0].AddedAt)
                   .Excluding(o => o.Items[0].ModifiedAt)
                   .Excluding(o => o.Items[0].Price)    // 排除價格差異
                   .Excluding(o => o.Items[0].Quantity) // 排除數量差異
                   .Excluding(o => o.AuditInfo!.CreatedAt)
                   .Excluding(o => o.AuditInfo!.ModifiedAt)
        );
    }
    
    [Fact]
    public void ComplexEntity_使用萬用字元排除_應正常運作()
    {
        var complexService = new ComplexService();
        var entity = complexService.ProcessEntity();
        var expectedEntity = new ComplexObject
        {
            ImportantProperty1 = "Value1",
            ImportantProperty2 = "Value2",
            CriticalData = "CriticalValue",
            Timestamp = DateTime.Now.AddHours(-1),
            GeneratedId = Guid.NewGuid()
        };
        
        // 使用萬用字元排除所有時間相關欄位
        entity.Should().BeEquivalentTo(expectedEntity, options =>
            options.Excluding(ctx => ctx.Path.EndsWith("At"))
                   .Excluding(ctx => ctx.Path.EndsWith("Time"))
                   .Excluding(ctx => ctx.Path.Contains("Timestamp"))
                   .Excluding(ctx => ctx.Path.Contains("GeneratedId")) // 也排除 GUID
        );
    }
}
```

### 屬性模式排除

#### 基於命名慣例的排除

```csharp
public static class SmartExclusionExtensions
{
    public static EquivalencyOptions<T> ExcludingAutoGeneratedFields<T>(
        this EquivalencyOptions<T> options)
    {
        return options
            .Excluding(ctx => ctx.Path.EndsWith("At"))
            .Excluding(ctx => ctx.Path.EndsWith("Time"))
            .Excluding(ctx => ctx.Path.Contains("Version"))
            .Excluding(ctx => ctx.Path.Contains("RowVersion"))
            .Excluding(ctx => ctx.Path.Contains("Timestamp"));
    }
    
    public static EquivalencyOptions<T> ExcludingAuditFields<T>(
        this EquivalencyOptions<T> options)
    {
        return options
            .Excluding(ctx => ctx.Path.Contains("CreatedBy"))
            .Excluding(ctx => ctx.Path.Contains("CreatedAt"))
            .Excluding(ctx => ctx.Path.Contains("ModifiedBy"))
            .Excluding(ctx => ctx.Path.Contains("ModifiedAt"))
            .Excluding(ctx => ctx.Path.Contains("LastModified"));
    }
}

public class SmartExclusionTests
{
    [Fact]
    public void EntityComparison_使用智慧排除_應正常運作()
    {
        var userService = new UserService();
        var user = userService.CreateUser("test@example.com");
        var retrievedUser = userService.GetUser(user.Id);
        
        // 使用智慧排除擴充方法
        retrievedUser.Should().BeEquivalentTo(user, options =>
            options.ExcludingAutoGeneratedFields()
                   .ExcludingAuditFields()
        );
    }
    
    [Fact]
    public void ComplexEntity_組合排除策略_應正常運作()
    {
        var orderService = new OrderService();
        var orderRequest = new[]
        {
            new OrderItem { ProductId = 1, Quantity = 1, Price = 100.0m }
        };
        var order = orderService.CreateOrder(orderRequest);
        var processedOrder = orderService.ProcessOrder(order.Id);
        
        processedOrder.Should().BeEquivalentTo(order, options =>
            options.ExcludingAutoGeneratedFields()
                   .ExcludingAuditFields()
                   .Excluding(o => o.Status)  // 額外排除狀態欄位
                   .Excluding(o => o.ProcessedAt)
                   .Excluding(o => o.Items)   // 處理後的訂單沒有項目
        );
    }
}
```

---

## 效能最佳化與複雜情境處理

### 大量資料 Assertions 策略

#### 分批處理與效能最佳化

```csharp
// 大量資料比對的效能最佳化策略
public static class PerformanceOptimizedAssertions
{
    // 選擇性屬性比較，避免全物件掃描
    public static void AssertLargeDataSet<T>(IEnumerable<T> actual, IEnumerable<T> expected)
    {
        actual.Should().HaveCount(expected.Count());
        
        // 分批處理，避免記憶體壓力
        var actualBatches = actual.Chunk(1000);
        var expectedBatches = expected.Chunk(1000);
        
        actualBatches.Zip(expectedBatches, (a, e) => new { Actual = a, Expected = e })
                    .AsParallel()
                    .ForAll(batch =>
                    {
                        batch.Actual.Should().BeEquivalentTo(batch.Expected, options =>
                            options.Excluding(ctx => ctx.Path.Contains("UpdatedAt"))
                                   .Excluding(ctx => ctx.Path.Contains("CreatedAt")));
                    });
    }
    
    // 關鍵屬性快速比對
    public static void AssertKeyPropertiesOnly<T>(T actual, T expected, params Expression<Func<T, object>>[] keySelectors)
    {
        foreach (var selector in keySelectors)
        {
            var actualValue = selector.Compile()(actual);
            var expectedValue = selector.Compile()(expected);
            actualValue.Should().Be(expectedValue, $"關鍵屬性 {selector} 應該相符");
        }
    }
}

// 實際應用範例
public class PerformanceOptimizedTests
{
    private readonly ITestOutputHelper _output;
    
    public PerformanceOptimizedTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void LargeDataSet_大量資料驗證_應使用效能最佳化策略()
    {
        var userService = new UserService();

        // Arrange
        // 建立大量測試資料
        var expectedUsers = Enumerable.Range(1, 50000)
            .Select(i => new User { Id = i, Name = $"User{i}", Email = $"user{i}@example.com" })
            .ToList();
        
        // Act
        var actualUsers = userService.ProcessLargeUserBatch(expectedUsers);
        
        // Assert

        // 使用效能最佳化的 Assertions 方法
        PerformanceOptimizedAssertions.AssertLargeDataSet(actualUsers, expectedUsers);
        
        // 驗證：大量資料處理應該在合理時間內完成
        _output.WriteLine($"成功驗證 {expectedUsers.Count} 筆使用者資料");
    }
    
    [Fact]
    public void EntityComparison_關鍵屬性_應快速比對()
    {
        // Arrange
        var expectedOrder = new Order 
        { 
            Id = 1, 
            CustomerName = "John Doe", 
            TotalAmount = 999.99m,
            Status = "Processing",
            CreatedAt = DateTime.Now,  // 這個會變動
            UpdatedAt = DateTime.Now   // 這個會變動
        };
        
        // Act
        var orderService = new OrderService();
        var actualOrder = orderService.GetOrder(1);
        
        // Assert

        // 只比較關鍵屬性，忽略會變動的時間戳記
        PerformanceOptimizedAssertions.AssertKeyPropertiesOnly(
            actualOrder, 
            expectedOrder,
            o => o.Id,
            o => o.CustomerName
        );
        
        // 這種方式比完整的 BeEquivalentTo 快很多
        _output.WriteLine("快速驗證訂單關鍵屬性完成");
    }
    
    [Fact]
    public void ComplexScenario_結合多種最佳化策略_應高效率執行()
    {
        var orderService = new OrderService();
        var largeOrderBatch = GenerateLargeOrderBatch(10000);
        var processedOrders = orderService.ProcessOrderBatch(largeOrderBatch);
        
        var stopwatch = Stopwatch.StartNew();
        
        // 先用快速方法檢查數量
        processedOrders.Should().HaveCount(largeOrderBatch.Count);
        
        // 抽樣詳細驗證（只驗證前100筆）
        var sampleOrders = processedOrders.Take(100).ToList();
        var expectedSample = largeOrderBatch.Take(100).ToList();
        
        sampleOrders.Should().BeEquivalentTo(expectedSample, options =>
            options.Excluding(o => o.ProcessedAt)
                   .Excluding(o => o.UpdatedAt)
                   .Excluding(o => o.Status)); // 排除狀態，因為會被改變
        
        // 對剩餘的只做關鍵屬性驗證
        var remainingOrders = processedOrders.Skip(100).ToList();
        var expectedRemaining = largeOrderBatch.Skip(100).ToList();
        
        for (int i = 0; i < remainingOrders.Count; i++)
        {
            PerformanceOptimizedAssertions.AssertKeyPropertiesOnly(
                remainingOrders[i],
                expectedRemaining[i],
                o => o.Id,
                o => o.TotalAmount
            );
        }
        
        stopwatch.Stop();
        
        // 效能驗證
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "因為混合驗證策略應該在 5 秒內完成");
            
        _output.WriteLine($"混合策略驗證 {processedOrders.Count} 筆訂單，耗時 {stopwatch.ElapsedMilliseconds}ms");
    }
    
    private List<Order> GenerateLargeOrderBatch(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Order 
            { 
                Id = i, 
                CustomerName = $"Customer{i}",
                TotalAmount = i * 10.5m,
                Status = "Pending"
            })
            .ToList();
    }
}
```

#### 團隊培訓與標準化

```csharp
// 建立團隊測試指南
public class TeamTestingGuidelines
{
    // 標準 1：使用有意義的測試名稱
    [Fact]
    public void CreateUser_有效電子郵件_應回傳啟用的使用者()
    {
        // 遵循：被測試方法_測試情境_預期行為
        var userService = new UserService();
        var actual = userService.CreateUser("test@example.com");
        actual.Should().NotBeNull();
        actual.Email.Should().Be("test@example.com");
    }
    
    // 標準 2：使用流暢的Assertions風格
    [Fact]
    public void ProcessOrder_多個項目_應計算正確的總金額()
    {
        var orderService = new OrderService();
        var validItems = new[]
        {
            new OrderItem { ProductId = 1, Quantity = 2, Price = 50.0m },
            new OrderItem { ProductId = 2, Quantity = 1, Price = 100.0m }
        };

        var order = orderService.CreateOrder(validItems);
        
        // 推薦：鏈式Assertions，邏輯清晰
        order.Should().NotBeNull()
             .And.BeOfType<Order>();
        order.TotalAmount.Should().BeGreaterThan(0);
        order.Items.Should().HaveCount(validItems.Length);
    }
    
    // 標準 3：適當使用自訂Assertions
    [Fact] 
    public void RegisterUser_完整個人資料_應建立有效使用者()
    {
        var userService = new UserService();
        var completeProfile = new UserProfile
        {
            FirstName = "John",
            LastName = "Doe",
            Address = "123 Main St"
        };

        var user = userService.CreateUser("john.doe@example.com");
        user.Profile = completeProfile;
        
        // 使用領域特定Assertions
        user.ShouldBeValidUser();
        user.Profile.ShouldBeComplete();
    }
}
```

### 錯誤訊息最佳化技巧

#### 客製化錯誤訊息

```csharp
// 提供更有意義的錯誤訊息
public class ErrorMessageOptimizationTests
{
    [Fact]
    public void ProcessPayment_無效金額_應提供詳細錯誤訊息()
    {
        var paymentService = new PaymentService();
        var payment = new PaymentRequest { Amount = -100 };
        
        var result = paymentService.ProcessPayment(payment);
        
        // 提供詳細的錯誤上下文
        result.IsSuccess.Should().BeFalse("因為付款金額不可為負數");
        result.ErrorMessage.Should().Contain("金額", "因為錯誤訊息應明確指出有問題的欄位");
        result.ErrorCode.Should().Be("INVALID_AMOUNT", "因為具體的錯誤代碼有助於問題排查");
    }
    
    [Fact]
    public void ComplexObjectValidation_詳細失敗分析_應提供清楚的失敗原因()
    {
        var orderService = new OrderService();
        var invalidOrderData = new[]
        {
            new OrderItem { ProductId = 1, Quantity = 1, Price = 100.0m }
        };

        var order = orderService.CreateOrder(invalidOrderData);
        
        // 組合多個條件並提供清楚的失敗原因
        order.Should().NotBeNull("因為訂單建立不應該回傳 null")
             .And.Subject.As<Order>().Items.Should().NotBeEmpty("因為訂單必須包含至少一個項目")
             .And.OnlyContain(item => item.Price > 0, "因為所有項目的價格都必須大於零");
    }
}
```

#### 測試報告最佳化

```csharp
// 建立測試品質指標
public class TestQualityMetrics
{
    [Fact]
    public void TestAssertion_應提供可操作的資訊()
    {
        var userService = new UserService();
        var testUserData = new UserProfile
        {
            FirstName = "Test",
            LastName = "User"
        };

        var userRegistration = userService.RegisterUser(testUserData);
        
        // 分層驗證，提供階段性失敗資訊
        userRegistration.Should().NotBeNull("因為使用者註冊不應該完全失敗");
        userRegistration.UserId.Should().BeGreaterThan(0, "因為應該分配有效的使用者ID");
        userRegistration.Email.Should().Be("test@example.com", "因為電子郵件應該與輸入相符");
        userRegistration.IsEmailVerified.Should().BeFalse("因為電子郵件初始時應該尚未驗證");
    }
    
    [Fact]
    public void PerformanceAssertion_應包含時間上下文()
    {
        var heavyComputationService = new HeavyComputationService();
        var largeDataSet = Enumerable.Range(1, 10000)
            .Select(i => new DataRecord { Id = i, Value = $"Record_{i}" })
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        
        var result = heavyComputationService.ProcessLargeDataSet(largeDataSet);
        
        stopwatch.Stop();
        
        // 效能Assertions包含具體數據
        result.Should().NotBeNull("因為處理應該要成功完成");
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(5),
            $"因為處理 {largeDataSet.Count} 筆資料應在 5 秒內完成，但實際花費 {stopwatch.Elapsed.TotalSeconds:F2} 秒");
    }
}
```

---

## 今日重點回顧

### 總結

1. **進階 Assertions 技巧掌握**：
   - 複雜物件深度比較的技術細節
   - Object Graph 比較驗證方法
   - 非同步 Assertions 的最佳實務

2. **自訂 Assertions 設計能力**：
   - 領域特定 Assertions 的建立方法
   - 條件式 Assertions 的實用技巧
   - 效能最佳化的實戰技巧

3. **複雜情境處理策略**：
   - 大量資料的分批處理技巧
   - 錯誤訊息最佳化方法
   - 團隊標準化的實作方法

4. **動態欄位處理技巧**：
   - 動態屬性排除技巧
   - 屬性模式匹配的應用
   - 時間戳記處理的最佳實務

---

## 明日預告

明天我們將說明 **Code Coverage 程式碼涵蓋範圍實戰指南**，內容包括：

- **Code Coverage 基礎概念與重要性**：了解程式碼涵蓋範圍的實際用途
- **測試涵蓋率工具選擇與設定**：了解 .NET 環境下的涵蓋率分析工具
- **涵蓋率報告分析與改善策略**：學會解讀報告並制定改善計畫

---

## 參考資源

### 工具與文件

- **AwesomeAssertions GitHub**：<https://github.com/AwesomeAssertions/AwesomeAssertions>
- **AwesomeAssertions 官方文件**：<https://awesomeassertions.org/>
- **xUnit.net 官方文件**：<https://xunit.net/>

---

## 老派工程師的心得感想

今天介紹了 AwesomeAssertions 的進階應用。這類工具好不好，看的是它能不能把麻煩的事變簡單，不是它自己有多少花樣。

**身為一個老派工程師，我的實際感想**：

- 好的測試設計反映了對業務邏輯的深度理解
- 自訂 Assertions 讓重複的驗證邏輯變成可重用的工具
- AwesomeAssertions 作為社群分支版本，提供了穩定的 Apache 2.0 授權保障
- 團隊的測試文化比工具本身更重要

**測試 Assertions 需要平衡考量**：

- 詳細到足以發現問題，但不能過於繁瑣
- 靈活到能適應變化，但不能失去一致性
- 高效率到能快速執行，但不能犧牲正確性

AI 幫得上忙的是生出測試程式碼，決定不了什麼值得測、Assertions 策略怎麼訂。這些基礎能力還是得自己有。

明天我們來瞭解與認識什麼是 Code Coverage 以及要寫多少的測試才足夠。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day05>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第五天。明天會介紹 Day 06：Code Coverage 程式碼涵蓋範圍實戰指南。**
