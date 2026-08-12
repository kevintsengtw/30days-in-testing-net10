---
day: 16
title: "Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime"
sample: samples/day16
target_framework: net10.0
packages:
  - AutoFixture
  - AutoFixture.AutoNSubstitute
  - AutoFixture.Xunit3
  - AwesomeAssertions
  - Microsoft.Bcl.TimeProvider
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.TrxReport
  - NSubstitute
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime

## 前言

前面已經學過各種測試技術，從基礎單元測試到 AutoFixture、Bogus 等進階工具。今天要解決一個很實際的問題：**時間相依性的測試**。

看看這些常見的開發情境：

- 營業時間判斷：系統需要根據目前時間決定是否允許下單
- 優惠活動控制：特定日期或時段才生效的促銷邏輯
- 快取過期機制：依據時間決定資料是否有效
- 排程任務觸發：定時執行的背景作業

這些功能在實際運作時都會直接使用 `DateTime.Now` 或 `DateTime.Today`，但這就造成測試上的困難：**根本沒辦法控制「現在」是什麼時候**。

今天就來看看如何用 Microsoft.Bcl.TimeProvider 解決這個根本問題，讓時間相依的邏輯可以被完整測試。

## 時間測試的根本問題

### 傳統 DateTime 的測試困境

先來看看一個典型的時間相依程式碼：

```csharp
public class OrderService
{
    public bool CanPlaceOrder()
    {
        var now = DateTime.Now;
        var currentHour = now.Hour;

        // 營業時間：上午9點到下午5點
        return currentHour >= 9 && currentHour < 17;
    }

    public string GetTimeBasedDiscount()
    {
        var today = DateTime.Today;

        if (today.DayOfWeek == DayOfWeek.Friday)
        {
            return "週五快樂：九折優惠";
        }

        if (today.Month == 12 && today.Day == 25)
        {
            return "聖誕特惠：八折優惠";
        }

        return "無優惠";
    }
}
```

這段程式碼看起來很簡單，但要為它寫測試時，就會遇到幾個嚴重的問題：

### 問題一：測試的不可預測性

```csharp
[Fact]
public void CanPlaceOrder_在營業時間內_應回傳True()
{
    // Arrange
    var orderService = new OrderService();

    // Act
    var result = orderService.CanPlaceOrder();

    // Assert
    // 這個測試會根據執行時間而有不同結果！
    result.Should().BeTrue(); // 可能通過，也可能失敗
}
```

這個測試的問題就是結果完全取決於執行時間。下午 3 點執行會通過，晚上 8 點執行就失敗。

### 問題二：邊界條件的測試困難

要怎麼測試「剛好上午 9 點」或「剛好下午 5 點」的情況？除非能精確控制測試執行時間，否則這些重要的邊界條件就無法驗證。

### 問題三：並行測試的競爭條件

在 CI/CD 環境中，多個測試可能會並行執行。如果多個測試都相依現在時間，就可能出現不可預期的結果。

### 時間測試的複雜性

除了基本的日期時間問題，還會遇到更複雜的情況：

```csharp
public class AuditLogger
{
    public void LogActivity(string activity)
    {
        var timestamp = DateTime.UtcNow;
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp, TimeZoneInfo.Local);

        Console.WriteLine($"[{localTime:yyyy-MM-dd HH:mm:ss}] {activity}");
    }
}
```

這個程式碼涉及：

- UTC 時間與本地時間的轉換
- 時區處理
- 時間格式化

每一個環節都可能成為測試的障礙。

## Microsoft.Bcl.TimeProvider 登場

Microsoft.Bcl.TimeProvider 是微軟提供的時間抽象層，解決了傳統 DateTime 測試的根本問題。

> **補充說明**：`TimeProvider` 自 .NET 8 起已內建於 BCL（`System.TimeProvider`），`Microsoft.Bcl.TimeProvider` 套件是提供給 netstandard2.0 與 .NET Framework 等舊框架使用的 polyfill。本文範例專案（net10.0）保留此套件引用以對應主題；實務上 .NET 8 以上的專案不安裝此套件也能直接使用 `TimeProvider`。

### 為什麼需要 TimeProvider？

TimeProvider 的核心概念是**時間抽象化**。它將「取得現在時間」這個動作抽象為一個可以注入的服務，讓你能夠：

1. **控制時間**：在測試中精確控制程式看到的時間
2. **可重現性**：確保測試結果可重現
3. **並行安全**：每個測試都有獨立的時間環境

### TimeProvider 核心架構

```csharp
// TimeProvider 是抽象類別，定義了時間相關的核心功能（示意，非完整原始碼）
// 注意：所有成員都有預設實作，沒有任何 abstract 成員；
// 自訂 Provider 只需覆寫自己需要控制的成員即可
public abstract class TimeProvider
{
    // 取得 UTC 時間（virtual，預設回傳 DateTimeOffset.UtcNow）
    public virtual DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

    // 取得本地時間（非虛擬，由 GetUtcNow() 套用 LocalTimeZone 的偏移計算而得）
    public DateTimeOffset GetLocalNow();

    // 取得本地時區（virtual，預設回傳 TimeZoneInfo.Local）
    public virtual TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    // 高精度時間戳與頻率（virtual，預設使用 Stopwatch）
    public virtual long GetTimestamp() => Stopwatch.GetTimestamp();
    public virtual long TimestampFrequency => Stopwatch.Frequency;

    // 計算兩個時間戳之間的時間差（非虛擬）
    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);

    // 建立計時器（virtual，預設基於 System.Threading.Timer）
    public virtual ITimer CreateTimer(TimerCallback callback, object? state,
                                      TimeSpan dueTime, TimeSpan period);
}
```

這樣的設計正是 FakeTimeProvider 能運作的原因：它只覆寫 `GetUtcNow()`、`LocalTimeZone` 等需要控制的成員，其餘行為沿用基底類別。

### 系統預設實作

```csharp
// 系統預設實作透過靜態屬性 TimeProvider.System 取得，
// 其行為即各 virtual 成員的預設實作：
// 時間來自 DateTimeOffset.UtcNow、時區來自 TimeZoneInfo.Local、時間戳來自 Stopwatch
var utcNow = TimeProvider.System.GetUtcNow();
var localNow = TimeProvider.System.GetLocalNow();
```

## 重構時間相依程式碼

把剛才的 OrderService 重構為可測試的版本：

```csharp
public class OrderService
{
    private readonly TimeProvider _timeProvider;

    public OrderService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public bool CanPlaceOrder()
    {
        var now = _timeProvider.GetLocalNow();
        var currentHour = now.Hour;

        // 營業時間：上午9點到下午5點
        return currentHour >= 9 && currentHour < 17;
    }

    public string GetTimeBasedDiscount()
    {
        var today = _timeProvider.GetLocalNow().Date;

        if (today.DayOfWeek == DayOfWeek.Friday)
        {
            return "週五快樂：九折優惠";
        }

        if (today.Month == 12 && today.Day == 25)
        {
            return "聖誕特惠：八折優惠";
        }

        return "無優惠";
    }
}
```

### 在實際應用中註冊 TimeProvider

```csharp
// Program.cs 或 Startup.cs
services.AddSingleton(TimeProvider.System);
services.AddScoped<OrderService>();
```

## FakeTimeProvider 與測試實戰

Microsoft.Extensions.TimeProvider.Testing 套件提供了 FakeTimeProvider，這是專門為測試設計的時間提供者。用它可以在測試中完全控制時間的流逝，就像擁有時光機一樣。

### 基礎時間控制

先看看如何用 FakeTimeProvider 來控制測試中的時間：

```csharp
public class OrderServiceTests
{
    [Fact]
    public void CanPlaceOrder_在營業時間內_應回傳True()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 設定為下午 2 點
        var testTime = new DateTime(2024, 3, 15, 14, 0, 0, DateTimeKind.Local);
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(TimeZoneInfo.ConvertTimeToUtc(testTime));

        var orderService = new OrderService(fakeTimeProvider);

        // Act
        var result = orderService.CanPlaceOrder();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPlaceOrder_在營業時間外_應回傳False()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();

        // 設定為晚上 8 點
        var testTime = new DateTime(2024, 3, 15, 20, 0, 0, DateTimeKind.Local);
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(TimeZoneInfo.ConvertTimeToUtc(testTime));

        var orderService = new OrderService(fakeTimeProvider);

        // Act
        var result = orderService.CanPlaceOrder();

        // Assert
        result.Should().BeFalse();
    }
}
```

### 簡化 FakeTimeProvider 設定

由於設定本地時間的步驟有點繁瑣，可以建立一個擴充方法：

```csharp
public static class FakeTimeProviderExtensions
{
    /// <summary>
    /// 設定 FakeTimeProvider 的本地時間
    /// </summary>
    /// <param name="fakeTimeProvider">FakeTimeProvider 執行個體</param>
    /// <param name="localDateTime">要設定的本地時間</param>
    public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, DateTime localDateTime)
    {
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(utcTime);
    }
}
```

使用擴充方法後，測試程式碼變得更簡潔：

```csharp
[Fact]
public void CanPlaceOrder_在營業時間內_應回傳True()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0)); // 下午 2 點

    var orderService = new OrderService(fakeTimeProvider);

    // Act
    var result = orderService.CanPlaceOrder();

    // Assert
    result.Should().BeTrue();
}
```

## 進階時間控制技術

### 時間凍結

有些測試情境需要讓時間「凍結」在特定時點：

```csharp
[Fact]
public void ProcessBatch_在固定時間點_應正確處理()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    var fixedTime = new DateTime(2024, 12, 25, 10, 30, 0); // 聖誕節上午10:30
    fakeTimeProvider.SetLocalNow(fixedTime);

    var processor = new BatchProcessor(fakeTimeProvider);

    // Act & Assert
    var result1 = processor.ProcessItem("Item1");
    var result2 = processor.ProcessItem("Item2");

    // 兩次處理的時間戳應該完全相同
    result1.Timestamp.Should().Be(result2.Timestamp);
}
```

### 時間快轉

很多業務邏輯需要等待一段時間才能看到結果，比如快取過期、Token 失效等。在測試中不可能真的等幾個小時，這時就需要用時間快轉來模擬時間流逝：

```csharp
[Fact]
public void CacheExpiry_經過過期時間_應清除快取()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));

    var cache = new TimedCache(fakeTimeProvider, TimeSpan.FromMinutes(30));
    cache.Set("key", "value");

    // Act - 快轉時間到31分鐘後
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(31));

    // Assert
    var result = cache.Get("key");
    result.Should().BeNull();
}
```

### 設定歷史起始時間與資料重播

在金融系統或資料分析領域，經常需要重播歷史資料來驗證演算法的正確性。這時可以將 FakeTimeProvider 的起始時間設定為過去的某個時點：

```csharp
[Fact]
public void HistoricalDataProcessor_回到過去時間_應正確處理歷史資料()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();

    // 回到2020年的某一天
    var historicalTime = new DateTime(2020, 1, 15, 9, 0, 0);
    fakeTimeProvider.SetLocalNow(historicalTime);

    var processor = new HistoricalDataProcessor(fakeTimeProvider);

    // Act
    var result = processor.ProcessDataForDate(historicalTime.Date);

    // Assert
    result.Should().NotBeNull();
    result.ProcessedAt.Should().Be(historicalTime);
}
```

> **注意**：FakeTimeProvider 的預設起始時間是 2000-01-01 00:00:00 UTC，所以上例把時間設到 2020 年，實際上仍是「向前」調整而非倒轉。`SetUtcNow()` 不允許設定成早於 Provider 目前時間的值，否則會拋出 `ArgumentOutOfRangeException`。若確實需要把時間往回撥（真正的倒轉），請改用 `AdjustTime()`——它直接調整內部時鐘，且不會觸發任何已排定的 timer。

## 實戰應用情境

接下來看看幾個真實世界的應用情境，了解 TimeProvider 在不同業務邏輯中的實際用法。

### 情境一：排程系統的觸發邏輯測試

排程系統是時間相依性最明顯的情境之一。需要根據目前時間判斷工作是否應該執行，並計算下次執行時間：

```csharp
public class JobSchedule
{
    public DateTime NextExecutionTime { get; set; }
    public string CronExpression { get; set; } = string.Empty;
}

public class ScheduleService
{
    private readonly TimeProvider _timeProvider;

    public ScheduleService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool ShouldExecuteJob(JobSchedule schedule)
    {
        var now = _timeProvider.GetLocalNow();

        return schedule.NextExecutionTime <= now;
    }

    public DateTime CalculateNextExecution(JobSchedule schedule)
    {
        var now = _timeProvider.GetLocalNow();

        return schedule.CronExpression switch
        {
            "0 0 * * *" => now.Date.AddDays(1), // 每日午夜
            "0 0 * * 1" => GetNextMonday(now),   // 每週一午夜
            _ => now.DateTime.AddHours(1) // 預設每小時
        };
    }

    private DateTime GetNextMonday(DateTimeOffset now)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        return now.Date.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);
    }
}
```

測試排程系統：

```csharp
public class ScheduleServiceTests
{
    [Theory]
    [InlineData("2024-03-15 14:30:00", "2024-03-15 14:00:00", true)]  // 已到執行時間
    [InlineData("2024-03-15 13:30:00", "2024-03-15 14:00:00", false)] // 尚未到執行時間
    public void ShouldExecuteJob_根據時間判斷_應回傳正確結果(
        string currentTimeStr,
        string scheduledTimeStr,
        bool expected)
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var currentTime = DateTime.Parse(currentTimeStr);
        var scheduledTime = DateTime.Parse(scheduledTimeStr);

        fakeTimeProvider.SetLocalNow(currentTime);

        var schedule = new JobSchedule { NextExecutionTime = scheduledTime };
        var service = new ScheduleService(fakeTimeProvider);

        // Act
        var result = service.ShouldExecuteJob(schedule);

        // Assert
        result.Should().Be(expected);
    }
}
```

### 情境二：快取過期機制的驗證

快取系統通常會設定過期時間來確保資料的新鮮度。在測試這類功能時，不可能真的等幾分鐘來驗證快取是否過期，這時 TimeProvider 的時間控制能力就很有用：

```csharp
public record CacheItem<T>(T Value, DateTimeOffset ExpiryTime);

public class TimedCache<T>
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, CacheItem<T>> _cache = new();

    public TimedCache(TimeProvider timeProvider, TimeSpan defaultExpiry)
    {
        _timeProvider = timeProvider;
        DefaultExpiry = defaultExpiry;
    }

    public TimeSpan DefaultExpiry { get; }

    public void Set(string key, T value, TimeSpan? expiry = null)
    {
        var expiryTime = _timeProvider.GetUtcNow().Add(expiry ?? DefaultExpiry);
        _cache[key] = new CacheItem<T>(value, expiryTime);
    }

    public T? Get(string key)
    {
        if (!_cache.TryGetValue(key, out var item))
        {
            return default;
        }

        if (item.ExpiryTime <= _timeProvider.GetUtcNow())
        {
            _cache.Remove(key);
            return default;
        }

        return item.Value;
    }
}
```

測試快取過期：

#### FakeTimeProvider.Advance() 方法說明

`fakeTimeProvider.Advance()` 這個方法的用途是將 FakeTimeProvider 內部所維護的「現在時間」往前推進指定的時間間隔（例如 3 分鐘）。這通常用於單元測試中，模擬時間的流逝，讓你可以測試與時間相關的邏輯（如快取過期、營業時間判斷等），而不需要真的等待時間過去。

**基本概念**：

- 呼叫 `Advance(TimeSpan.FromMinutes(3))` 會讓 FakeTimeProvider 的「現在時間」增加 3 分鐘
- 這樣可以驗證快取項目是否如預期在過期時間後失效
- 此方法只會影響 FakeTimeProvider，不會影響系統的真實時間

**方法特性**：

- **非阻塞**：不會真正等待時間經過，而是直接更新內部時鐘
- **精確控制**：可以精確控制時間推進的幅度，從毫秒到小時都可以
- **測試效率**：避免在測試中真正等待時間經過，讓測試瞬間完成

**常見使用情境**：

- 測試快取過期邏輯
- 驗證定時任務觸發
- 模擬長時間執行的業務流程
- 測試時間區間相關的業務規則

**與真實等待的比較**：

```csharp
// X 真實測試 - 需要等待 5 分鐘
Thread.Sleep(TimeSpan.FromMinutes(5)); // 測試執行緩慢

// O 模擬測試 - 瞬間完成
fakeTimeProvider.Advance(TimeSpan.FromMinutes(5)); // 瞬間時間跳躍
```

```csharp
[Fact]
public void Cache_設定項目後快轉時間_應正確處理過期()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    var startTime = new DateTime(2024, 3, 15, 10, 0, 0);
    fakeTimeProvider.SetLocalNow(startTime);

    var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(5));

    // Act & Assert - 設定快取項目（時間點：10:00）
    cache.Set("key1", "value1");
    cache.Get("key1").Should().Be("value1");

    // 模擬時間前進 3 分鐘（時間點：10:03），快取尚未過期（5分鐘期限）
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(3));
    cache.Get("key1").Should().Be("value1"); // 3 < 5，仍在有效期內

    // 再次模擬時間前進 3 分鐘（時間點：10:06），快取已過期
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(3)); // 總計 6 分鐘 > 5 分鐘期限
    cache.Get("key1").Should().BeNull(); // 已過期，返回 null
}
```

#### 關鍵優勢

使用 `Advance()` 方法的最大價值在於**邊界測試**和**可重複性**：

- 可以精確測試時間邊界條件，如剛好過期的瞬間
- 每次測試都有相同的時間控制，避免時間相關的不穩定測試
- 可以輕鬆測試各種時間情境，包括極端情況

### 情境三：業務規則中的時間區間測試

金融交易系統有嚴格的交易時間限制，只有在特定時間區間內才能進行交易。這類業務規則通常涉及複雜的時間判斷邏輯：

```csharp
public class TradingService
{
    private readonly TimeProvider _timeProvider;

    public TradingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool IsInTradingHours()
    {
        var now = _timeProvider.GetLocalNow();
        var currentTime = now.TimeOfDay;

        // 交易時間：9:00-11:30, 13:00-15:00
        return (currentTime >= TimeSpan.FromHours(9) && currentTime <= TimeSpan.FromHours(11.5)) ||
               (currentTime >= TimeSpan.FromHours(13) && currentTime <= TimeSpan.FromHours(15));
    }

    public decimal GetMarketMultiplier()
    {
        var now = _timeProvider.GetLocalNow();

        return now.DayOfWeek switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => 0m, // 週末不交易
            DayOfWeek.Friday when now.Hour >= 14 => 1.1m, // 週五下午波動較大
            _ => 1.0m
        };
    }
}
```

測試交易時間區間：

```csharp
[Theory]
[InlineData("09:30:00", true)]  // 上午交易時間
[InlineData("11:15:00", true)]  // 上午交易時間結束前
[InlineData("12:00:00", false)] // 中午休息時間
[InlineData("14:30:00", true)]  // 下午交易時間
[InlineData("15:30:00", false)] // 下午交易結束後
public void IsInTradingHours_不同時間點_應回傳正確結果(string timeStr, bool expected)
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    var testTime = DateTime.Today.Add(TimeSpan.Parse(timeStr));
    fakeTimeProvider.SetLocalNow(testTime);

    var service = new TradingService(fakeTimeProvider);

    // Act
    var result = service.IsInTradingHours();

    // Assert
    result.Should().Be(expected);
}
```

## AutoFixture 與 TimeProvider 整合

前面已處理各種時間控制情境，接著把 AutoFixture 與 TimeProvider 接在一起，減少重複的測試設定。

### 範例專案對比學習

動手之前，先比較範例專案裡的兩個測試類別：

- **`OrderServiceTests.cs`**：傳統測試寫法，手動建立所有測試物件
- **`OrderServiceAutoFixtureTests.cs`**：AutoFixture 測試寫法，自動化物件建立

把相同測試案例套在這兩個類別上，就能直接比較兩種寫法。請留意：

1. **物件建立的差異**：傳統寫法需要手動 `new FakeTimeProvider()` 和 `new OrderService()`
2. **參數注入的方式**：AutoFixture 如何用 `[Frozen]` 和相依性注入自動處理
3. **程式碼簡潔度**：AutoFixture 版本如何減少重複的 Arrange 程式碼

### 為什麼要使用 AutoFixture

前面的範例反覆出現幾項準備工作：

- 建立 FakeTimeProvider 執行個體
- 設定特定的時間
- 建立被測試的服務執行個體

AutoFixture 可以接手這些重複工作，讓測試保留真正重要的行為與斷言。

### 建立 FakeTimeProviderCustomization

首先建立一個自訂的 AutoFixture Customization：

```csharp
public class FakeTimeProviderCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Register(() => new FakeTimeProvider());
    }
}
```

### AutoDataWithCustomization 的完整設定

建立一個整合了 NSubstitute 和 FakeTimeProvider 的自訂屬性：

```csharp
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    internal static IFixture CreateFixture()
    {
        var fixture = new Fixture()
            .Customize(new AutoNSubstituteCustomization())
            .Customize(new FakeTimeProviderCustomization());

        return fixture;
    }
}
```

### 傳統寫法 vs AutoFixture 寫法的對比

直接比較使用 AutoFixture 前後的兩種寫法：

#### 傳統寫法

```csharp
[Fact]
public void CanPlaceOrder_在營業時間內_傳統寫法()
{
    // Arrange - 需要手動建立所有物件
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0));

    var orderService = new OrderService(fakeTimeProvider);

    // Act
    var result = orderService.CanPlaceOrder();

    // Assert
    result.Should().BeTrue();
}
```

#### AutoFixture 寫法

```csharp
[Theory]
[AutoDataWithCustomization]
public void GetTimeBasedDiscount_週一_應回傳無優惠(
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
    OrderService sut) // sut = System Under Test，由 AutoFixture 自動建立
{
    // Arrange - 只需要設定測試相關的時間
    var mondayTime = new DateTime(2024, 3, 11, 14, 0, 0); // 2024/3/11 是週一
    fakeTimeProvider.SetLocalNow(mondayTime);

    // Act
    var discount = sut.GetTimeBasedDiscount();

    // Assert
    discount.Should().Be("無優惠");
}
```

### AutoFixture 整合的技術挑戰

#### Matching.DirectBaseType 的重要性

如果直接使用 `[Frozen] FakeTimeProvider`，會遇到類型不匹配問題：

```csharp
// X 這樣寫會失敗
[Theory]
[AutoDataWithCustomization]
public void Test([Frozen] FakeTimeProvider provider, OrderService sut)
{
    // OrderService 建構式需要 TimeProvider，但 AutoFixture 只知道 FakeTimeProvider
    // 這會導致類型不匹配錯誤
}
```

正確的解決方案是使用 `Matching.DirectBaseType`：

```csharp
// O 正確的寫法
[Theory]
[AutoDataWithCustomization]
public void Test([Frozen(Matching.DirectBaseType)] FakeTimeProvider provider, OrderService sut)
{
    // AutoFixture 會將 FakeTimeProvider 也註冊為 TimeProvider 使用
}
```

#### Matching.DirectBaseType 詳細說明

**核心概念**：

- `Matching.DirectBaseType` 告訴 AutoFixture：「當需要基底類型（TimeProvider）時，使用這個衍生類型的執行個體（FakeTimeProvider）」
- 這解決了相依性注入中常見的抽象類型與具體實作類型不匹配的問題

**實際運作流程**：

1. AutoFixture 看到需要建立 OrderService
2. 發現 OrderService 的建構式需要 TimeProvider 參數
3. 檢查是否有被 `[Frozen]` 標記的執行個體可以滿足這個需求
4. 找到 `[Frozen(Matching.DirectBaseType)] FakeTimeProvider`
5. 確認 TimeProvider 是 FakeTimeProvider 的直接基底類型
6. 將 FakeTimeProvider 執行個體注入到 OrderService 的建構式中

### AutoFixture 的實際應用範例

為了更清楚展示 AutoFixture 的優勢，我們建立了專門的對比測試類別：

**傳統測試**：`OrderServiceTests.cs` - 展示手動建立物件的傳統寫法
**AutoFixture 測試**：`OrderServiceAutoFixtureTests.cs` - 展示自動化物件建立的現代寫法

以下是幾個實際的測試範例：

```csharp
[Theory]
[AutoDataWithCustomization]
public void GetTimeBasedDiscount_週五_應回傳九折優惠(
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
    OrderService sut)
{
    // Arrange
    var fridayTime = new DateTime(2024, 3, 15, 14, 0, 0); // 2024/3/15 是週五
    fakeTimeProvider.SetLocalNow(fridayTime);

    // Act
    var discount = sut.GetTimeBasedDiscount();

    // Assert
    discount.Should().Be("週五快樂：九折優惠");
}

[Theory]
[AutoDataWithCustomization]
public void CanPlaceOrder_營業時間邊界測試_使用不同執行個體(
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider1,
    OrderService sut1)
{
    // Arrange & Act & Assert - 上午9點整（營業時間開始）
    fakeTimeProvider1.SetLocalNow(new DateTime(2024, 3, 15, 9, 0, 0));
    sut1.CanPlaceOrder().Should().BeTrue();
}

[Theory]
[AutoDataWithCustomization]
public void TimedCache_使用AutoFixture測試過期機制_應正確處理(
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
    string key,    // AutoFixture 自動產生
    string value)  // AutoFixture 自動產生
{
    // Arrange
    var startTime = new DateTime(2024, 3, 15, 10, 0, 0);
    fakeTimeProvider.SetLocalNow(startTime);

    var cache = new TimedCache<string>(fakeTimeProvider, TimeSpan.FromMinutes(30));

    // Act & Assert - 設定和立即取得
    cache.Set(key, value);
    cache.Get(key).Should().Be(value);

    // Act & Assert - 快轉時間後應過期
    fakeTimeProvider.Advance(TimeSpan.FromMinutes(31));
    cache.Get(key).Should().BeNull();
}
```

### AutoFixture 的優勢總結

結合 AutoFixture 與 TimeProvider 的主要優勢：

1. **減少樣板程式碼**：不需要手動建立測試物件和相依
2. **提高測試涵蓋率**：可以輕鬆產生多種測試案例和測試資料
3. **保持測試獨立性**：每個測試都有獨立的時間環境和測試資料
4. **增強可讀性**：測試重點更聚焦在業務邏輯驗證，而非物件建立
5. **提升維護性**：當建構式參數變更時，AutoFixture 會自動適應

### 何時使用 AutoFixture

**建議使用的情況**：

- 測試類別有多個相似的測試方法
- 被測試的類別有複雜的建構式參數
- 需要大量不同的測試資料組合
- 希望減少測試程式碼的重複性

**可以考慮傳統寫法的情況**：

- 測試案例很簡單，只有少數幾個
- 需要對物件建立過程有完全的控制
- 團隊對 AutoFixture 不熟悉，學習成本考量

### 延伸思考：InlineAutoDataAttribute 的應用

仔細觀察 `OrderServiceTests.cs` 中的 `CanPlaceOrder_不同時間點_應回傳正確結果` 測試方法，它使用了 `[Theory]` 和 `[InlineData]` 來測試多個時間點的情況：

```csharp
[Theory]
[InlineData(8, false)]  // 上午8點 - 營業時間前
[InlineData(9, true)]   // 上午9點 - 剛開始營業
[InlineData(12, true)]  // 中午12點 - 營業時間內
[InlineData(16, true)]  // 下午4點 - 營業時間內
[InlineData(17, false)] // 下午5點 - 剛結束營業
[InlineData(18, false)] // 下午6點 - 營業時間後
public void CanPlaceOrder_不同時間點_應回傳正確結果(int hour, bool expected)
```

這個測試案例結合了參數化測試的優勢，但仍需要手動建立 `FakeTimeProvider` 和 `OrderService`。

**思考題**：還記得 `Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用` 學過的 `InlineAutoDataAttribute` 嗎？我們是否可以將這個測試改寫成：

```csharp
[Theory]
[InlineAutoDataWithCustomization(8, false)]
[InlineAutoDataWithCustomization(9, true)]
// ... 其他測試案例
public void CanPlaceOrder_不同時間點_AutoFixture版本(
    int hour,
    bool expected,
    [Frozen(Matching.DirectBaseType)] FakeTimeProvider fakeTimeProvider,
    OrderService sut)
```

要實現這個功能，你需要：

1. **建立 InlineAutoDataWithCustomizationAttribute**：繼承 `InlineAutoDataAttribute`
2. **整合 FakeTimeProviderCustomization**：確保 AutoFixture 知道如何建立 FakeTimeProvider
3. **處理參數順序**：InlineData 的參數要放在前面，AutoFixture 產生的參數放在後面

> **xunit v3 實作提示**：在 AutoFixture.Xunit3 中，`InlineAutoDataAttribute` 的建構式簽章已改為 `(Func<IFixture> fixtureFactory, params object[] values)`，不再提供接受 `AutoDataAttribute` 執行個體的多載。實作時請將 `CreateFixture` 宣告為 `internal static`，並以 `base(AutoDataWithCustomizationAttribute.CreateFixture, values)` 傳入 fixture factory。特別注意：若沿用 v2 時代的 `base(new AutoDataWithCustomizationAttribute(), values)` 寫法，attribute 執行個體會被 `params object[]` 多載吞掉——**編譯可以通過，執行期才會發現 attribute 執行個體被當成第一個測試參數而失敗**。完整的實作可以參考範例專案的 `AutoFixtureCustomizations.cs`。

這樣的整合可以結合參數化測試的靈活性和 AutoFixture 的自動化優勢，進一步減少測試程式碼的重複性。

有興趣的朋友可以嘗試實作看看，這是一個很好的練習，可以加深對 AutoFixture 客製化的理解。

## 最佳實務與注意事項

### 相依性注入整合

在實際專案中設定 TimeProvider 的相依性注入時，需要區分不同環境的需求：

```csharp
// 正式環境
services.AddSingleton(TimeProvider.System);

// 開發環境（如果需要特定時間測試）
if (isDevelopment)
{
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 25, 10, 0, 0)); // 測試用時間
    services.AddSingleton<TimeProvider>(fakeTimeProvider);
}
```

### 執行緒安全考量

#### 基本執行緒安全特性

`FakeTimeProvider` 本身是執行緒安全的，這表示多個執行緒可以同時安全地呼叫其方法：

- **讀取操作安全**：`GetUtcNow()`、`GetLocalNow()` 等方法可以被多個執行緒同時呼叫
- **寫入操作安全**：`SetUtcNow()`、`Advance()` 等方法內部有適當的同步機制
- **記憶體可見性**：時間變更會立即對所有執行緒可見

#### 測試多執行緒情境的注意事項

在測試多執行緒程式碼時，需要注意以下幾點：

##### 1. 時間一致性測試

```csharp
[Fact]
public async Task ConcurrentOperations_使用相同TimeProvider_應保持一致性()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 10, 0, 0));

    var service = new TimeService(fakeTimeProvider);

    // Act - 並行執行多個操作
    var tasks = Enumerable.Range(0, 100)
        .Select(_ => Task.Run(() => service.GetCurrentTimeString()))
        .ToArray();

    var results = await Task.WhenAll(tasks);

    // Assert - 所有結果應該相同（因為時間被凍結）
    results.Should().AllBe(results[0]);
}
```

##### 2. 時間變更的競爭條件測試

```csharp
[Fact]
public async Task TimeAdvancement_在並行讀取期間_應保持原子性()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    var startTime = new DateTime(2024, 3, 15, 10, 0, 0);
    fakeTimeProvider.SetLocalNow(startTime);

    var service = new TimeService(fakeTimeProvider);
    var readTasks = new List<Task<DateTime>>();
    var advanceTasks = new List<Task>();

    // Act - 同時進行讀取和時間推進（推進任務也要收集起來等待，否則測試可能在推進完成前就結束）
    for (int i = 0; i < 50; i++)
    {
        readTasks.Add(Task.Run(() => service.GetCurrentTime()));

        // 每隔幾次讀取就推進時間（共 5 次、每次 1 分鐘）
        if (i % 10 == 0)
        {
            advanceTasks.Add(Task.Run(() => fakeTimeProvider.Advance(TimeSpan.FromMinutes(1))));
        }
    }

    var results = await Task.WhenAll(readTasks);
    await Task.WhenAll(advanceTasks);

    // Assert - 每個讀取值都必須落在起始時間與最終時間（+5 分鐘）之間，
    // 不會出現「推進到一半」的撕裂值
    results.Should().AllSatisfy(time =>
        time.Should().BeOnOrAfter(startTime).And.BeOnOrBefore(startTime.AddMinutes(5)));
}
```

#### 常見陷阱與解決方案

##### 陷阱 1：假設時間在測試期間不會變化

```csharp
// X 錯誤：假設時間在整個測試過程中固定
[Fact]
public async Task BadExample_假設時間不變()
{
    var fakeTimeProvider = new FakeTimeProvider();
    var service = new TimeService(fakeTimeProvider);

    var time1 = service.GetCurrentTime();

    // 如果有其他執行緒呼叫 Advance()，這個假設就會失敗
    await Task.Delay(100);

    var time2 = service.GetCurrentTime();
    time1.Should().Be(time2); // 可能失敗！
}

// O 正確：明確控制時間
[Fact]
public async Task GoodExample_明確控制時間()
{
    var fakeTimeProvider = new FakeTimeProvider();
    var fixedTime = new DateTime(2024, 3, 15, 10, 0, 0);
    fakeTimeProvider.SetLocalNow(fixedTime);

    var service = new TimeService(fakeTimeProvider);

    var time1 = service.GetCurrentTime();
    // 明確說明：在沒有 Advance() 的情況下，時間應該相同
    var time2 = service.GetCurrentTime();

    time1.Should().Be(time2);
}
```

##### 陷阱 2：在並行測試中共用 FakeTimeProvider 執行個體

```csharp
// X 錯誤：多個測試共用同一個執行個體
public class BadTestClass
{
    private static readonly FakeTimeProvider SharedProvider = new();

    [Fact] public void Test1() { /* 可能互相干擾 */ }
    [Fact] public void Test2() { /* 可能互相干擾 */ }
}

// O 正確：每個測試使用獨立執行個體
public class GoodTestClass
{
    [Fact]
    public void Test1()
    {
        var fakeTimeProvider = new FakeTimeProvider(); // 獨立執行個體
        // 測試邏輯
    }

    [Fact]
    public void Test2()
    {
        var fakeTimeProvider = new FakeTimeProvider(); // 獨立執行個體
        // 測試邏輯
    }
}
```

### 測試隔離策略

在測試類別中使用 FakeTimeProvider 時，要確保每個測試都有獨立的時間環境，避免測試之間互相影響：

```csharp
public class TimeServiceTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly TimeService _sut;

    public TimeServiceTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _sut = new TimeService(_fakeTimeProvider);
    }

    [Fact]
    public void Test1()
    {
        _fakeTimeProvider.SetLocalNow(new DateTime(2024, 1, 1));
        // 測試邏輯...
    }

    [Fact]
    public void Test2()
    {
        _fakeTimeProvider.SetLocalNow(new DateTime(2024, 12, 31));
        // 測試邏輯...
    }
}
```

### 時區處理的最佳實務

開發全球化應用時，時區處理是個複雜的議題。TimeProvider 可以方便地測試不同時區的邏輯：

```csharp
public class GlobalTimeService
{
    private readonly TimeProvider _timeProvider;

    public GlobalTimeService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset GetTimeInTimeZone(string timeZoneId)
    {
        var utcNow = _timeProvider.GetUtcNow();
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTime(utcNow, targetTimeZone);
    }
}
```

測試不同時區：

```csharp
[Theory]
[InlineData("UTC", "2024-03-15 10:00:00")]
[InlineData("Tokyo Standard Time", "2024-03-15 19:00:00")]
[InlineData("Eastern Standard Time", "2024-03-15 06:00:00")]
public void GetTimeInTimeZone_不同時區_應回傳正確時間(string timeZoneId, string expectedTimeStr)
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    var baseUtcTime = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);
    fakeTimeProvider.SetUtcNow(baseUtcTime);

    var service = new GlobalTimeService(fakeTimeProvider);
    var expectedTime = DateTime.Parse(expectedTimeStr);

    // Act
    var result = service.GetTimeInTimeZone(timeZoneId);

    // Assert
    result.DateTime.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
}
```

## 效能考量與限制

### FakeTimeProvider 的效能特性

FakeTimeProvider 的時間查詢（`GetUtcNow()` 等）只是讀取內部欄位加上簡單的鎖同步，對單元測試的規模而言綽綽有餘，一般不需要為它撰寫效能測試。

若真的需要量測高頻查詢的耗時，請使用 BenchmarkDotNet 這類基準測試工具，而不是在單元測試裡用 `Stopwatch` 搭配固定毫秒門檻做斷言——那種寫法會受 CI 主機規格、Debug/Release 組態、JIT 暖機與當下負載影響，是不穩定測試（flaky test）的典型來源。

### 記憶體使用注意事項

FakeTimeProvider 沒有非受控資源，也未實作 `IDisposable`，不需要（也無法）呼叫 `Dispose()` 釋放，測試結束後交給 GC 回收即可。

同樣地，不要在單元測試裡以 `GC.Collect()` 搭配 `GC.GetTotalMemory()` 斷言「記憶體一定下降」——GC 的執行時機與區域變數的存活期都沒有這種保證，這類測試與效能門檻測試一樣，註定不穩定。

## 今日小結

Microsoft.Bcl.TimeProvider 將系統時間改成可替換的相依性，測試不必再依賴真正的時鐘。

### 核心概念

- **時間抽象化**：將時間取得邏輯抽象為可注入的服務
- **測試可控性**：用 FakeTimeProvider 控制測試中的時間
- **並行安全**：每個測試都有獨立的時間環境

### 實戰技能

- **基礎重構**：將 DateTime 相依程式碼改為使用 TimeProvider
- **測試設計**：使用 FakeTimeProvider 進行時間控制測試
- **AutoFixture 整合**：結合自動化測試資料產生與時間控制

### 進階應用

- **時間控制技術**：凍結、快轉、倒轉等進階時間操作
- **實戰情境**：排程系統、快取過期、交易時間區間等實際應用
- **最佳實務**：相依性注入、執行緒安全、測試隔離等重要考量

### 關鍵收穫

1. **可預測性**：時間相依的程式碼不再受執行環境影響
2. **可重現性**：測試結果完全可重現，消除隨機失敗
3. **完整性**：能夠測試所有時間相關的邊界條件和例外情況

Microsoft.Bcl.TimeProvider 讓測試能固定時間、向前推進並觸發計時器。測試結果因此不再取決於執行當下的時刻，也不必真的等待目標時間到來。

## 延伸閱讀

- [TimeProvider 官方文件](https://learn.microsoft.com/zh-tw/dotnet/api/system.timeprovider)
- [Microsoft.Bcl.TimeProvider NuGet 套件](https://www.nuget.org/packages/Microsoft.Bcl.TimeProvider/)
- [Microsoft.Extensions.TimeProvider.Testing NuGet 套件](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing/)
- [使用 Microsoft.Bcl.TimeProvider 取代 DateTime](https://www.dotblogs.com.tw/mrkt/2024/09/28/222532)
- [當 AutoFixture 的 AutoData 與 Microsoft.Bcl.TimeProvider 碰在一起時會如何？](https://www.dotblogs.com.tw/mrkt/2024/10/02/155445)

明天會處理檔案與 IO 測試的問題，看看如何用 System.IO.Abstractions 讓檔案系統操作變得可測試。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day16>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十六天。明天會介紹 Day 17 - 檔案與 IO 測試：使用 System.IO.Abstractions 模擬檔案系統 - 打造可測試的檔案操作。**
