---
day: 15
title: "Day 15 - AutoFixture 與 Bogus 的整合應用"
sample: samples/day15
target_framework: net10.0
packages:
  - AutoFixture
  - AutoFixture.Xunit3
  - AwesomeAssertions
  - Bogus
  - Microsoft.Testing.Extensions.TrxReport
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 15 - AutoFixture 與 Bogus 的整合應用

<!-- toc -->

- [前言](#前言)
- [需要的套件和參考](#需要的套件和參考)
- [AutoFixture 與 Bogus 整合的核心概念](#autofixture-與-bogus-整合的核心概念)
- [完整的整合範例](#完整的整合範例)
- [類型層級的整合](#類型層級的整合)
- [建立整合的測試資料工廠](#建立整合的測試資料工廠)
- [Seed 統一管理](#seed-統一管理)
- [實戰應用案例](#實戰應用案例)
- [今日小結](#今日小結)
- [相關參考資料](#相關參考資料)

<!-- /toc -->

## 前言

前幾篇分別介紹了 AutoFixture 與 Bogus：

- AutoFixture 擅長匿名測試和快速物件建構
- Bogus 則專精於真實感的語意化資料產生

但在實際專案中，我們常常需要結合兩種工具的優勢來建立好用的測試資料產生策略。

這一篇把兩套工具接在一起，建立可重用的測試資料產生策略。

## 需要的套件和參考

在開始整合之前，先確認你的專案已安裝必要的 NuGet 套件：

```xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="Bogus" Version="35.6.5" />
<PackageReference Include="AutoFixture.Xunit3" Version="4.19.0" />
<PackageReference Include="AwesomeAssertions" Version="9.4.0" />
<PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
```

並加入這些 using 語句：

```csharp
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit3;
using Bogus;
using AwesomeAssertions;
using System.Reflection;
```

## AutoFixture 與 Bogus 整合的核心概念

### 為什麼需要整合？

在實際使用中，我們會發現 AutoFixture 和 Bogus 各有優勢：

**AutoFixture 的優勢**：

- 快速產生匿名測試資料
- 自動處理複雜物件結構
- 好用的循環參考處理

**Bogus 的優勢**：

- 產生真實感的資料
- 豐富的資料類型支援
- 對驗證比較友善的資料格式

但單獨使用時會遇到問題：

```csharp
// 使用 AutoFixture 的問題：資料不夠真實
var user = fixture.Create<User>();
// user.Email 可能是 "EmailGuid123"，不像真實 Email

// 使用 Bogus 的問題：設定複雜物件很繁瑣
var userFaker = new Faker<User>()
    .RuleFor(u => u.FirstName, f => f.Person.FirstName)
    .RuleFor(u => u.LastName, f => f.Person.LastName)
    // ... 需要為每個屬性設定規則
```

### 整合後的效果

整合兩個工具後，我們可以同時享受到兩者的優點：

```csharp
// 整合後：結合兩者優勢
var user = hybridGenerator.Generate<User>();
// 既有 AutoFixture 的便利性，又有 Bogus 的真實感
```

這樣產生的 `User` 物件會有：

- 真實的 Email 格式（來自 Bogus）
- 合理的姓名（來自 Bogus）  
- 自動填充的其他屬性（來自 AutoFixture）
- 正確處理的物件關聯（來自 AutoFixture）

### 實際應用情境：混合資料產生策略

我們先設計一個統一的資料產生介面。這個介面會成為整個測試資料產生系統的核心，讓不同的產生器都能提供一致的 API：

```csharp
/// <summary>
/// 統一的測試資料產生介面
/// </summary>
public interface ITestDataGenerator
{
    /// <summary>
    /// 產生單一物件
    /// </summary>
    T Generate<T>();

    /// <summary>
    /// 產生指定數量的物件
    /// </summary>
    IEnumerable<T> Generate<T>(int count);

    /// <summary>
    /// 產生物件並允許後續設定
    /// </summary>
    T Generate<T>(Action<T> configure);

    /// <summary>
    /// 產生物件並允許建構參數客製化
    /// </summary>
    T Generate<T>(params object[] constructorParameters);
}

/// <summary>
/// 混合資料產生器實作
/// </summary>
public class HybridTestDataGenerator : ITestDataGenerator
{
    private readonly IFixture _fixture;

    public HybridTestDataGenerator(int? seed = null)
    {
        _fixture = new Fixture();

        // 設定 Seed 以確保測試可重現性
        if (seed.HasValue)
        {
            SetSeed(seed.Value);
        }

        // 設定 AutoFixture 的預設行為
        ConfigureAutoFixture();

        // 整合 Bogus 到 AutoFixture
        IntegrateBogus();
    }

    public T Generate<T>()
    {
        return _fixture.Create<T>();
    }

    public IEnumerable<T> Generate<T>(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Generate<T>());
    }

    public T Generate<T>(Action<T> configure)
    {
        var item = Generate<T>();
        configure(item);
        return item;
    }

    public T Generate<T>(params object[] constructorParameters)
    {
        if (constructorParameters.Length == 0)
        {
            return Generate<T>();
        }

        return _fixture.Build<T>()
                       .FromFactory(() => (T)Activator.CreateInstance(typeof(T), constructorParameters)!)
                       .Create();
    }

    /// <summary>
    /// 取得底層的 AutoFixture 執行個體，供進階使用
    /// </summary>
    public IFixture GetFixture()
    {
        return _fixture;
    }

    private void SetSeed(int seed)
    {
        // 設定 AutoFixture 的隨機種子
        var random = new Random(seed);
        _fixture.Register(() => random);

        // 設定 Bogus 的隨機種子（稍後在 SpecimenBuilder 中使用）
        Randomizer.Seed = new Random(seed);
    }

    private void ConfigureAutoFixture()
    {
        // 循環參考處理
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // 設定集合長度
        _fixture.RepeatCount = 3;
    }

    private void IntegrateBogus()
    {
        // 先加入屬性層級的整合（優先級較高）
        _fixture.Customizations.Add(new EmailSpecimenBuilder());
        _fixture.Customizations.Add(new PhoneSpecimenBuilder());
        _fixture.Customizations.Add(new NameSpecimenBuilder());
        _fixture.Customizations.Add(new AddressSpecimenBuilder());
        _fixture.Customizations.Add(new WebsiteSpecimenBuilder());
        _fixture.Customizations.Add(new CompanyNameSpecimenBuilder());

        // 再加入類型層級的整合（優先級較低）
        // 使用種子感知的 SpecimenBuilder 以確保一致性
        _fixture.Customizations.Add(new SeedAwareBogusSpecimenBuilder(GetCurrentSeed()));
    }

    private int? GetCurrentSeed()
    {
        // 嘗試從 Randomizer 取得目前的種子
        return Randomizer.Seed?.Next();
    }
}
```

### 自訂 SpecimenBuilder 整合 Bogus

整合點是 `ISpecimenBuilder` 介面。它能攔截 AutoFixture 的物件建立流程，並在符合條件時改由 Bogus 產生資料。

以下是幾個實用的 SpecimenBuilder 範例，它們會根據屬性名稱來決定是否使用 Bogus：

```csharp
/// <summary>
/// Email 屬性的 Bogus 整合
/// </summary>
public class EmailSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property && 
            property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return _faker.Internet.Email();
        }
        
        return new NoSpecimen();
    }
}

/// <summary>
/// 電話號碼屬性的 Bogus 整合
/// </summary>
public class PhoneSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property && 
            property.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            return _faker.Phone.PhoneNumber();
        }
        
        return new NoSpecimen();
    }
}

/// <summary>
/// 姓名屬性的 Bogus 整合
/// </summary>
public class NameSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property)
        {
            return property.Name.ToLower() switch
            {
                var name when name.Contains("firstname") => _faker.Person.FirstName,
                var name when name.Contains("lastname") => _faker.Person.LastName,
                var name when name.Contains("fullname") => _faker.Person.FullName,
                _ => new NoSpecimen()
            };
        }
        
        return new NoSpecimen();
    }
}

/// <summary>
/// 地址屬性的 Bogus 整合
/// </summary>
public class AddressSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property)
        {
            return property.Name.ToLower() switch
            {
                var name when name.Contains("street") => _faker.Address.StreetAddress(),
                var name when name.Contains("city") => _faker.Address.City(),
                var name when name.Contains("postal") || name.Contains("zip") => _faker.Address.ZipCode(),
                var name when name.Contains("country") => _faker.Address.Country(),
                _ => new NoSpecimen()
            };
        }

        return new NoSpecimen();
    }
}

/// <summary>
/// 網站 URL 屬性的 Bogus 整合
/// </summary>
public class WebsiteSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property &&
            property.Name.Contains("Website", StringComparison.OrdinalIgnoreCase))
        {
            return _faker.Internet.Url();
        }

        return new NoSpecimen();
    }
}

/// <summary>
/// 公司名稱屬性的 Bogus 整合
/// </summary>
public class CompanyNameSpecimenBuilder : ISpecimenBuilder
{
    private readonly Faker _faker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo property && 
            property.DeclaringType?.Name == "Company" &&
            property.Name.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            return _faker.Company.CompanyName();
        }

        return new NoSpecimen();
    }
}
```

## 完整的整合範例

### 針對整個類型的整合

除了針對屬性的整合，我們也可以為整個類型建立 Bogus 產生器：

```csharp
/// <summary>
/// 整合 Bogus 的 AutoFixture SpecimenBuilder
/// </summary>
public class BogusSpecimenBuilder : ISpecimenBuilder
{
    private readonly Dictionary<Type, object> _fakers;

    public BogusSpecimenBuilder()
    {
        _fakers = new Dictionary<Type, object>();
        RegisterFakers();
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && _fakers.TryGetValue(type, out var faker))
        {
            return GenerateWithFaker(faker);
        }
        
        return new NoSpecimen();
    }

    private void RegisterFakers()
    {
        // 註冊使用者相關的 Faker
        _fakers[typeof(User)] = new Faker<User>()
            .RuleFor(u => u.Id, f => f.Random.Guid())
            .RuleFor(u => u.FirstName, f => f.Person.FirstName)
            .RuleFor(u => u.LastName, f => f.Person.LastName)
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
            .RuleFor(u => u.BirthDate, f => f.Person.DateOfBirth)
            .RuleFor(u => u.Age, f => f.Random.Int(18, 80))
            .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber())
            .Ignore(u => u.HomeAddress)
            .Ignore(u => u.Company)
            .Ignore(u => u.Orders);

        // 註冊地址相關的 Faker
        _fakers[typeof(Address)] = new Faker<Address>()
            .RuleFor(a => a.Id, f => f.Random.Guid())
            .RuleFor(a => a.Street, f => f.Address.StreetAddress())
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.Country, f => f.Address.Country());

        // 註冊公司相關的 Faker
        _fakers[typeof(Company)] = new Faker<Company>()
            .RuleFor(c => c.Id, f => f.Random.Guid())
            .RuleFor(c => c.Name, f => f.Company.CompanyName())
            .RuleFor(c => c.Industry, f => f.Commerce.Department())
            .RuleFor(c => c.Website, f => f.Internet.Url())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .Ignore(c => c.Address)
            .Ignore(c => c.Employees);

        // 註冊產品相關的 Faker
        _fakers[typeof(Product)] = new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => f.Random.Decimal(1, 1000))
            .RuleFor(p => p.Category, f => f.Commerce.Categories(1).First())
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.8f));

        // 註冊訂單項目相關的 Faker
        _fakers[typeof(OrderItem)] = new Faker<OrderItem>()
            .RuleFor(oi => oi.Id, f => f.Random.Guid())
            .RuleFor(oi => oi.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(oi => oi.UnitPrice, f => f.Random.Decimal(1, 500))
            .Ignore(oi => oi.Product);

        // 註冊訂單相關的 Faker
        _fakers[typeof(Order)] = new Faker<Order>()
            .RuleFor(o => o.Id, f => f.Random.Guid())
            .RuleFor(o => o.OrderDate, f => f.Date.Recent(30))
            .RuleFor(o => o.TotalAmount, f => f.Random.Decimal(10, 5000))
            .RuleFor(o => o.Status, f => f.Random.Enum<OrderStatus>())
            .Ignore(o => o.Customer)
            .Ignore(o => o.Items);
    }

    private object GenerateWithFaker(object faker)
    {
        var generateMethod = faker.GetType().GetMethod("Generate", Type.EmptyTypes);
        return generateMethod?.Invoke(faker, null) ?? new NoSpecimen();
    }
}
```

### 擴充方法：讓整合更好用

光有 SpecimenBuilder 還不夠，我們需要一些方便的擴充方法來簡化使用。但在加入這些方法之前，我們得先處理一個重要的問題：循環參考。

#### 處理循環參考的重要性

在整合 AutoFixture 和 Bogus 之前，我們需要先解決**循環參考**問題。

**什麼是循環參考？**

看看這個常見的業務模型：

```csharp
public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public int Age { get; set; }
    public Address? HomeAddress { get; set; }
    public Company? Company { get; set; }  // User 參考 Company
    public List<Order> Orders { get; set; } = new();
}

public class Company  
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address? Address { get; set; }
    public List<User> Employees { get; set; } = new();  // Company 參考 User 集合
}
```

當 AutoFixture 嘗試建立 `User` 物件時會發生：

1. 建立 `User` → 需要建立 `Company` 屬性
2. 建立 `Company` → 需要建立 `Employees` 集合  
3. 建立 `Employees` → 需要建立 `User` 物件
4. 建立 `User` → 又需要建立 `Company` 屬性……無限迴圈！

結果就是 `StackOverflowException` 或 `ObjectCreationException`。

#### AutoFixture 的預設行為

AutoFixture 預設使用 `ThrowingRecursionBehavior`，當偵測到循環參考時會拋出例外：

```csharp
// 預設會拋出 ObjectCreationExceptionWithPath
var user = fixture.Create<User>(); 
// AutoFixture was unable to create an instance because 
// the traversed object graph contains a circular reference
```

#### 解決方案：OmitOnRecursionBehavior

`OmitOnRecursionBehavior` 會在偵測到循環參考時，把重複的屬性設為 `null` 或空集合，避免無限迴圈：

```csharp
var fixture = new Fixture();
fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
    .ToList()
    .ForEach(b => fixture.Behaviors.Remove(b));
fixture.Behaviors.Add(new OmitOnRecursionBehavior());

var user = fixture.Create<User>(); 
// 成功建立，但 user.Company.Employees 可能為空以避免循環參考
```

#### 在整合時為什麼特別重要？

整合 AutoFixture 和 Bogus 時，我們希望：

- 建立完整的測試物件結構
- 使用 Bogus 產生真實感的資料  
- 避免因循環參考導致測試失敗
- 確保測試執行穩定

所以在任何整合方案中，循環參考處理都是第一優先。

```csharp
/// <summary>
/// 設定 AutoFixture 使用 Bogus 整合
/// </summary>
public static class FixtureExtensions
{
    /// <summary>
    /// 為 AutoFixture 加入 Bogus 整合功能
    /// </summary>
    public static IFixture WithBogus(this IFixture fixture)
    {
        // 先設定循環參考處理
        fixture.WithOmitOnRecursion();

        // 先加入屬性層級的整合
        fixture.Customizations.Add(new EmailSpecimenBuilder());
        fixture.Customizations.Add(new PhoneSpecimenBuilder());
        fixture.Customizations.Add(new NameSpecimenBuilder());
        fixture.Customizations.Add(new AddressSpecimenBuilder());
        fixture.Customizations.Add(new WebsiteSpecimenBuilder());
        fixture.Customizations.Add(new CompanyNameSpecimenBuilder());

        // 再加入類型層級的整合
        fixture.Customizations.Add(new BogusSpecimenBuilder());

        return fixture;
    }

    /// <summary>
    /// 為特定類型註冊 Bogus Faker
    /// </summary>
    public static IFixture WithBogusFor<T>(this IFixture fixture, Faker<T> faker)
        where T : class
    {
        fixture.Customizations.Add(new TypedBogusSpecimenBuilder<T>(faker));
        return fixture;
    }

    /// <summary>
    /// 設定 AutoFixture 的循環參考處理
    /// </summary>
    public static IFixture WithOmitOnRecursion(this IFixture fixture)
    {
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture;
    }

    /// <summary>
    /// 設定集合的預設長度
    /// </summary>
    public static IFixture WithRepeatCount(this IFixture fixture, int count)
    {
        fixture.RepeatCount = count;
        return fixture;
    }

    /// <summary>
    /// 設定隨機種子以確保測試可重現性
    /// </summary>
    public static IFixture WithSeed(this IFixture fixture, int seed)
    {
        var random = new Random(seed);
        fixture.Register(() => random);
        Randomizer.Seed = new Random(seed);

        return fixture;
    }
}
```

#### 循環參考處理的實際效果

寫個簡單測試，確認 `WithOmitOnRecursion` 的效果：

```csharp
[Fact]
public void 循環參考處理_對比測試()
{
    // 沒有處理循環參考的情況 - 會拋出例外
    var defaultFixture = new Fixture();
    // var user1 = defaultFixture.Create<User>(); // 這行會炸掉

    // 有處理循環參考的情況 - 正常運作
    var safeFixture = new Fixture().WithOmitOnRecursion().WithBogus();
    var user2 = safeFixture.Create<User>(); // 成功建立

    // 驗證結果
    user2.Should().NotBeNull();
    user2.Email.Should().Contain("@"); // Bogus 產生的真實 Email
    user2.FirstName.Should().NotBeNullOrEmpty(); // Bogus 產生的真實姓名
    
    // Company 物件會被建立，但 Employees 可能為空以避免循環參考
    user2.Company.Should().NotBeNull();
    user2.Company.Name.Should().NotBeNullOrEmpty();
}

[Fact] 
public void 理解_OmitOnRecursion_的行為()
{
    // Arrange
    var fixture = new Fixture().WithOmitOnRecursion().WithBogus();
    
    // Act
    var company = fixture.Create<Company>();
    
    // Assert
    company.Should().NotBeNull();
    company.Name.Should().NotBeNullOrEmpty(); // Bogus 產生的公司名稱
    company.Employees.Should().NotBeNull(); // 集合會被建立
    
    if (company.Employees.Any())
    {
        var firstEmployee = company.Employees.First();
        firstEmployee.Email.Should().Contain("@");
        
        // 重點：員工的 Company 屬性可能為 null（避免循環參考）
        // 這是 OmitOnRecursionBehavior 的正常行為
        Console.WriteLine($"Employee company is null: {firstEmployee.Company == null}");
    }
}
```

#### 實務建議

第一，永遠先處理循環參考：

```csharp
// 推薦的做法
public static IFixture WithBogus(this IFixture fixture)
{
    fixture.WithOmitOnRecursion(); // 先處理循環參考
    // 然後加入 Bogus 整合...
}
```

第二，測試時專注於重要屬性：

```csharp
[Fact]
public void 測試應該專注於業務邏輯_而非物件圖完整性()
{
    var user = fixture.Create<User>();
    
    // 專注於測試所需的屬性
    user.Email.Should().Contain("@");
    user.FirstName.Should().NotBeNullOrEmpty();
    user.Company.Name.Should().NotBeNullOrEmpty();
    
    // 不用驗證 user.Company.Employees 是否包含該 user
    // 因為這是 OmitOnRecursionBehavior 故意避免的循環參考
}
```

第三，了解循環參考處理的取捨：

優點：

- 避免 StackOverflow 例外
- 測試執行穩定
- 可以建立複雜的物件結構

要注意：

- 某些循環參考屬性可能為 null
- 需要了解哪些屬性會被省略
- 不適合需要完整物件圖的測試情境

### 自訂 AutoData 屬性整合

為了讓測試更簡潔，我們可以建立一個自訂的 AutoData 屬性，自動套用 Bogus 整合：

```csharp
/// <summary>
/// 整合 Bogus 的 AutoData 屬性
/// </summary>
public class BogusAutoDataAttribute : AutoDataAttribute
{
    public BogusAutoDataAttribute() : base(() => new Fixture().WithBogus())
    {
    }
}
```

這樣就可以在測試中直接使用：

```csharp
[Theory]
[BogusAutoData]
public void 使用_BogusAutoData_測試(User user, Address address)
{
    // 資料會自動使用 Bogus 整合產生
    user.Email.Should().Contain("@");
    address.City.Should().NotBeNullOrEmpty();
}
```

## 類型層級的整合

### 針對特定類型的 Bogus 整合

```csharp
/// <summary>
/// 針對特定類型的 Bogus 整合
/// </summary>
public class TypedBogusSpecimenBuilder<T> : ISpecimenBuilder where T : class
{
    private readonly Faker<T> _faker;

    public TypedBogusSpecimenBuilder(Faker<T> faker)
    {
        _faker = faker;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(T))
        {
            return _faker.Generate();
        }
        
        return new NoSpecimen();
    }
}
```

### 實際整合使用範例

```csharp
public class IntegratedTestDataTests
{
    private readonly IFixture _fixture;
    private readonly HybridTestDataGenerator _generator;

    public IntegratedTestDataTests()
    {
        // 方法一：使用擴充方法
        _fixture = new Fixture().WithBogus();
        
        // 方法二：使用混合產生器
        _generator = new HybridTestDataGenerator();
    }

    [Fact]
    public void AutoFixture_整合_Bogus_應能產生真實感資料()
    {
        // Arrange & Act
        var user = _fixture.Create<User>();
        var company = _fixture.Create<Company>();
        
        // Assert - User 使用 Bogus 產生，有真實感的資料
        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
        user.Phone.Should().MatchRegex(@"[\d\-\(\)\s\+\.x]+");

        // Company 使用 Bogus 產生
        company.Name.Should().NotBeNullOrEmpty();
        company.Website.Should().StartWith("http");
    }

    [Fact]
    public void 混合產生器_應能自動處理複雜物件()
    {
        // Arrange & Act
        var order = _generator.Generate<Order>();

        // Assert
        order.Should().NotBeNull();
        order.Customer.Email.Should().Contain("@");                   // Customer 使用 Bogus
        order.Items.Should().NotBeEmpty();                            // Items 由 AutoFixture 處理
        order.Items.First().Product.Name.Should().NotBeNullOrEmpty(); // Product.Name 使用 Bogus
    }

    [Theory]
    [BogusAutoData]
    public void 使用_AutoData_與_Bogus_整合(User user, Address address)
    {
        // Arrange - 資料由整合後的 AutoFixture 自動產生

        // Assert
        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
        address.City.Should().NotBeNullOrEmpty();
        address.Country.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void 客製化_特定類型的_Bogus_產生器()
    {
        // Arrange
        var customUserFaker = new Faker<User>()
            .RuleFor(u => u.FirstName, "John")
            .RuleFor(u => u.LastName, "Doe")
            .RuleFor(u => u.Age, f => f.Random.Int(25, 65));

        var customFixture = new Fixture()
            .WithBogusFor(customUserFaker);

        // Act
        var user = customFixture.Create<User>();

        // Assert
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Age.Should().BeInRange(25, 65);
    }

    [Fact]
    public void 應能正確處理循環參考()
    {
        // Arrange & Act
        var company = _fixture.Create<Company>();

        // Assert
        company.Should().NotBeNull();
        company.Name.Should().NotBeNullOrEmpty();

        // 驗證循環參考被正確處理（不會拋出例外）
        company.Employees.Should().NotBeNull();

        // 使用 OmitOnRecursionBehavior 時，循環參考的屬性會被設為 null 以避免無限迴圈
        // 這是正常行為，表示循環參考被正確處理
        if (company.Employees.Any())
        {
            var firstEmployee = company.Employees.First();
            // OmitOnRecursionBehavior 會在遇到循環參考時將屬性設為 null
            // 這是預期行為，表示成功處理了循環參考問題
        }
    }
}
```

## 建立整合的測試資料工廠

### 實戰整合應用

在實際專案中，我們可以建立一個完整的測試資料工廠：

```csharp
/// <summary>
/// 整合測試資料工廠
/// </summary>
public class IntegratedTestDataFactory
{
    private readonly IFixture _fixture;
    private readonly Dictionary<Type, object> _cache;
    private readonly Random _random;

    public IntegratedTestDataFactory(int? seed = null)
    {
        _cache = new Dictionary<Type, object>();
        _random = seed.HasValue ? new Random(seed.Value) : new Random();

        _fixture = new Fixture()
                   .WithBogus()
                   .WithOmitOnRecursion()
                   .WithRepeatCount(3);

        if (seed.HasValue)
        {
            _fixture.WithSeed(seed.Value);
        }

        // 初始化產生器
        InitializeGenerators();
    }

    /// <summary>
    /// 取得或建立快取版本的產生器
    /// </summary>
    public T GetCached<T>() where T : class
    {
        var type = typeof(T);
        if (_cache.TryGetValue(type, out var cached))
        {
            return (T)cached;
        }

        var instance = _fixture.Create<T>();
        _cache[type] = instance;
        return instance;
    }

    /// <summary>
    /// 建立新的執行個體（不使用快取）
    /// </summary>
    public T CreateFresh<T>()
    {
        return _fixture.Create<T>();
    }

    /// <summary>
    /// 建立多個執行個體
    /// </summary>
    public List<T> CreateMany<T>(int count = 3)
    {
        return _fixture.CreateMany<T>(count).ToList();
    }

    /// <summary>
    /// 建立並設定執行個體
    /// </summary>
    public T Create<T>(Action<T> configure)
    {
        var instance = _fixture.Create<T>();
        configure(instance);
        return instance;
    }

    /// <summary>
    /// 清除快取
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 取得底層 AutoFixture 執行個體
    /// </summary>
    public IFixture GetFixture()
    {
        return _fixture;
    }

    private void InitializeGenerators()
    {
        // 註冊特殊的 Faker，例如台灣地區相關資料
        var taiwanUserFaker = new Faker<User>("zh_TW")
                              .RuleFor(u => u.Id, f => f.Random.Guid())
                              .RuleFor(u => u.FirstName, f => f.Person.FirstName)
                              .RuleFor(u => u.LastName, f => f.Person.LastName)
                              .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
                              .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber("09########"))
                              .RuleFor(u => u.BirthDate, f => f.Person.DateOfBirth)
                              .RuleFor(u => u.Age, f => f.Random.Int(18, 80));

        // 可以選擇性地使用台灣地區的 Faker
        _fixture.WithBogusFor(taiwanUserFaker);
    }

    /// <summary>
    /// 建立完整的測試情境
    /// </summary>
    public TestScenario CreateTestScenario()
    {
        var company = CreateFresh<Company>();
        var users = CreateMany<User>(5);
        var orders = CreateMany<Order>(10);

        // 建立關聯
        foreach (var user in users)
        {
            user.Company = company;
            user.HomeAddress = CreateFresh<Address>();
        }

        foreach (var order in orders)
        {
            order.Customer = users[_random.Next(users.Count)];
            order.Items = CreateMany<OrderItem>(_random.Next(1, 5));

            foreach (var item in order.Items)
            {
                item.Product = CreateFresh<Product>();
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
        }

        company.Employees = users;

        return new TestScenario
        {
            Company = company,
            Users = users,
            Orders = orders
        };
    }
}

/// <summary>
/// 測試情境資料結構
/// </summary>
public class TestScenario
{
    public Company Company { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
}
```

### 工廠使用範例

```csharp
public class IntegratedFactoryTests
{
    private readonly IntegratedTestDataFactory _factory = new();

    [Fact]
    public void 工廠_應能產生完整的測試資料()
    {
        // Arrange & Act
        var user = _factory.CreateFresh<User>();
        var company = _factory.CreateFresh<Company>();

        // Assert
        user.Email.Should().Contain("@");
        user.FirstName.Should().NotBeNullOrEmpty();
        company.Name.Should().NotBeNullOrEmpty();
        company.Website.Should().StartWith("http");
    }

    [Fact]
    public void 工廠_應能客製化物件屬性()
    {
        // Arrange & Act
        var user = _factory.Create<User>(u =>
        {
            u.Age = 30;
        });

        // Assert
        user.Age.Should().Be(30);
        user.Email.Should().Contain("@"); // 其他屬性仍由 Bogus 產生
    }

    [Fact]
    public void 工廠_應能建立完整的測試情境()
    {
        // Arrange & Act
        var scenario = _factory.CreateTestScenario();

        // Assert
        scenario.Company.Should().NotBeNull();
        scenario.Users.Should().HaveCount(5);
        scenario.Orders.Should().HaveCount(10);

        // 驗證關聯關係
        scenario.Users.Should().AllSatisfy(user =>
        {
            user.Company.Should().Be(scenario.Company);
            user.HomeAddress.Should().NotBeNull();
            user.Email.Should().Contain("@");
        });

        scenario.Orders.Should().AllSatisfy(order =>
        {
            order.Customer.Should().BeOneOf(scenario.Users);
            order.Items.Should().NotBeEmpty();
            order.TotalAmount.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void 工廠_應能產生集合資料()
    {
        // Arrange & Act
        var users = _factory.CreateMany<User>(5);

        // Assert
        users.Should().HaveCount(5);
        users.Should().OnlyContain(u => !string.IsNullOrEmpty(u.Email));
        users.Select(u => u.Email).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void 工廠_快取功能_應能正常運作()
    {
        // Arrange & Act
        var user1 = _factory.GetCached<User>();
        var user2 = _factory.GetCached<User>();

        // Assert - 快取應該回傳相同執行個體
        user1.Should().BeSameAs(user2);

        // 清除快取後應該產生新執行個體
        _factory.ClearCache();
        var user3 = _factory.GetCached<User>();
        user3.Should().NotBeSameAs(user1);
    }
}
```

## Seed 統一管理

### 確保測試可重現性的實際考量

整合兩個工具時，測試資料的可重現性需要特別注意。在 `HybridTestDataGenerator` 和 `IntegratedTestDataFactory` 中，都支援 Seed 設定，但需要了解實際的限制：

```csharp
// 使用 HybridTestDataGenerator 時設定 Seed
var generator1 = new HybridTestDataGenerator(seed: 12345);
var user1 = generator1.Generate<User>();

var generator2 = new HybridTestDataGenerator(seed: 12345);
var user2 = generator2.Generate<User>();

// user1 和 user2 會有相似的資料品質，但不一定完全相同

// 使用 IntegratedTestDataFactory 時設定 Seed
var factory1 = new IntegratedTestDataFactory(seed: 12345);
var user3 = factory1.CreateFresh<User>();

var factory2 = new IntegratedTestDataFactory(seed: 12345);
var user4 = factory2.CreateFresh<User>();

// user3 和 user4 同樣會有相似的資料品質
```

#### 重要說明：AutoFixture 與 Bogus 整合的可重現性限制

由於 AutoFixture 和 Bogus 有不同的隨機數管理機制，完全的可重現性在整合情境中是有技術挑戰的：

1. **Bogus 的 Randomizer.Seed 是全域設定**：多個 Faker 執行個體會共享相同的隨機狀態
2. **AutoFixture 的隨機數管理**：與 Bogus 的機制不完全同步
3. **SpecimenBuilder 的執行順序**：可能影響最終的資料產生

#### Seed 整合的實務建議

如果你的專案需要**完全的可重現性**，建議：

- 使用單一工具（純 AutoFixture 或純 Bogus）
- 在整合環境中，著重**資料品質的一致性**，不要求每個值完全相同
- 使用 Seed 確保**測試行為的穩定性**和**大致的可重現性**

#### 在整合環境中，Seed 的實際作用

- 測試每次都能穩定執行
- 資料格式與品質維持一致
- `CreateTestScenario()` 的結構性選擇保持一致，例如客戶分配與項目數量
- 不保證所有屬性值完全相同

## 實戰應用案例

### 建立測試基底類別

實際專案中可以提供一個統一的測試基底類別，整合所有的資料產生功能：

```csharp
/// <summary>
/// 測試基底類別，提供統一的資料產生功能
/// </summary>
public abstract class TestBase
{
    protected readonly IFixture Fixture;
    protected readonly HybridTestDataGenerator Generator;
    protected readonly IntegratedTestDataFactory Factory;

    protected TestBase(int? seed = null)
    {
        // 建立統一設定的 AutoFixture
        Fixture = new Fixture()
            .WithBogus()
            .WithOmitOnRecursion()
            .WithRepeatCount(3);

        if (seed.HasValue)
        {
            Fixture.WithSeed(seed.Value);
        }

        // 建立混合產生器
        Generator = new HybridTestDataGenerator(seed);

        // 建立整合工廠
        Factory = new IntegratedTestDataFactory(seed);
    }

    /// <summary>
    /// 快速建立單一物件
    /// </summary>
    protected T Create<T>() => Fixture.Create<T>();

    /// <summary>
    /// 快速建立多個物件
    /// </summary>
    protected List<T> CreateMany<T>(int count = 3)
        => Fixture.CreateMany<T>(count).ToList();

    /// <summary>
    /// 建立並設定物件
    /// </summary>
    protected T Create<T>(Action<T> configure)
    {
        var instance = Create<T>();
        configure(instance);
        return instance;
    }

    /// <summary>
    /// 記錄測試資訊
    /// </summary>
    protected virtual void LogTestInfo(string info)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {info}");
    }
}
```

### 進階測試基底類別

除了基本的 TestBase，實際專案中還有一個更完整的版本：

```csharp
/// <summary>
/// 測試基底類別，提供統一的資料產生功能
/// </summary>
public abstract class IntegratedTestBase
{
    protected readonly IntegratedTestDataFactory Factory;
    protected readonly IFixture Fixture;

    protected IntegratedTestBase(int? seed = null)
    {
        Factory = new IntegratedTestDataFactory(seed);
        Fixture = new Fixture().WithBogus();
        
        if (seed.HasValue)
        {
            Fixture.WithSeed(seed.Value);
        }
    }

    protected virtual void LogTestInfo(string info)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {info}");
    }
}

/// <summary>
/// 服務層測試範例
/// </summary>
public class OrderServiceTests : IntegratedTestBase
{
    public OrderServiceTests() : base(seed: 12345)
    {
    }

    [Fact]
    public void CreateOrder_使用整合資料產生_應正確建立訂單()
    {
        // Arrange
        var orderService = new OrderService();
        var customer = Create<User>();
        var products = CreateMany<Product>(3);

        // Act
        var order = orderService.CreateOrder(customer, products);

        // Assert
        order.Should().NotBeNull();
        order.Customer.Should().Be(customer);
        order.Items.Should().HaveCount(3);
        order.TotalAmount.Should().BeGreaterThan(0);
        order.OrderDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
    }

    [Theory]
    [BogusAutoData]
    public void CreateOrder_使用_AutoData_整合_應能自動產生測試資料(
        User customer, 
        List<Product> products)
    {
        // Arrange - 資料由整合後的 AutoFixture 自動產生
        LogTestInfo($"Customer: {customer.Email}, Products: {products.Count}");

        // Assert - 驗證資料品質
        customer.Email.Should().Contain("@");
        products.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Name));
        
        // 這裡可以加入實際的業務邏輯測試
    }
}

/// <summary>
/// 實際應用情境測試範例
/// </summary>
public class RealWorldApplicationTests : TestBase
{
    public RealWorldApplicationTests() : base(seed: 789)
    {
    }

    [Fact]
    public void UserRegistration_使用真實感資料_應通過驗證()
    {
        // Arrange
        var userService = new UserService();
        var user = Generate<User>();

        // Act
        var validationResult = userService.ValidateUser(user);

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.Errors.Should().BeEmpty();

        // 驗證 Bogus 產生的資料格式正確
        user.Email.Should().MatchRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        user.FirstName.Should().NotBeNullOrWhiteSpace();
        user.LastName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CompanyAnalysis_使用完整測試情境_應正確統計()
    {
        // Arrange
        var analysisService = new CompanyAnalysisService();
        var scenario = Factory.CreateTestScenario();

        // Act
        var analysis = analysisService.AnalyzeCompany(scenario.Company, scenario.Orders);

        // Assert
        analysis.Should().NotBeNull();
        analysis.TotalEmployees.Should().Be(scenario.Company.Employees.Count);
        analysis.TotalOrders.Should().Be(scenario.Orders.Count);
        analysis.TotalRevenue.Should().Be(scenario.Orders.Sum(o => o.TotalAmount));
        analysis.AverageOrderValue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BulkDataProcessing_大量資料處理_應正確執行()
    {
        // Arrange
        var dataProcessor = new BulkDataProcessor();
        var users = Factory.CreateMany<User>(100);
        var orders = Factory.CreateMany<Order>(500);

        // Act
        var result = dataProcessor.ProcessUserOrders(users, orders);

        // Assert
        result.Should().NotBeNull();
        result.ProcessedUsers.Should().HaveCount(100);
        result.ProcessedOrders.Should().HaveCount(500);
        result.ProcessingErrors.Should().BeEmpty();
    }
}
```

## 今日小結

這套整合讓 AutoFixture 負責建立物件圖，Bogus 負責需要業務語意的欄位。兩者的責任分開後，自訂規則比較容易追蹤。

### 重點整理

1. **整合的價值**：
   - AutoFixture 提供方便的匿名測試資料產生
   - Bogus 提供真實感的語意化資料
   - 整合後可以同時享受兩者的優點

2. **技術整合方式**：
   - 透過 `ISpecimenBuilder` 將 Bogus 整合到 AutoFixture
   - 針對特定屬性（如 Email、Phone）使用 Bogus 產生器
   - 建立統一的測試資料工廠讓使用更簡單

3. **實戰技巧**：
   - 適當的 Seed 管理確保測試行為穩定性
   - 後處理機制確保資料邏輯一致性
   - 適當的快取機制提升效能
   - 了解整合工具的可重現性限制

### 實際好處

**開發效率提升**：

- 減少測試資料準備時間
- 自動處理複雜物件關聯
- 統一的 API 降低學習成本

**測試品質改善**：

- 真實感資料提高測試可信度
- 穩定的測試行為
- 更好的邊界值測試涵蓋

**維護性增強**：

- 集中管理測試資料產生邏輯
- 容易擴展和客製化
- 清楚的介面設計

### 使用建議

**什麼時候用整合方案**：

- 需要大量測試資料的專案
- 對資料真實感有要求的測試
- 團隊希望統一測試資料產生方式

**好的做法**：

- 為常用的業務實體建立專用的 SpecimenBuilder
- 使用 Seed 確保測試行為穩定性
- 適當使用快取提升大量資料產生的效能
- 建立測試基底類別統一資料產生邏輯
- 理解整合工具的技術限制

**要注意的**：

- 避免過度設計，保持簡單實用
- 定期檢查產生的資料品質

這套整合方式讓 AutoFixture 負責物件結構，Bogus 負責有語意的欄位。兩邊的責任切清楚後，後續要替換規則或加入新模型都比較容易。

### 效能測試與監控

在實際專案中，測試資料的產生效能也是需要考慮的重要因素。我們可以透過效能測試來監控整合方案的表現：

```csharp
using AutoFixtureBogusMix.Core.Models;
using AutoFixtureBogusMix.Core.TestData.Factories;
using AwesomeAssertions;
using System.Diagnostics;
using Xunit;

/// <summary>
/// 效能測試
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void 大量資料產生_效能測試()
    {
        // Arrange
        var factory = new IntegratedTestDataFactory(seed: 123);
        // 降低測試數量，因為 User 物件包含複雜的循環參考結構
        const int dataCount = 100; // 從 1000 降到 100

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var users = factory.CreateMany<User>(dataCount);
        stopwatch.Stop();

        var cacheStopwatch = Stopwatch.StartNew();
        var cachedUsers = Enumerable.Range(0, dataCount)
            .Select(_ => factory.GetCached<User>())
            .ToList();
        cacheStopwatch.Stop();

        // Output results
        _output.WriteLine($"建立 {dataCount} 個 User 物件耗時: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"使用快取建立 {dataCount} 個 User 物件耗時: {cacheStopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"平均每個 User 物件耗時: {(double)stopwatch.ElapsedMilliseconds / dataCount:F2} ms");

        // Assert
        users.Should().HaveCount(dataCount);
        cachedUsers.Should().HaveCount(dataCount);

        // 快取版本通常會更快（在大量資料產生時）
        cacheStopwatch.ElapsedMilliseconds.Should().BeLessThan(stopwatch.ElapsedMilliseconds);

        // 調整效能期望值 - User 物件有複雜結構，每個可能需要 20-50ms
        // 100 個 User 物件應該在 10 秒內完成（考慮到循環參考的複雜度）
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10秒內完成
        
        // 平均每個物件不應超過 100ms
        var averageTimePerUser = (double)stopwatch.ElapsedMilliseconds / dataCount;
        averageTimePerUser.Should().BeLessThan(100);
    }

    [Fact]
    public void 簡單物件_大量資料產生_效能測試()
    {
        // Arrange
        var factory = new IntegratedTestDataFactory(seed: 123);
        const int dataCount = 1000;

        // Act & Measure - 使用簡單的 Address 物件而非複雜的 User
        var stopwatch = Stopwatch.StartNew();
        var addresses = factory.CreateMany<Address>(dataCount);
        stopwatch.Stop();

        // Output results
        _output.WriteLine($"建立 {dataCount} 個 Address 物件耗時: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"平均每個 Address 物件耗時: {(double)stopwatch.ElapsedMilliseconds / dataCount:F3} ms");

        // Assert
        addresses.Should().HaveCount(dataCount);

        // Address 是簡單物件，應該能在 5 秒內完成 1000 個
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
        
        // 平均每個 Address 不應超過 5ms
        var averageTimePerAddress = (double)stopwatch.ElapsedMilliseconds / dataCount;
        averageTimePerAddress.Should().BeLessThan(5);
    }

    [Fact]
    public void 複雜物件結構_產生效能測試()
    {
        // Arrange
        var factory = new IntegratedTestDataFactory(seed: 456);
        const int scenarioCount = 100;

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var scenarios = Enumerable.Range(0, scenarioCount)
            .Select(_ => factory.CreateTestScenario())
            .ToList();
        stopwatch.Stop();

        // Output results
        _output.WriteLine($"建立 {scenarioCount} 個完整測試情境耗時: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"平均每個情境耗時: {(double)stopwatch.ElapsedMilliseconds / scenarioCount:F2} ms");

        // Assert
        scenarios.Should().HaveCount(scenarioCount);
        scenarios.Should().AllSatisfy(scenario =>
        {
            scenario.Company.Should().NotBeNull();
            scenario.Users.Should().NotBeEmpty();
            scenario.Orders.Should().NotBeEmpty();
        });

        // 效能驗證
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // 10秒內完成
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    public void 不同數量_資料產生效能比較(int count)
    {
        // Arrange
        var factory = new IntegratedTestDataFactory();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var products = factory.CreateMany<Product>(count);
        stopwatch.Stop();

        // Output
        _output.WriteLine($"產生 {count} 個 Product 耗時: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"平均每個 Product 耗時: {(double)stopwatch.ElapsedMilliseconds / count:F3} ms");

        // Assert
        products.Should().HaveCount(count);

        // 線性效能要求：平均每個物件不應超過 10ms
        var averageTimePerItem = (double)stopwatch.ElapsedMilliseconds / count;
        averageTimePerItem.Should().BeLessThan(10);
    }
}
```

執行結果：

```text
// 簡單物件_大量資料產生_效能測試
建立 1000 個 Address 物件耗時: 166 ms
平均每個 Address 物件耗時: 0.166 ms

// 複雜物件結構_產生效能測試
建立 100 個完整測試情境耗時: 4824 ms
平均每個情境耗時: 48.24 ms

// 大量資料產生_效能測試
建立 100 個 User 物件耗時: 1451 ms
使用快取建立 100 個 User 物件耗時: 6 ms
平均每個 User 物件耗時: 14.51 ms
```

### 效能考量與最佳化

在實際使用中，要注意複雜物件的效能影響：

**複雜物件效能特性**：

- `User` 物件包含循環參考（Company ↔ User），每個物件建立時間約 10-20ms
- 完整測試情境（包含 Company + 5個 User + 10個 Order）建立時間約 40-60ms
- 大量資料測試時建議使用簡單物件（如 `Address`），建立時間約 0.1-0.3ms
- 效能測試應該分層進行：
  - 簡單物件：測試 1000+ 個
  - 複雜物件：測試 50-100 個
  - 完整情境：測試 10-50 個

**效能最佳化建議**：

- 使用 Seed 確保可重現性
- 適當使用快取（`GetCached<T>()`）
- 調整 `RepeatCount` 控制集合大小
- 監控測試執行時間，設定合理的期望值

**實際測試結果參考**：

根據我們的測試，在一般的開發機器上：

- 100 個 User 物件：約 1451ms（平均 14.51ms/個）
- 1000 個 Address 物件：約 166ms（平均 0.166ms/個）
- 100 個完整測試情境：約 4824ms（平均 48.24ms/個）
- 快取機制可以大幅提升重複存取的效能（從 1451ms 降到 6ms）

## 相關參考資料

- [AutoFixture 官方文件](https://github.com/AutoFixture/AutoFixture)
- [Bogus 官方文件](https://github.com/bchavez/Bogus)
- [AutoBogus 整合套件](https://github.com/nickdodd79/AutoBogus)
- [測試資料管理最佳實務](https://martinfowler.com/articles/test-data-builders.html)

明天將會介紹 Microsoft.Bcl.TimeProvider，看看如何在測試中處理時間相依性問題。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day15>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十五天。明天會介紹 Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime。**
