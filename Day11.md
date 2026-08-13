---
day: 11
title: "Day 11 - AutoFixture 進階：自訂化測試資料產生策略"
sample: samples/day11
target_framework: net10.0
packages:
  - AutoFixture
  - AwesomeAssertions
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Microsoft.Testing.Extensions.TrxReport
---

# Day 11 - AutoFixture 進階：自訂化測試資料產生策略

<!-- toc -->

- [本日重點](#本日重點)
- [System.ComponentModel.DataAnnotations 整合應用](#systemcomponentmodeldataannotations-整合應用)
- [限定屬性值的範圍](#限定屬性值的範圍)
- [補充：AutoFixture 的 .With(...) 行為](#補充autofixture-的-with-行為)
- [限定 DateTime 屬性值的範圍](#限定-datetime-屬性值的範圍)
- [實作可以指定屬性的 RandomRangedDateTimeBuilder 類別](#實作可以指定屬性的-randomrangeddatetimebuilder-類別)
- [實作可以指定屬性的 RandomRangedNumericSequenceBuilder](#實作可以指定屬性的-randomrangednumericsequencebuilder)
- [將 RandomRangedNumericSequenceBuilder 修改為支援所有數值型別](#將-randomrangednumericsequencebuilder-修改為支援所有數值型別)
- [今日重點回顧](#今日重點回顧)
- [本日小結](#本日小結)
- [使用的套件版本](#使用的套件版本)

<!-- /toc -->

前面的章節介紹了 AutoFixture 的基本用法。這一篇改為依業務規則自訂測試資料產生邏輯，讓隨機資料仍落在有效範圍內。

## 本日重點

- System.ComponentModel.DataAnnotations 整合應用
- 限定屬性值的範圍控制技術
- 限定 DateTime 屬性值的範圍
- 實作自訂 RandomRangedDateTimeBuilder 類別
- 實作自訂 RandomRangedNumericSequenceBuilder
- 支援所有數值型別的泛型化設計

## System.ComponentModel.DataAnnotations 整合應用

AutoFixture 能讀取 DataAnnotation Attribute，並依現有的模型驗證規則產生符合限制的測試資料。

### Person 類別範例

先建立一個包含兩種 DataAnnotation 屬性的 Person 類別：

```csharp
using System.ComponentModel.DataAnnotations;

public class Person
{
    public Guid Id { get; set; }
    
    [StringLength(10)]
    public string Name { get; set; } = string.Empty;
    
    [Range(10, 80)]
    public int Age { get; set; }
    
    public DateTime CreateTime { get; set; }
}
```

### 驗證 AutoFixture 對 DataAnnotations 的自動識別

接著驗證 AutoFixture 能否識別這些屬性，並產生符合限制的資料：

```csharp
[Fact]
public void AutoFixture_應能識別DataAnnotations並產生符合限制的資料()
{
    // Arrange
    var fixture = new Fixture();

    // Act
    var person = fixture.Create<Person>();

    // Assert
    person.Id.Should().NotBe(Guid.Empty);
    person.Name.Should().NotBeNull();
    person.Name.Length.Should().Be(10);    // StringLength(10) 的限制
    person.Age.Should().BeInRange(10, 80); // Range(10, 80) 的限制
    person.CreateTime.Should().BeAfter(DateTime.MinValue);
}
```

### 實務測試：驗證多個物件都符合限制

我們也可以測試批量產生的物件是否都符合 DataAnnotation 的限制：

```csharp
[Fact]
public void AutoFixture_批量產生的Person物件_都應符合DataAnnotations限制()
{
    // Arrange
    var fixture = new Fixture();

    // Act
    var persons = fixture.CreateMany<Person>(10).ToList();

    // Assert
    persons.Should().HaveCount(10);
    persons.Should().AllSatisfy(person =>
    {
        person.Name.Length.Should().Be(10);
        person.Age.Should().BeInRange(10, 80);
    });
}
```

## 限定屬性值的範圍

除了使用 DataAnnotations，也可以用 `.With()` 方法搭配 `Random.Shared.Next()`，動態產生指定範圍的隨機數值。

### Member 類別範例

在 Age 屬性上使用 Range 特性，並限制範圍為 10 \~ 80 之間

```csharp
public class Member
{
    public string Name { get; set; } = string.Empty;

    [Range(10, 80)]
    public int Age { get; set; }
    
    public string Email { get; set; } = string.Empty;
}
```

### 使用 .With() 控制屬性範圍

```csharp
[Fact]
public void 使用With方法_控制Member年齡範圍()
{
    // Arrange
    var fixture = new Fixture();

    // Act
    var member = fixture.Build<Member>()
                        .With(x => x.Age, () => Random.Shared.Next(30, 50))
                        .Create();

    // Assert
    member.Age.Should().BeInRange(30, 50);
    member.Name.Should().NotBeNull();
    member.Email.Should().NotBeNull();
}
```

### CreateMany 產生多個物件時的範圍控制

當我們需要產生多個物件時，每個物件的屬性值都會重新計算：

```csharp
[Fact]
public void CreateMany產生多個Member_每個Age都在指定範圍內()
{
    // Arrange
    var fixture = new Fixture();

    // Act
    var members = fixture.Build<Member>()
                         .With(x => x.Age, () => Random.Shared.Next(30, 50))
                         .CreateMany(10)
                         .ToList();

    // Assert
    members.Should().HaveCount(10);
    members.Should().AllSatisfy(member => { member.Age.Should().BeInRange(30, 50); });

    // 驗證年齡確實是隨機的（不是所有物件都有相同年齡）
    var distinctAges = members.Select(m => m.Age).Distinct().Count();
    distinctAges.Should().BeGreaterThan(1);
}
```

## 補充：AutoFixture 的 .With(...) 行為

在使用 `.With()` 方法時，固定值和動態值有重要的差異：

- `.With(x => x.Prop, value)`：固定值，所有物件都用這個值
- `.With(x => x.Prop, () => value)`：動態值，每個物件都會執行一次 lambda

### 固定值 vs 動態值的差異

直接用範例比較兩者的差異：

```csharp
[Fact]
public void With方法_固定值vs動態值的差異()
{
    // Arrange
    var fixture = new Fixture();

    // Act - 使用固定值（不用 lambda，只會執行一次）
    var membersWithFixedAge = fixture.Build<Member>()
                                     .With(x => x.Age, Random.Shared.Next(30, 50)) // 固定值，只執行一次隨機產生，所有物件都是同樣的值
                                     .CreateMany(5)
                                     .ToList();

    // Act - 使用動態值（用 lambda，每個物件都會執行一次）
    var membersWithDynamicAge = fixture.Build<Member>()
                                       .With(x => x.Age, () => Random.Shared.Next(30, 50)) // 動態值，每個物件都重新計算
                                       .CreateMany(5)
                                       .ToList();

    // Assert
    // 固定值：所有物件的年齡都相同（雖然是隨機產生的，但只產生一次）
    var firstAge = membersWithFixedAge.First().Age;
    firstAge.Should().BeInRange(30, 49);
    membersWithFixedAge.Should().AllSatisfy(member => member.Age.Should().Be(firstAge));
    var distinctFixedAges = membersWithFixedAge.Select(m => m.Age).Distinct().Count();
    distinctFixedAges.Should().Be(1); // 只有一個不同的值

    // 動態值：每個物件的年齡都可能不同
    membersWithDynamicAge.Should().AllSatisfy(member => member.Age.Should().BeInRange(30, 49));
    var distinctDynamicAges = membersWithDynamicAge.Select(m => m.Age).Distinct().Count();
    distinctDynamicAges.Should().BeGreaterThan(1); // 通常會有多個不同的值
}
```

### 使用 Random.Shared 的優點

在 .NET 6 之後，建議使用 `Random.Shared` 而不是 `new Random()`：

1. **效能更好**：不需要每次都建立新的 Random 執行個體，減少配置成本。

2. **執行緒安全**：在多執行緒環境中使用 `Random.Shared` 是安全的；共用同一個 `new Random()` 執行個體則可能導致 race condition 或錯誤的亂數行為。

3. **歷史包袱說明**：.NET Framework 時代的 `new Random()` 用系統時間當種子，短時間內建立多個執行個體會拿到相同種子、產生重複值。.NET 6 之後無參數建構式已改用非決定性種子，這個問題不復存在——但每次 `new` 仍有成本，共用 `Random.Shared` 依然是比較好的選擇。

### Random.Shared vs new Random() 比較表

| 特性           | `new Random()`                       | `Random.Shared`            |
| :------------- | :----------------------------------- | :------------------------- |
| **執行個體化方式** | 每次呼叫都會建立新的 `Random` 執行個體   | 使用全域共用的單一執行個體     |
| **執行緒安全** | 不是執行緒安全                       | 是執行緒安全               |
| **效能**       | 多次建立會有配置成本                 | 效能更佳                   |
| **用途建議**   | 適合單執行緒、短期用途               | 適合多執行緒、全域共用用途 |

### 實務範例：避免重複值問題

```csharp
[Fact]
public void 展示Random共用執行個體的優勢()
{
    // Arrange
    var fixture = new Fixture();

    // Act - 使用 Random.Shared（推薦）
    var members1 = fixture.Build<Member>()
                          .With(x => x.Age, () => Random.Shared.Next(20, 60))
                          .CreateMany(20)
                          .ToList();

    // Assert
    members1.Should().AllSatisfy(member => member.Age.Should().BeInRange(20, 60));

    // 驗證產生的年齡有足夠的隨機性
    var distinctAges = members1.Select(m => m.Age).Distinct().Count();
    distinctAges.Should().BeGreaterThan(5); // 在 20 個物件中，應該有超過 5 個不同的年齡值
}
```

### 為什麼不用測試比較兩者的隨機性？

早期的教學常會寫一個測試，比較 `new Random()` 與 `Random.Shared` 產生重複值的數量。在 .NET 6 之後這種測試已經失去意義：兩者的隨機品質相當，重複數誰多誰少全看運氣，斷言「`Random.Shared` 的重複值比較少」是不穩定測試（flaky test）——這也是範例專案沒有收錄這種比較測試的原因。`Random.Shared` 的價值在**執行緒安全與省下重複配置**，不在隨機性更好。

## 限定 DateTime 屬性值的範圍

對於 DateTime 屬性，AutoFixture 提供了 `RandomDateTimeSequenceGenerator` 來控制日期範圍。

`RandomDateTimeSequenceGenerator` 是 AutoFixture 中的一個內建類別，實作了 ISpecimenBuilder 介面，專門用來產生一系列隨機但遞增的 DateTime 值。

功能簡介：

- 用途：當你需要為多個物件產生不同的 DateTime 值時，這個類別會自動產生一個從某個起始時間開始、每次略微遞增的 DateTime 值。
- 特性：
  - 每次呼叫都會產生一個新的 DateTime，比前一次稍晚。
  - 預設從 DateTime.Now 開始。
  - 遞增的間隔是隨機的 (通常是幾秒到幾分鐘之間)。

### RandomDateTimeSequenceGenerator 的基本應用

先在 Member 類別加入日期屬性：

```csharp
public class Member
{
    public string Name { get; set; } = string.Empty;

    [Range(10, 80)]
    public int Age { get; set; }

    public string Email { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
```

### 使用 RandomDateTimeSequenceGenerator 控制日期範圍

```csharp
[Fact]
public void 使用RandomDateTimeSequenceGenerator_控制DateTime範圍()
{
    // Arrange
    var fixture = new Fixture();
    var minDate = new DateTime(2025, 1, 1);
    var maxDate = new DateTime(2025, 12, 31);

    fixture.Customizations.Add(new RandomDateTimeSequenceGenerator(minDate, maxDate));

    // Act
    var member = fixture.Create<Member>();

    // Assert
    member.CreateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);
    member.UpdateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);
}
```

### RandomDateTimeSequenceGenerator 的限制

`RandomDateTimeSequenceGenerator` 會影響所有的 DateTime 屬性，這可能不是我們想要的結果。有時候我們希望只控制特定的屬性。

```csharp
[Fact]
public void RandomDateTimeSequenceGenerator_會影響所有DateTime屬性()
{
    // Arrange
    var fixture = new Fixture();
    var minDate = new DateTime(2025, 1, 1);
    var maxDate = new DateTime(2025, 12, 31);

    fixture.Customizations.Add(new RandomDateTimeSequenceGenerator(minDate, maxDate));

    // Act
    var members = fixture.CreateMany<Member>(5).ToList();

    // Assert
    members.Should().AllSatisfy(member =>
    {
        // 所有的 DateTime 屬性都會被限制在同樣的範圍內
        member.CreateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);
        member.UpdateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);
    });
}
```

## 實作可以指定屬性的 RandomRangedDateTimeBuilder 類別

**使用 RandomDateTimeSequenceGenerator 要注意的事情**

使用 RandomDateTimeSequenceGenerator 後，同一個類別的其他 DateTime 屬性也會受影響。只要屬性由 AutoFixture 自動產生，且未被其他自訂邏輯（例如 `.With(...)`）覆寫，就會套用這項設定。

RandomDateTimeSequenceGenerator 是一個實作 `ISpecimenBuilder` 介面的類別，所以當我們在測試方法裡有作了以下的設定

```csharp
fixture.Customizations.Add(new RandomDateTimeSequenceGenerator());
```

它會攔截所有 DateTime 型別的請求，並回傳一個隨機但遞增的 DateTime 值。因此：

- 如果你的類別中有多個 DateTime 屬性 (例如 CreateTime、UpdateTime、BirthDate)，
- 而你沒有針對這些屬性做 .With (...) 或其他自訂設定，
- 那麼這些屬性都會被 RandomDateTimeSequenceGenerator 所控制。

**如何避免影響其他屬性？**

可以實作 `ISpecimenBuilder`，建立能指定屬性的 `RandomRangedDateTimeBuilder`。它只處理指定名稱的日期屬性，不會影響其他同為 DateTime 的欄位。

### 自訂 RandomRangedDateTimeBuilder 類別

```csharp
using AutoFixture.Kernel;
using System.Reflection;

public class RandomRangedDateTimeBuilder : ISpecimenBuilder
{
    private readonly DateTime _minDate;
    private readonly DateTime _maxDate;
    private readonly HashSet<string> _targetProperties;

    public RandomRangedDateTimeBuilder(DateTime minDate, DateTime maxDate, params string[] targetProperties)
    {
        if (minDate >= maxDate)
        {
            throw new ArgumentException("最小日期必須小於最大日期", nameof(minDate));
        }

        if (targetProperties == null || targetProperties.Length == 0)
        {
            throw new ArgumentException("必須指定至少一個目標屬性", nameof(targetProperties));
        }

        this._minDate = minDate;
        this._maxDate = maxDate;
        this._targetProperties = new HashSet<string>(targetProperties);
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            this._targetProperties.Contains(propertyInfo.Name))
        {
            // 支援 DateTime 和 DateTime? 類型
            if (propertyInfo.PropertyType == typeof(DateTime) ||
                propertyInfo.PropertyType == typeof(DateTime?))
            {
                var range = this._maxDate - this._minDate;
                var randomTicks = (long)(Random.Shared.NextDouble() * range.Ticks);
                return this._minDate.AddTicks(randomTicks);
            }
        }

        return new NoSpecimen();
    }
}
```

### 使用自訂 RandomRangedDateTimeBuilder

```csharp
[Fact]
public void 使用自訂RandomRangedDateTimeBuilder_只控制特定DateTime屬性()
{
    // Arrange
    var fixture = new Fixture();

    var minDate = new DateTime(2025, 1, 1);
    var maxDate = new DateTime(2025, 12, 31);

    // 使用 RandomRangedDateTimeBuilder 控制 UpdateTime 屬性
    fixture.Customizations.Add(new RandomRangedDateTimeBuilder(minDate, maxDate, "UpdateTime"));

    var otherMinDate = new DateTime(2024, 1, 1);
    var otherMaxDate = new DateTime(2024, 12, 31);

    // 使用 RandomDateTimeSequenceGenerator 控制其他 DateTime 屬性
    fixture.Customizations.Add(new RandomDateTimeSequenceGenerator(otherMinDate, otherMaxDate));

    // Act
    var member = fixture.Create<Member>();

    // Assert
    // UpdateTime 應該在 2025-01-01 ~ 2025-12-31 之間
    member.UpdateTime.Should().BeOnOrAfter(minDate).And.BeOnOrBefore(maxDate);
    (member.UpdateTime >= minDate && member.UpdateTime <= maxDate).Should().BeTrue();
    (member.UpdateTime < otherMinDate || member.UpdateTime > otherMaxDate).Should().BeTrue();

    // CreateTime 應該在 2024-01-01 ~ 2024-12-31 之間
    member.CreateTime.Should().BeOnOrAfter(otherMinDate).And.BeOnOrBefore(otherMaxDate);
    (member.CreateTime >= otherMinDate && member.CreateTime <= otherMaxDate).Should().BeTrue();
    (member.CreateTime < minDate || member.CreateTime > maxDate).Should().BeTrue();
}
```

可以看到 UpdateTime 的值都在指定的日期時間範圍內，而 CreateTime 則不受到影響。

### NoSpecimen 的正確回傳時機

`NoSpecimen` 是 AutoFixture 中的特殊物件，表示目前的建構器無法處理此請求，應該交由責任鏈中的下一個建構器處理：

```csharp
[Fact]
public void RandomRangedDateTimeBuilder_應正確回傳NoSpecimen()
{
    // Arrange
    var minDate = new DateTime(2025, 1, 1);
    var maxDate = new DateTime(2025, 12, 31);

    var builder = new RandomRangedDateTimeBuilder(minDate, maxDate, "CreateTime");

    var context = new SpecimenContext(new Fixture());

    // Act & Assert - 非目標屬性應回傳 NoSpecimen
    var stringPropertyRequest = typeof(Member).GetProperty("Name");
    stringPropertyRequest.Should().NotBeNull();
    var result1 = builder.Create(stringPropertyRequest, context);
    result1.Should().BeOfType<NoSpecimen>();

    // DateTime 但非目標屬性也應回傳 NoSpecimen
    var updateTimePropertyRequest = typeof(Member).GetProperty("UpdateTime");
    updateTimePropertyRequest.Should().NotBeNull();
    var result2 = builder.Create(updateTimePropertyRequest, context);
    result2.Should().BeOfType<NoSpecimen>();

    // 目標屬性應回傳 DateTime
    var createTimePropertyRequest = typeof(Member).GetProperty("CreateTime");
    createTimePropertyRequest.Should().NotBeNull();
    var result3 = builder.Create(createTimePropertyRequest, context);
    result3.Should().BeOfType<DateTime>();
}
```

## 實作可以指定屬性的 RandomRangedNumericSequenceBuilder

對於數值屬性的範圍控制，情況比 DateTime 更複雜，因為 AutoFixture 對 int 和 DateTime 有不同的處理方式。

### 第一個版本：簡單的屬性名稱比對

先從簡單的實作開始：

```csharp
public class RandomRangedNumericSequenceBuilder : ISpecimenBuilder
{
    private readonly int _min;
    private readonly int _max;
    private readonly HashSet<string> _targetProperties;

    public RandomRangedNumericSequenceBuilder(int min, int max, params string[] targetProperties)
    {
        this._min = min;
        this._max = max;
        this._targetProperties = new HashSet<string>(targetProperties);
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(int) &&
            this._targetProperties.Contains(propertyInfo.Name))
        {
            return Random.Shared.Next(this._min, this._max);
        }

        return new NoSpecimen();
    }
}
```

### 第一個版本會失效的原因分析

這個簡單的實作在某些情況下可能會失效：

```csharp
[Fact]
public void 第一個版本RandomRangedNumericSequenceBuilder_可能會失效()
{
    // Arrange
    var fixture = new Fixture();
    fixture.Customizations.Add(new RandomRangedNumericSequenceBuilder(30, 50, "Age"));

    // Act
    var member = fixture.Create<Member>();

    // Assert
    // 這個測試展示第一個版本的問題：
    // 由於 AutoFixture 內建建構器的優先順序更高，我們的自訂建構器可能會失效
    // 結果可能還是使用 DataAnnotations 的範圍（10-80）而不是我們指定的範圍（30-50）
    member.Age.Should().BeInRange(10, 80); // 實際上還是使用 DataAnnotations 的範圍

    // 驗證不在我們指定的範圍內，說明自訂建構器失效了
    if (member.Age is < 30 or >= 50)
    {
        // 這證明了第一個版本的問題：內建建構器優先順序更高
        member.Age.Should().BeInRange(10, 80); // 使用 DataAnnotations 的範圍
    }
}
```

### 問題根源：AutoFixture 內建建構器的優先順序

AutoFixture 內建了許多建構器，如 `RangeAttributeRelay`、`NumericSequenceGenerator` 等，它們可能比我們的自訂建構器有更高的優先順序。

int 和 DateTime 在 AutoFixture 的預設產生流程中，其實走的是不同的行為：

- 整數 (int) 的預設產生器
  - AutoFixture 內建了兩個會先處理 int 的 builder： `RangeAttributeRelay`、`NumericSequenceGenerator`
  - 因為這兩個都已經註冊在 fixture.Customizations 的前面 (Index: 0、1)，如果只是用 fixture.Customizations.Add (...) 把自己的 RandomRangedNumericBuilder 加到後面，請求一到就被前面的 builder 攔走了，你的 builder 永遠不會看到那個屬性請求。

- 日期時間 (DateTime) 的預設產生器
  - AutoFixture 沒有專門處理 DateTime 的 `RangeAttributeRelay` 或序列產生器，只有最後執行的 `ReflectionRelay`（用 ctor 建立物件並注入屬性）。
  - 於是，當遇到你寫的 RandomRangeDateTimeBuilder (不論是用 Add 還是 Insert)，因為前面根本沒有人處理過 DateTime，它就正常接到 CreateTime / UpdateTime 的請求，回傳你指定區間的隨機值。

### 修改版本：使用 Func<PropertyInfo, bool> predicate

為了解決優先順序問題，我們需要更精確的控制：

```csharp
public class ImprovedRandomRangedNumericSequenceBuilder : ISpecimenBuilder
{
    private readonly int _min;
    private readonly int _max;
    private readonly Func<PropertyInfo, bool> _predicate;

    public ImprovedRandomRangedNumericSequenceBuilder(int min, int max, Func<PropertyInfo, bool> predicate)
    {
        this._min = min;
        this._max = max;
        this._predicate = predicate;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(int) &&
            this._predicate(propertyInfo))
        {
            return Random.Shared.Next(this._min, this._max);
        }

        return new NoSpecimen();
    }
}
```

### fixture.Customizations.Insert(0) vs Add() 的差異與重要性

關鍵在於使用 `Insert(0)` 而不是 `Add()`，這樣可以確保我們的建構器有最高的優先順序：

```csharp
[Fact]
public void 使用Insert0確保自訂建構器優先順序_控制Member年齡範圍()
{
    // Arrange
    var fixture = new Fixture();

    // 使用 Insert(0) 確保最高優先順序
    fixture.Customizations.Insert(index: 0,
                                  new ImprovedRandomRangedNumericSequenceBuilder(
                                      min: 30,
                                      max: 50,
                                      predicate: prop => prop.Name == "Age" && prop.DeclaringType == typeof(Member)));

    // Act
    var member = fixture.Create<Member>();

    // Assert
    member.Age.Should().BeInRange(30, 49);
}
```

### 完整測試案例：Member.Age 在 30-50 範圍的驗證

```csharp
[Fact]
public void ImprovedRandomRangedNumericSequenceBuilder_完整測試案例()
{
    // Arrange
    var fixture = new Fixture();
    fixture.Customizations.Insert(index: 0,
                                  new ImprovedRandomRangedNumericSequenceBuilder(
                                      min: 30,
                                      max: 50,
                                      predicate: prop => prop.Name == "Age" && prop.DeclaringType == typeof(Member)));

    // Act
    var members = fixture.CreateMany<Member>(20).ToList();

    // Assert
    members.Should().HaveCount(20);
    members.Should().AllSatisfy(member => { member.Age.Should().BeInRange(30, 49); });

    // 驗證年齡確實是隨機的
    var distinctAges = members.Select(m => m.Age).Distinct().Count();
    distinctAges.Should().BeGreaterThan(1);
}
```

## 將 RandomRangedNumericSequenceBuilder 修改為支援所有數值型別

最後，我們要建立一個支援所有數值型別的泛型化設計，讓同一套機制可以處理各種數值型別。

### 數值型別清單

我們需要支援的數值型別包括：

- int
- long
- short
- byte
- float
- double
- decimal

### 泛型化的 NumericRangeBuilder\<TValue\> 類別設計

```csharp
public class NumericRangeBuilder<TValue> : ISpecimenBuilder
    where TValue : struct, IComparable, IConvertible
{
    private readonly TValue _min;
    private readonly TValue _max;
    private readonly Func<PropertyInfo, bool> _predicate;

    public NumericRangeBuilder(TValue min, TValue max, Func<PropertyInfo, bool> predicate)
    {
        if (Comparer<TValue>.Default.Compare(min, max) >= 0)
        {
            throw new ArgumentException("最小值必須小於最大值", nameof(min));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        this._min = min;
        this._max = max;
        this._predicate = predicate;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            propertyInfo.PropertyType == typeof(TValue) &&
            this._predicate(propertyInfo))
        {
            return this.GenerateRandomValue();
        }

        return new NoSpecimen();
    }

    private TValue GenerateRandomValue()
    {
        // 統一轉換為 decimal 進行計算
        var minDecimal = Convert.ToDecimal(this._min);
        var maxDecimal = Convert.ToDecimal(this._max);
        var range = maxDecimal - minDecimal;
        var randomValue = minDecimal + (decimal)Random.Shared.NextDouble() * range;

        // 根據目標型別轉換回去
        return typeof(TValue).Name switch
        {
            nameof(Int32) => (TValue)(object)(int)randomValue,
            nameof(Int64) => (TValue)(object)(long)randomValue,
            nameof(Int16) => (TValue)(object)(short)randomValue,
            nameof(Byte) => (TValue)(object)(byte)randomValue,
            nameof(Single) => (TValue)(object)(float)randomValue,
            nameof(Double) => (TValue)(object)(double)randomValue,
            nameof(Decimal) => (TValue)(object)randomValue,
            _ => throw new NotSupportedException($"Type {typeof(TValue).Name} is not supported")
        };
    }
}
```

### FixtureRangedNumericExtensions 靜態擴充類別

為了讓使用更方便，我們建立擴充方法：

```csharp
public static class FixtureRangedNumericExtensions
{
    public static IFixture AddRandomRange<T, TValue>(this IFixture fixture,
                                                     TValue min, TValue max, Func<PropertyInfo, bool> predicate)
        where TValue : struct, IComparable, IConvertible
    {
        fixture.Customizations.Insert(0, new NumericRangeBuilder<TValue>(min, max, predicate));
        return fixture;
    }
}
```

### AddRandomRange<T, TValue> 擴充方法的完整實作

```csharp
[Fact]
public void 使用擴充方法_控制不同數值型別的範圍()
{
    // Arrange
    var fixture = new Fixture();

    // 控制不同型別的屬性範圍
    fixture.AddRandomRange<Product, decimal>(
        min: 100m,
        max: 1000m,
        predicate: prop => prop.Name == "Price" && prop.DeclaringType == typeof(Product));

    fixture.AddRandomRange<Product, int>(
        min: 1,
        max: 100,
        predicate: prop => prop.Name == "Quantity" && prop.DeclaringType == typeof(Product));

    // Act
    var product = fixture.Create<Product>();

    // Assert
    product.Price.Should().BeInRange(100m, 1000m);
    product.Quantity.Should().BeInRange(1, 99); // Next 不包含上限
}
```

### Product 和 Order 複雜實體範例

定義包含多種數值型別的複雜實體：

```csharp
public class Product
{
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public double Rating { get; set; }
    
    public float Discount { get; set; }
}

public class Order
{
    public string OrderNumber { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    
    public short ItemCount { get; set; }
    
    public byte Priority { get; set; }
    
    public long CustomerId { get; set; }
}
```

### 多重數值型別範圍設定的完整測試案例

```csharp
[Fact]
public void 複雜實體多重數值型別範圍控制_完整測試案例()
{
    // Arrange
    var fixture = new Fixture();

    // Product 屬性範圍設定
    fixture.AddRandomRange<Product, decimal>(
        min: 50m,
        max: 500m,
        predicate: prop => prop.Name == "Price" && prop.DeclaringType == typeof(Product));
    fixture.AddRandomRange<Product, int>(
        min: 1,
        max: 50,
        predicate: prop => prop.Name == "Quantity" && prop.DeclaringType == typeof(Product));
    fixture.AddRandomRange<Product, double>(
        min: 1.0,
        max: 5.0,
        predicate: prop => prop.Name == "Rating" && prop.DeclaringType == typeof(Product));
    fixture.AddRandomRange<Product, float>(
        min: 0.0f,
        max: 0.5f,
        predicate: prop => prop.Name == "Discount" && prop.DeclaringType == typeof(Product));

    // Order 屬性範圍設定
    fixture.AddRandomRange<Order, decimal>(
        min: 100m,
        max: 10000m,
        predicate: prop => prop.Name == "Amount" && prop.DeclaringType == typeof(Order));
    fixture.AddRandomRange<Order, short>(
        min: (short)1,
        max: (short)20,
        predicate: prop => prop.Name == "ItemCount" && prop.DeclaringType == typeof(Order));
    fixture.AddRandomRange<Order, byte>(
        min: (byte)1,
        max: (byte)5,
        predicate: prop => prop.Name == "Priority" && prop.DeclaringType == typeof(Order));
    fixture.AddRandomRange<Order, long>(
        min: 1000L,
        max: 9999L,
        predicate: prop => prop.Name == "CustomerId" && prop.DeclaringType == typeof(Order));

    // Act
    var products = fixture.CreateMany<Product>(10).ToList();
    var orders = fixture.CreateMany<Order>(10).ToList();

    // Assert
    products.Should().AllSatisfy(product =>
    {
        product.Price.Should().BeInRange(50m, 500m);
        product.Quantity.Should().BeInRange(1, 49);
        product.Rating.Should().BeInRange(1.0, 5.0);
        product.Discount.Should().BeInRange(0.0f, 0.5f);
    });

    orders.Should().AllSatisfy(order =>
    {
        order.Amount.Should().BeInRange(100m, 10000m);
        order.ItemCount.Should().BeInRange((short)1, (short)19);
        order.Priority.Should().BeInRange((byte)1, (byte)4);
        order.CustomerId.Should().BeInRange(1000L, 9998L);
    });
}
```

## 今日重點回顧

### 建議做法

1. **善用 DataAnnotations 整合**
   - 充分利用現有的模型驗證規則
   - 讓 AutoFixture 自動產生符合業務限制的資料

2. **適當的屬性範圍控制**
   - 使用 `.With()` 方法配合 `Random.Shared.Next()` 動態產生隨機值
   - 理解固定值與動態值的差異，選擇適合的方式

3. **自訂建構器的優先順序管理**
   - 使用 `Insert(0)` 而不是 `Add()` 確保自訂建構器優先權
   - 理解 AutoFixture 內建建構器可能覆寫自訂邏輯的問題

4. **泛型化設計的運用**
   - 建立可重用的泛型建構器支援多種數值型別
   - 使用擴充方法提供流暢的設定介面

### 避免做法

1. **忽略建構器優先順序**
   - 不要假設 `Add()` 的自訂建構器一定會生效
   - 記得檢查是否被內建建構器覆寫

2. **過度複雜的自訂邏輯**
   - 避免在建構器中加入過於複雜的業務邏輯
   - 保持建構器的單一職責

3. **循環參考處理不當**
   - 注意物件間的循環參考問題
   - 適當設定遞迴行為避免無限遞迴

## 本日小結

本篇從 DataAnnotations 整合一路做到泛型數值範圍建構器。AutoFixture 不只負責產生資料；加入適當的 Customization 後，也能把業務限制寫進資料產生規則。

### 進階技術掌握

- **DataAnnotations 自動整合**：AutoFixture 能自動識別並遵循模型驗證規則，產生符合限制的測試資料
- **精確的屬性控制**：用 `.With()` 方法和 `Random.Shared` 動態產生可重複的屬性值
- **DateTime 範圍控制演進**：從全域的 `RandomDateTimeSequenceGenerator` 到精確的 `RandomRangedDateTimeBuilder`
- **自訂 ISpecimenBuilder 實作**：深入理解 AutoFixture 的擴展機制，實作符合特定需求的建構器

### 數值範圍建構器的技術突破

- **基礎實作**：`RandomRangedNumericSequenceBuilder` 使用屬性名稱匹配的簡單方式
- **改進版本**：`ImprovedRandomRangedNumericSequenceBuilder` 採用條件函式提供更靈活的判斷機制
- **泛型化設計**：`NumericRangeBuilder<TValue>` 支援所有數值型別，實現真正的可重用性
- **流暢介面**：`FixtureRangedNumericExtensions` 提供優雅的擴充方法語法

### 架構設計原則

- **單一職責**：每個建構器專注於特定的資料產生邏輯
- **開放封閉**：以泛型和擴充方法保留擴充空間
- **相依反轉**：使用 `ISpecimenBuilder` 介面實現可插拔的建構器架構
- **最小意外原則**：建構器行為符合直覺，`Insert(0)` 確保優先順序符合預期

### 實務應用價值

這些技術適合用在以下需求：

1. **減少樣板程式碼**：從手動設定每個屬性到自動化的範圍控制
2. **提升測試涵蓋率**：輕鬆產生大量符合業務規則的測試資料
3. **增強測試穩定性**：精確控制範圍，避免隨機資料造成不穩定測試
4. **改善程式碼維護性**：泛型化設計讓同一套邏輯可應用於多種型別

### 學習行程回顧

Day 10 講的是 AutoFixture 的基本用法，今天補上自訂建構器和泛型化設計。差別在於，現在你可以把資料產生的範圍控制到很精確。

## 使用的套件版本

本章範例使用以下套件版本：

- **AutoFixture**: 4.18.1
- **xunit.v3.mtp-v2**: 3.2.2
- **AwesomeAssertions**: 9.5.0
- **.NET**: 10.0

這些版本是本文範例實際使用的組合。AutoFixture 4.18.1 支援 DataAnnotations 與自訂建構器。

下一篇會整合 AutoData 屬性與 xUnit，用屬性標註自動產生測試參數，繼續減少資料準備程式碼。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day11>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十一天。明天會介紹 Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用。**
