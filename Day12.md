---
day: 12
title: "Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用"
sample: samples/day12
target_framework: net10.0
packages:
  - AutoFixture.Xunit3
  - AwesomeAssertions
  - CsvHelper
  - Microsoft.Testing.Extensions.TrxReport
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用

## 前言

前一天完成了 AutoFixture 資料產生策略的自訂。這一篇改用 AutoData 屬性家族整合 xUnit 與 AutoFixture，讓參數化測試自動取得資料。

## 本日重點

- **AutoData 屬性家族**：AutoData、InlineAutoData、MemberAutoData、CompositeAutoData
- **參數注入策略**：複雜物件的自動建構、介面參數的自動模擬、泛型參數處理
- **外部測試資料整合**：CSV/JSON 檔案讀取、Build Action 設定、工具整合
- **資料來源設計模式**：階層式組織、可重用資料集、版本控制
- **與 Awesome Assertions 協作**：改善測試可讀性與維護性

## AutoData 屬性家族概述

AutoData 屬性家族是 AutoFixture.Xunit3 套件提供的功能，它們將 AutoFixture 的資料產生能力與 xUnit 的參數化測試整合。

> **NuGet Package**: AutoFixture.Xunit3  
> **套件連結**：[https://www.nuget.org/packages/AutoFixture.Xunit3/](https://www.nuget.org/packages/AutoFixture.Xunit3/)

安裝 NuGet Package：

```bash
dotnet add package AutoFixture.Xunit3
```

> **參考文件**：
>
> - [AutoFixture Cheat Sheet - AutoData Theories](https://github.com/AutoFixture/AutoFixture/wiki/Cheat-Sheet#autodata-theories)
> - [AutoFixture.xUnit3 - GitHub Repository](https://github.com/AutoFixture/AutoFixture.xUnit3)

### AutoData 屬性家族成員

```csharp
// AutoData：完全自動產生參數
[Theory]
[AutoData]
public void Test_AutoData(Person person, string message, int count)
{
    // 所有參數都由 AutoFixture 自動產生
}

// InlineAutoData：混合固定值與自動產生
[Theory]
[InlineAutoData("張三", 25)]
[InlineAutoData("李四", 30)]
public void Test_InlineAutoData(string name, int age, Person person)
{
    // name 和 age 使用固定值，person 由 AutoFixture 產生
}

// MemberAutoData：結合外部資料來源
[Theory]
[MemberAutoData(nameof(GetTestData))]
public void Test_MemberAutoData(string category, Product product)
{
    // category 來自 GetTestData，product 由 AutoFixture 產生
}

// CompositeAutoData：多重資料來源整合
[Theory]
[CompositeAutoData(typeof(CustomAutoData), typeof(DomainAutoData))]
public void Test_CompositeAutoData(Customer customer, Order order)
{
    // 結合多個自訂 AutoData 來源
}
```

## AutoData：完全自動產生參數

`AutoData` 是最基礎的屬性，它會自動為測試方法的所有參數產生測試資料。

### 基本使用方式

```csharp
public class Person
{
    public Guid Id { get; set; }
    
    [StringLength(10)]
    public string Name { get; set; } = string.Empty;
    
    [Range(18, 80)]
    public int Age { get; set; }
    
    public string Email { get; set; } = string.Empty;
    
    public DateTime CreateTime { get; set; }
}

[Theory]
[AutoData]
public void AutoData_應能自動產生所有參數(Person person, string message, int count)
{
    // Arrange & Act - 參數已由 AutoData 自動產生

    // Assert
    person.Should().NotBeNull();
    person.Id.Should().NotBe(Guid.Empty);
    person.Name.Should().HaveLength(10); // 遵循 StringLength 限制
    person.Age.Should().BeInRange(18, 80); // 遵循 Range 限制
    message.Should().NotBeNullOrEmpty();
    count.Should().NotBe(0);
}
```

### 控制 AutoData 的行為

方法參數上的 DataAnnotation 可以約束 AutoData 的行為：

```csharp
[Theory]
[AutoData]
public void AutoData_透過DataAnnotation約束參數(
    [StringLength(5, MinimumLength = 3)] string shortName,
    [Range(1, 100)] int percentage,
    Person person)
{
    // Arrange & Act - 已由 AutoData 根據 DataAnnotation 產生

    // Assert
    shortName.Length.Should().BeInRange(3, 5);
    percentage.Should().BeInRange(1, 100);
    person.Should().NotBeNull();
}
```

## InlineAutoData：混合固定值與自動產生

`InlineAutoData` 結合 `InlineData` 的固定值與 `AutoData` 的自動產生功能：部分參數可指定固定值，其餘參數交給 AutoData 產生。

### 基本語法與應用

```csharp
[Theory]
[InlineAutoData("VIP客戶", 1000)]
[InlineAutoData("一般客戶", 500)]
[InlineAutoData("新客戶", 100)]
public void InlineAutoData_混合固定值與自動產生(string customerType, decimal creditLimit, Person person)
{
    // Arrange
    // customerType 和 creditLimit 使用固定值
    // person 由 AutoFixture 自動產生

    // Act
    var customer = new Customer
    {
        Person = person,
        Type = customerType,
        CreditLimit = creditLimit
    };

    // Assert
    customer.Person.Should().NotBeNull();
    customer.Type.Should().Be(customerType);
    customer.CreditLimit.Should().Be(creditLimit);

    // 所有測試資料的信用額度都應該大於等於 100（最小值）
    customer.CreditLimit.Should().BeGreaterThanOrEqualTo(100);
}

public class Customer
{
    public Person Person { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    
    public bool CanPlaceOrder(decimal orderAmount)
    {
        return orderAmount <= CreditLimit;
    }
}
```

### InlineAutoData 的參數順序

InlineAutoData 中固定值的順序必須與方法參數順序一致：

```csharp
[Theory]
[InlineAutoData("產品A", 100)]  // 依序對應 name, price
[InlineAutoData("產品B", 200)]
public void InlineAutoData_參數順序一致性(
    string name,        // 對應第1個固定值
    decimal price,      // 對應第2個固定值
    Product product)    // 由 AutoFixture 產生
{
    // Arrange & Act - 參數已設定

    // Assert
    name.Should().StartWith("產品");
    price.Should().BePositive();
    product.Should().NotBeNull();
}

public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### InlineAutoData 與 DataAnnotation 的協作

```csharp
[Theory]
[InlineAutoData("電子產品")]
[InlineAutoData("服飾用品")]
[InlineAutoData("生活用品")]
public void InlineAutoData_與DataAnnotation協作(
    string category,
    [Range(1, 1000)] decimal price,
    [StringLength(50)] string description,
    Product product)
{
    // Arrange
    product.Name = $"{category}-{product.Name}";
    product.Price = price;
    product.Description = description;

    // Act
    var isValid = ValidateProduct(product);

    // Assert
    isValid.Should().BeTrue();
    product.Name.Should().StartWith(category);
    product.Price.Should().BeInRange(1, 1000);
    product.Description.Length.Should().BeLessThanOrEqualTo(50);
}

private static bool ValidateProduct(Product product)
{
    return !string.IsNullOrEmpty(product.Name) 
        && product.Price > 0 
        && !string.IsNullOrEmpty(product.Description);
}
```

### InlineAutoData 的限制

`InlineAutoData` 的每個固定值都是 C# 的 attribute argument，必須是**編譯期常數運算式**（或 `typeof`、attribute 參數型別的一維陣列）。這裡有兩個很容易搞混的地方：能不能用「運算式」跟能不能用「某個型別」，其實是兩件不同的事。

```csharp
// O 正確：整數常數字面值
[InlineAutoData("VIP", 100000)]
[InlineAutoData("Premium", 50000)]

// O 正確：編譯期常數運算式（100 * 1000 在編譯期就會算成 100000）
[InlineAutoData("VIP", 100 * 1000)]

// O 正確：參照 const int 常數（能不能用，取決於型別與是否為常數，而不是「不能用 const」）
private const int VipCreditLimit = 100000;
[InlineAutoData("VIP", VipCreditLimit)]

// X 錯誤：decimal 不是合法的 attribute 參數型別（CS0182）
//         連 const decimal 也不行——問題不在 const，而在 decimal 這個型別本身
[InlineAutoData("VIP", 100000m)]

// X 錯誤：執行期方法呼叫不是常數運算式（CS0182）
[InlineAutoData("VIP", CalculateVipCreditLimit())]
```

這裡有個常見的疑問：測試方法的參數明明是 `decimal creditLimit`，為什麼 attribute 裡卻寫 `int` 的 `100000`？因為 attribute argument 本身必須是合法的常數（int、double、string 等都可以，但 `decimal` 不行），xUnit 會在呼叫測試方法時，再把這個 int 轉成參數宣告的 `decimal`。也就是說：合法與否看的是「寫進 attribute 的那個值」，型別轉換則是執行期由 xUnit 處理。

如果需要 `decimal`、變數，或是執行期才算得出來的資料，應該改用 `MemberAutoData` 搭配靜態方法：

```csharp
public static IEnumerable<object[]> CustomerData()
{
    var vipCreditLimit = CalculateVipCreditLimit(); // 可以使用變數和計算
    yield return new object[] { "VIP", vipCreditLimit };
    yield return new object[] { "Premium", 50000m };
}

[Theory]
[MemberAutoData(nameof(CustomerData))]
public void CustomerTest(string type, decimal creditLimit, Customer customer)
{
    // 測試邏輯
}
```

## MemberAutoData：結合外部資料來源

`MemberAutoData` 可以從類別的方法、屬性或欄位取得測試資料，再跟 AutoFixture 的自動產生功能結合。

> **行為變更（Xunit2 → Xunit3）**：AutoFixture.Xunit2 的 `MemberAutoData` 有一個長期存在的 bug——不管資料來源有幾列，實際只會執行第一列（它用 `CompositeDataAttribute` 把 MemberData 與 AutoData 逐列配對，AutoData 只產生一列，配對結果永遠只剩一列）。AutoFixture.Xunit3 修正了這個行為，資料來源的每一列都會展開執行。以本篇範例專案為例，遷移後測試案例數從 41 增加到 64，多出來的 23 個就是 v2 時代從未真正執行過的資料列。

### 使用靜態方法提供測試資料

```csharp
public class MemberAutoDataTests
{
    public static IEnumerable<object[]> GetProductCategories()
    {
        yield return new object[] { "3C產品", "TECH" };
        yield return new object[] { "服飾配件", "FASHION" };
        yield return new object[] { "居家生活", "HOME" };
        yield return new object[] { "運動健身", "SPORTS" };
    }

    [Theory]
    [MemberAutoData(nameof(GetProductCategories))]
    public void MemberAutoData_使用靜態方法資料(
        string categoryName,
        string categoryCode, 
        Product product)
    {
        // Arrange
        product.Name = $"[{categoryCode}] {product.Name}";

        // Act
        var categorizedProduct = new CategorizedProduct
        {
            Product = product,
            CategoryName = categoryName,
            CategoryCode = categoryCode
        };

        // Assert
        categorizedProduct.Product.Should().NotBeNull();
        categorizedProduct.CategoryName.Should().Be(categoryName);
        categorizedProduct.CategoryCode.Should().Be(categoryCode);
        categorizedProduct.Product.Name.Should().StartWith($"[{categoryCode}]");
    }
}

public class CategorizedProduct
{
    public Product Product { get; set; } = new();
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
}
```

### 使用靜態屬性 (static property) 提供測試資料

```csharp
public class MemberAutoDataTests
{
    public static IEnumerable<object[]> StatusTransitions => new[]
    {
        new object[] { OrderStatus.Created, OrderStatus.Confirmed },
        new object[] { OrderStatus.Confirmed, OrderStatus.Shipped },
        new object[] { OrderStatus.Shipped, OrderStatus.Delivered },
        new object[] { OrderStatus.Delivered, OrderStatus.Completed }
    };

    [Theory]
    [MemberAutoData(nameof(StatusTransitions))]
    public void MemberAutoData_使用屬性資料(
        OrderStatus fromStatus,
        OrderStatus toStatus,
        Order order)
    {
        // Arrange
        order.Status = fromStatus;

        // Act
        var canTransition = order.CanTransitionTo(toStatus);

        // Assert
        canTransition.Should().BeTrue();
        order.Status.Should().Be(fromStatus);
    }
}

public enum OrderStatus
{
    Created,
    Confirmed,
    Shipped,
    Delivered,
    Completed,
    Cancelled
}

public class Order
{
    public OrderStatus Status { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    public bool CanTransitionTo(OrderStatus newStatus)
    {
        return (this.Status, newStatus) switch
        {
            (OrderStatus.Created, OrderStatus.Confirmed) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.Delivered, OrderStatus.Completed) => true,
            _ => false
        };
    }
}
```

## 外部測試資料整合

實際專案常把測試資料放在外部檔案。以下示範如何整合 CSV 與 JSON。

### 測試專案中的檔案管理

先設定測試資料檔案：

1. **檔案放置位置**：建議在測試專案中建立 `TestData` 資料夾
2. **Build Action 設定**：設定為 `Content`
3. **Copy to Output Directory**：設定為 `Copy always` 或 `Copy if newer`

### 專案檔案設定範例

在測試專案的 `.csproj` 檔案中（套件版本集中在 `Directory.Packages.props`（CPM）管理，`.csproj` 不寫版本號）：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="TestData\products.csv">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
    <Content Include="TestData\customers.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="AutoFixture.Xunit3" />
    <PackageReference Include="AwesomeAssertions" />
    <PackageReference Include="CsvHelper" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
    <PackageReference Include="xunit.v3.mtp-v2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AutoData.Core\AutoData.Core.csproj" />
  </ItemGroup>

</Project>
```

### CSV 檔案整合應用

#### TestData/products.csv

```csv
ProductId,Name,Category,Price,IsAvailable
1,"iPhone 15","3C產品",35900,true
2,"MacBook Pro","3C產品",89900,true
3,"AirPods Pro","3C產品",7490,false
4,"Nike Air Max","運動用品",4200,true
5,"Adidas Ultraboost","運動用品",5800,true
```

#### CSV 讀取與整合

```csharp
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

public class ExternalDataIntegrationTests
{
    public static IEnumerable<object[]> GetProductsFromCsv()
    {
        var testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "products.csv");

        using var reader = new StringReader(File.ReadAllText(testDataPath));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

        var records = csv.GetRecords<ProductCsvRecord>().ToList();

        foreach (var record in records)
        {
            yield return new object[]
            {
                record.ProductId,
                record.Name,
                record.Category,
                record.Price,
                record.IsAvailable
            };
        }
    }

    [Theory]
    [MemberAutoData(nameof(GetProductsFromCsv))]
    public void CSV整合測試_產品資料驗證(
        int productId,
        string name,
        string category,
        decimal price,
        bool isAvailable,
        Order order) // 由 AutoFixture 自動產生
    {
        // Arrange
        var product = new Product
        {
            Name = name,
            Price = price,
            IsAvailable = isAvailable
        };

        order.Amount = price;

        // Act
        var orderItem = new OrderItem
        {
            ProductId = productId,
            Product = product,
            Quantity = 1
        };

        // Assert
        orderItem.ProductId.Should().Be(productId);
        orderItem.Product.Name.Should().Be(name);
        orderItem.Product.Price.Should().Be(price);

        // 驗證產品類別
        category.Should().NotBeNullOrEmpty("產品應該有類別");

        // 從 CSV 讀取的產品價格都應該是正數
        orderItem.Product.Price.Should().BePositive();
    }
}

public class OrderItem
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
}

public class ProductCsvRecord
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}
```

### JSON 檔案整合應用

#### TestData/customers.json

```json
[
  {
    "customerId": 1001,
    "name": "張三",
    "email": "zhang.san@example.com",
    "level": "VIP",
    "creditLimit": 50000
  },
  {
    "customerId": 1002,
    "name": "李四",
    "email": "li.si@example.com",
    "level": "Premium",
    "creditLimit": 30000
  },
  {
    "customerId": 1003,
    "name": "王五",
    "email": "wang.wu@example.com",
    "level": "Regular",
    "creditLimit": 10000
  }
]
```

#### JSON 讀取與整合

```csharp
using System.Text.Json;

public class ExternalDataIntegrationTests
{
    public static IEnumerable<object[]> GetCustomersFromJson()
    {
        var testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "customers.json");
        var jsonContent = File.ReadAllText(testDataPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var customers = JsonSerializer.Deserialize<List<CustomerJsonRecord>>(jsonContent, options);

        foreach (var customer in customers ?? new List<CustomerJsonRecord>())
        {
            yield return new object[]
            {
                customer.CustomerId,
                customer.Name,
                customer.Email,
                customer.Level,
                customer.CreditLimit
            };
        }
    }

    [Theory]
    [MemberAutoData(nameof(GetCustomersFromJson))]
    public void JSON整合測試_客戶資料驗證(
        int customerId,
        string name,
        string email,
        string level,
        decimal creditLimit,
        Order order) // 由 AutoFixture 自動產生
    {
        // Arrange
        var customer = new Customer
        {
            Person = new Person { Name = name, Email = email },
            Type = level,
            CreditLimit = creditLimit
        };

        order.Amount = 15000;
        var canPlaceOrder = customer.CanPlaceOrder(order.Amount);

        // Assert
        customer.Person.Name.Should().Be(name);
        customer.Person.Email.Should().Be(email);
        customer.Type.Should().Be(level);
        customer.CreditLimit.Should().Be(creditLimit);

        // 驗證客戶 ID
        customerId.Should().BePositive("客戶 ID 應該是正數");

        // 驗證客戶等級
        customer.Type.Should().BeOneOf("VIP", "Premium", "Regular");

        // 根據信用額度驗證下單能力
        order.Amount.Should().Be(15000);

        if (order.Amount <= customer.CreditLimit)
        {
            canPlaceOrder.Should().BeTrue();
        }
        else
        {
            canPlaceOrder.Should().BeFalse();
        }
    }
}

public class CustomerJsonRecord
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
}
```

## CompositeAutoData：多重資料來源整合

`CompositeAutoData` 允許我們組合多個自訂的 AutoData 設定，建立複雜的測試資料產生策略。

### 自訂 AutoData 屬性

```csharp
public class DomainAutoDataAttribute : AutoDataAttribute
{
    public DomainAutoDataAttribute() : base(() => CreateFixture())
    {
    }

    internal static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        
        // 設定 Person 的屬性
        fixture.Customize<Person>(composer =>
            composer.With(p => p.Name, () => $"測試使用者{Random.Shared.Next(1, 999)}")
                   .With(p => p.Age, () => Random.Shared.Next(18, 65))
                   .With(p => p.Email, () => $"user{Random.Shared.Next(1, 999)}@example.com")
                   .With(p => p.CreateTime, DateTime.Now));
        
        // 設定 Product 的屬性
        fixture.Customize<Product>(composer =>
            composer.With(p => p.Name, () => $"產品{Random.Shared.Next(100, 999)}")
                   .With(p => p.Price, () => Random.Shared.Next(100, 10000))
                   .With(p => p.IsAvailable, true)
                   .With(p => p.Description, () => $"這是測試產品的描述內容{Random.Shared.Next(1, 99)}"));
                   
        return fixture;
    }
}

public class BusinessAutoDataAttribute : AutoDataAttribute
{
    public BusinessAutoDataAttribute() : base(() => CreateFixture())
    {
    }

    internal static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        
        // 設定 Order 的屬性
        fixture.Customize<Order>(composer =>
            composer.With(o => o.Status, OrderStatus.Created)
                   .With(o => o.Amount, () => Random.Shared.Next(1000, 50000))
                   .With(o => o.OrderNumber, () => $"ORD{DateTime.Now:yyyyMMdd}{Random.Shared.Next(1000, 9999)}"));
        
        return fixture;
    }
}
```

### CompositeAutoData 屬性實作

```csharp
public class CompositeAutoDataAttribute : AutoDataAttribute
{
    public CompositeAutoDataAttribute(params Type[] autoDataAttributeTypes) : base(() => CreateFixture(autoDataAttributeTypes))
    {
    }

    private static IFixture CreateFixture(Type[] autoDataAttributeTypes)
    {
        var fixture = new Fixture();

        foreach (var attributeType in autoDataAttributeTypes)
        {
            // 取得每個 AutoData 屬性類型的 CreateFixture 方法
            var createFixtureMethod = attributeType.GetMethod("CreateFixture", 
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            
            if (createFixtureMethod != null)
            {
                // 執行 CreateFixture 方法並取得設定
                var sourceFixture = (IFixture)createFixtureMethod.Invoke(null, null)!;
                
                // 將來源 Fixture 的自訂設定複製到目標 Fixture
                foreach (var customization in sourceFixture.Customizations)
                {
                    fixture.Customizations.Add(customization);
                }
            }
        }

        return fixture;
    }
}
```

### CompositeAutoData 的使用

```csharp
[Theory]
[CompositeAutoData(typeof(DomainAutoDataAttribute), typeof(BusinessAutoDataAttribute))]
public void CompositeAutoData_整合多重資料來源(
    Person person,
    Product product,
    Order order)
{
    // Arrange
    // 所有物件都已經根據各自的 AutoData 設定產生

    // Act
    var orderItem = new OrderItem
    {
        ProductId = Random.Shared.Next(1, 1000),
        Product = product,
        Quantity = 2
    };

    // Assert
    // 驗證 DomainAutoData 的設定
    person.Age.Should().BeInRange(18, 64);
    person.Email.Should().EndWith("@example.com");
    person.Name.Should().StartWith("測試使用者");
    
    product.Price.Should().BeInRange(100, 9999);
    product.IsAvailable.Should().BeTrue();
    product.Name.Should().StartWith("產品");
    
    // 驗證 BusinessAutoData 的設定
    order.Status.Should().Be(OrderStatus.Created);
    order.Amount.Should().BeInRange(1000, 49999);
    order.OrderNumber.Should().StartWith("ORD");
    
    orderItem.Should().NotBeNull();
    orderItem.ProductId.Should().BePositive();
    orderItem.Quantity.Should().Be(2);
}
```

## 控制物件集合產生的數量

AutoData 預設產生 3 筆集合元素，也可以用自訂屬性調整數量。

### CollectionSizeAttribute 實作

> **參考資料來源**：StackOverflow - [Collection size from attribute for Autofixture declarative autodata parameter](https://stackoverflow.com/questions/67713351/collection-size-from-attribute-for-autofixture-declarative-autodata-parameter/67713352#67713352)

```csharp
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit3;
using System.Reflection;

public class CollectionSizeAttribute : CustomizeAttribute
{
    private readonly int _size;

    public CollectionSizeAttribute(int size)
    {
        _size = size;
    }

    public override ICustomization GetCustomization(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var objectType = parameter.ParameterType.GetGenericArguments()[0];

        var isTypeCompatible = parameter.ParameterType.IsGenericType &&
                              parameter.ParameterType.GetGenericTypeDefinition()
                                  .MakeGenericType(objectType)
                                  .IsAssignableFrom(typeof(List<>).MakeGenericType(objectType));

        if (!isTypeCompatible)
        {
            throw new InvalidOperationException(
                $"{nameof(CollectionSizeAttribute)} 指定的型別與 List 不相容: {parameter.ParameterType} {parameter.Name}");
        }

        var customizationType = typeof(CollectionSizeCustomization<>).MakeGenericType(objectType);
        return (ICustomization)Activator.CreateInstance(customizationType, parameter, _size)!;
    }

    private class CollectionSizeCustomization<T> : ICustomization
    {
        private readonly ParameterInfo _parameter;
        private readonly int _repeatCount;

        public CollectionSizeCustomization(ParameterInfo parameter, int repeatCount)
        {
            _parameter = parameter;
            _repeatCount = repeatCount;
        }

        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(
                new FilteringSpecimenBuilder(
                    new FixedBuilder(fixture.CreateMany<T>(_repeatCount).ToList()),
                    new EqualRequestSpecification(_parameter)));
        }
    }
}
```

### CollectionSizeAttribute 的使用

```csharp
[Theory]
[AutoData]
public void CollectionSize_控制自動產生集合大小(
    [CollectionSize(5)] List<Product> products,
    [CollectionSize(3)] List<Order> orders,
    Customer customer)
{
    // Arrange & Act - 集合已根據 CollectionSize 產生

    // Assert
    products.Should().HaveCount(5);
    orders.Should().HaveCount(3);
    customer.Should().NotBeNull();

    // 驗證每個 Product 都有合理的值
    products.Should().AllSatisfy(product =>
    {
        product.Name.Should().NotBeNullOrEmpty();
        product.Price.Should().BeGreaterThanOrEqualTo(0);
    });

    // 驗證每個 Order 都有合理的值
    orders.Should().AllSatisfy(order =>
    {
        order.OrderNumber.Should().NotBeNullOrEmpty();
        order.Amount.Should().BeGreaterThanOrEqualTo(0);
    });
}
```

## 資料來源設計模式

### 階層式資料組織策略

建立分層的測試資料組織結構：

```csharp
namespace AutoData.Tests.DataSources;

/// <summary>
/// 測試資料來源基底類別
/// </summary>
public abstract class BaseTestData
{
    protected static string GetTestDataPath(string fileName)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
    }
}

/// <summary>
/// 產品測試資料來源
/// </summary>
public class ProductTestDataSource : BaseTestData
{
    public static IEnumerable<object[]> BasicProducts()
    {
        yield return new object[] { "iPhone", 35900m, true };
        yield return new object[] { "MacBook", 89900m, true };
        yield return new object[] { "iPad", 18900m, false };
    }
    
    public static IEnumerable<object[]> ElectronicsFromCsv()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "products.csv");
        if (!File.Exists(testDataPath))
        {
            // 如果在輸出目錄找不到，嘗試從專案目錄找
            testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestData", "products.csv");
        }

        if (!File.Exists(testDataPath))
        {
            // 如果還是找不到，回傳預設資料
            yield return new object[] { 1, "iPhone 15", "3C產品", 35900m, true };
            yield return new object[] { 2, "MacBook Pro", "3C產品", 89900m, true };
            yield break;
        }

        var csvContent = File.ReadAllText(testDataPath);
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // 跳過標題行
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            if (values.Length >= 5 && values[2].Trim('"') == "3C產品")
            {
                yield return new object[] 
                { 
                    int.Parse(values[0]),
                    values[1].Trim('"'),
                    values[2].Trim('"'),
                    decimal.Parse(values[3]),
                    bool.Parse(values[4])
                };
            }
        }
    }
}

/// <summary>
/// 客戶測試資料來源
/// </summary>
public class CustomerTestDataSource : BaseTestData
{
    public static IEnumerable<object[]> VipCustomers()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "customers.json");
        if (!File.Exists(testDataPath))
        {
            // 如果在輸出目錄找不到，嘗試從專案目錄找
            testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestData", "customers.json");
        }

        if (!File.Exists(testDataPath))
        {
            // 如果還是找不到，回傳預設資料
            yield return new object[] { 1001, "張三", "zhang.san@example.com", "VIP", 50000m };
            yield break;
        }

        var jsonContent = File.ReadAllText(testDataPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var customers = JsonSerializer.Deserialize<List<CustomerJsonRecord>>(jsonContent, options);
        
        foreach (var customer in customers ?? new List<CustomerJsonRecord>())
        {
            if (customer.Level == "VIP")
            {
                yield return new object[] 
                { 
                    customer.CustomerId, 
                    customer.Name, 
                    customer.Email, 
                    customer.Level, 
                    customer.CreditLimit 
                };
            }
        }
    }
}
```

### 可重用資料集的建立與維護

```csharp
/// <summary>
/// 可重用的測試資料集
/// </summary>
public class ReusableTestDataSets
{
    /// <summary>
    /// 可重用的產品分類資料
    /// </summary>
    public static class ProductCategories
    {
        public const string Electronics = "3C產品";
        public const string Fashion = "服飾配件";
        public const string Home = "居家生活";
        public const string Sports = "運動健身";
        
        public static IEnumerable<object[]> All()
        {
            yield return new object[] { Electronics, "TECH" };
            yield return new object[] { Fashion, "FASHION" };
            yield return new object[] { Home, "HOME" };
            yield return new object[] { Sports, "SPORTS" };
        }
    }
    
    /// <summary>
    /// 可重用的價格區間資料
    /// </summary>
    public static class PriceRanges
    {
        public static IEnumerable<object[]> Budget()
        {
            yield return new object[] { 100m, 500m };
            yield return new object[] { 500m, 1000m };
        }
        
        public static IEnumerable<object[]> Premium()
        {
            yield return new object[] { 5000m, 10000m };
            yield return new object[] { 10000m, 50000m };
        }
    }
}
```

### 使用可重用資料集

`MemberAutoData` 要在目前的測試類別裡找到靜態方法，所以得建一個代理方法：

```csharp
public class DataSourceDesignPatternTests
{
    // 代理方法讓 MemberAutoData 能找到正確的資料來源
    public static IEnumerable<object[]> All() => ReusableTestDataSets.ProductCategories.All();
    
    [Theory]
    [MemberAutoData(nameof(All))]
    public void 可重用資料集測試_產品分類驗證(
        string categoryName,
        string categoryCode,
        Product product)
    {
        // Arrange
        var categorizedProduct = new CategorizedProduct
        {
            Product = product,
            CategoryName = categoryName,
            CategoryCode = categoryCode
        };

        // Act
        var isValidCategory = ValidateCategory(categorizedProduct);

        // Assert
        isValidCategory.Should().BeTrue();
        ReusableTestDataSets.ProductCategories.All()
            .SelectMany(data => data)
            .Should().Contain(categoryName);
    }

    private static bool ValidateCategory(CategorizedProduct product)
    {
        var validCategories = ReusableTestDataSets.ProductCategories.All()
            .Select(data => data[0].ToString()).ToList();
        return validCategories.Contains(product.CategoryName);
    }
}
```

## 與 Awesome Assertions 協作

AutoData 屬性與 Awesome Assertions 的結合，可以寫出簡潔的測試程式碼。

### 測試驗證範例

```csharp
[Theory]
[InlineAutoData("VIP", 100000)]
[InlineAutoData("Premium", 50000)]
[InlineAutoData("Regular", 20000)]
public void AutoData與AwesomeAssertions協作_客戶等級與信用額度驗證(
    string customerLevel,
    decimal expectedCreditLimit,
    [Range(1000, 15000)] decimal orderAmount, // 確保所有等級客戶都能負擔
    Customer customer,
    Order order)
{
    // Arrange
    customer.Type = customerLevel;
    customer.CreditLimit = expectedCreditLimit;
    order.Amount = orderAmount;

    // Act
    var canPlaceOrder = customer.CanPlaceOrder(order.Amount);
    var discountRate = CalculateDiscount(customer.Type, order.Amount);

    // Assert - 使用 Awesome Assertions 語法
    customer.Type.Should().Be(customerLevel);
    customer.CreditLimit.Should().Be(expectedCreditLimit);
    customer.CreditLimit.Should().BePositive();
    
    order.Amount.Should().BeInRange(1000m, 15000m);
    
    // 驗證下單能力（訂單金額在所有客戶等級的信用額度內）
    canPlaceOrder.Should().BeTrue();
    
    // 驗證折扣率範圍
    discountRate.Should().BeInRange(0m, 0.3m);
}

[Theory]
[InlineAutoData("VIP", 0.15)]
[InlineAutoData("Premium", 0.10)]
[InlineAutoData("Regular", 0.05)]
public void AutoData與AwesomeAssertions協作_VIP客戶折扣驗證(
    string customerLevel,
    decimal expectedBaseDiscount,
    [Range(1000, 25000)] decimal orderAmount,
    Customer customer,
    Order order)
{
    // Arrange
    customer.Type = customerLevel;
    customer.CreditLimit = 100000m;
    order.Amount = orderAmount;

    // Act
    var discountRate = CalculateDiscount(customer.Type, order.Amount);

    // Assert
    discountRate.Should().BeGreaterThanOrEqualTo(expectedBaseDiscount);
    
    // VIP 客戶的特殊驗證
    discountRate.Should().Be(expectedBaseDiscount);
}

[Theory]
[InlineAutoData("VIP", 35000, 0.20)]
[InlineAutoData("Premium", 40000, 0.15)]
[InlineAutoData("Regular", 35000, 0.10)]
public void AutoData與AwesomeAssertions協作_大額訂單額外折扣驗證(
    string customerLevel,
    decimal largeOrderAmount,
    decimal expectedDiscountRate,
    Customer customer,
    Order order)
{
    // Arrange
    customer.Type = customerLevel;
    customer.CreditLimit = 100000m;
    order.Amount = largeOrderAmount;

    // Act
    var discountRate = CalculateDiscount(customer.Type, order.Amount);

    // Assert
    order.Amount.Should().BeGreaterThan(30000m);
    discountRate.Should().Be(expectedDiscountRate);
}

private static decimal CalculateDiscount(string customerType, decimal orderAmount)
{
    var baseDiscount = customerType switch
    {
        "VIP" => 0.15m,
        "Premium" => 0.10m,
        "Regular" => 0.05m,
        _ => 0m
    };
    
    // 大額訂單額外折扣
    var largeOrderBonus = orderAmount > 30000m ? 0.05m : 0m;
    
    return Math.Min(baseDiscount + largeOrderBonus, 0.3m); // 最高 30% 折扣
}
```

### 複雜業務情境的驗證

```csharp
[Theory]
[MemberAutoData(nameof(ElectronicsFromCsv))]
public void 複雜業務情境驗證_電子產品訂單處理(
    int productId,
    string productName,
    string category,
    decimal price,
    bool isAvailable,
    [CollectionSize(3)] List<Customer> customers,
    Order order)
{
    // Arrange
    var product = new Product
    {
        Name = productName,
        Price = price,
        IsAvailable = isAvailable
    };
    
    var vipCustomer = customers.First();
    vipCustomer.Type = "VIP";
    vipCustomer.CreditLimit = 200000m;

    // Act
    var orderResult = ProcessElectronicsOrder(vipCustomer, product, order, quantity: 2);

    // Assert - 使用 Awesome Assertions 驗證複雜結果
    productId.Should().BePositive(); // 使用 productId 參數
    category.Should().Be("3C產品"); // 使用 category 參數
    orderResult.Should().NotBeNull();
    orderResult.IsSuccess.Should().Be(isAvailable); // 訂單成敗取決於產品是否可售（CSV 第三列 AirPods Pro 為 false）
    
    // 驗證產品資訊
    orderResult.Product.Should().NotBeNull();
    orderResult.Product.Name.Should().Be(productName);
    orderResult.Product.Price.Should().Be(price);
    
    // 驗證客戶資訊
    orderResult.Customer.Should().NotBeNull();
    orderResult.Customer.Type.Should().Be("VIP");
    orderResult.Customer.CreditLimit.Should().Be(200000m);
    
    // 驗證訂單計算
    orderResult.TotalAmount.Should().Be(price * 2); // 數量 x 單價
    orderResult.DiscountAmount.Should().BeGreaterThan(0); // VIP 客戶應有折扣
    orderResult.FinalAmount.Should().BeLessThan(orderResult.TotalAmount);
    
    // 驗證集合資料
    customers.Should().HaveCount(3);
    customers.Should().AllSatisfy(customer =>
    {
        customer.Person.Should().NotBeNull();
        customer.Person.Name.Should().NotBeNullOrEmpty();
        customer.CreditLimit.Should().BePositive();
    });
}

[Theory]
[InlineAutoData("3C產品", 15000, true, 7)]
[InlineAutoData("3C產品", 25000, true, 7)] // 實作為 Random.Shared.Next(3, 8)，上界一律 7，與價格高低無關
public void 複雜業務情境驗證_高價3C產品需要審核(
    string category,
    decimal price,
    bool expectedRequiresApproval,
    int maxDeliveryDays,
    [CollectionSize(1)] List<Customer> customers,
    Order order)
{
    // Arrange
    var product = new Product
    {
        Name = $"{category}產品",
        Price = price,
        IsAvailable = true
    };
    
    var vipCustomer = customers.First();
    vipCustomer.Type = "VIP";
    vipCustomer.CreditLimit = 200000m;

    // Act
    var orderResult = ProcessElectronicsOrder(vipCustomer, product, order, quantity: 1);

    // Assert
    category.Should().Be("3C產品");
    price.Should().BeGreaterThan(10000m);
    orderResult.RequiresApproval.Should().Be(expectedRequiresApproval);
    orderResult.EstimatedDeliveryDays.Should().BeInRange(3, maxDeliveryDays);
}

public class OrderResult
{
    public bool IsSuccess { get; set; }
    public Product Product { get; set; } = new();
    public Customer Customer { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool RequiresApproval { get; set; }
    public int EstimatedDeliveryDays { get; set; }
}

private static OrderResult ProcessElectronicsOrder(Customer customer, Product product, Order order, int quantity)
{
    var totalAmount = product.Price * quantity;
    var discountRate = customer.Type == "VIP" ? 0.15m : 0.1m;
    var discountAmount = totalAmount * discountRate;
    var finalAmount = totalAmount - discountAmount;
    
    return new OrderResult
    {
        IsSuccess = product.IsAvailable && finalAmount <= customer.CreditLimit,
        Product = product,
        Customer = customer,
        TotalAmount = totalAmount,
        DiscountAmount = discountAmount,
        FinalAmount = finalAmount,
        RequiresApproval = product.Price > 10000m,
        EstimatedDeliveryDays = product.Price > 10000m ? Random.Shared.Next(3, 8) : Random.Shared.Next(1, 4)
    };
}
```

## 本日重點回顧

### AutoData 屬性家族的特色

1. **AutoData**：自動化的參數產生，適合簡單的測試情境
2. **InlineAutoData**：結合固定值與自動產生，平衡控制與便利性
3. **MemberAutoData**：整合外部資料來源，支援複雜的測試資料需求
4. **CompositeAutoData**：組合多重設定，建立複雜的測試環境

### 外部資料整合的實作要點

1. **檔案管理**：正確設定 Build Action 和 Copy 行為
2. **資料格式**：使用 CSV 的簡潔性和 JSON 的結構化
3. **工具整合**：CsvHelper 和 System.Text.Json 的運用
4. **路徑處理**：使用相對路徑確保測試的可移植性

### 設計模式的應用

1. **階層式組織**：建立清晰的測試資料結構
2. **可重用性**：設計可跨測試重用的資料集
3. **分離關注點**：將資料來源與測試邏輯分離
4. **版本控制**：確保測試資料的一致性與可追蹤性

### 與 Awesome Assertions 的協作

1. **可讀性改善**：斷言語法讓測試意圖更清晰
2. **錯誤訊息改善**：詳細的失敗訊息幫助定位問題
3. **驗證簡化**：支援複雜物件和集合的驗證
4. **測試維護性**：減少測試程式碼的複雜度

## 今日小結

今天學了 AutoData 屬性家族跟外部資料整合。從基礎的 AutoData 到複雜的 CompositeAutoData，現在知道怎麼讓測試參數自動注入了。

### 重點整理

- **參數注入自動化**：從手動建立物件到自動化的參數注入
- **資料來源多元化**：CSV、JSON 等外部檔案的整合
- **設計模式實踐**：階層式資料組織和可重用資料集的建立
- **工具整合**：CsvHelper、JsonConvert 等工具的運用

### 實務應用

這些做法可以：

1. **減少測試準備程式碼**：從冗長的 Arrange 區塊到簡潔的屬性標註
2. **使用真實測試資料**：從外部檔案載入更接近實際情境的測試資料
3. **改善測試維護性**：集中管理測試資料，減少重複程式碼
4. **提高測試可讀性**：結合 Awesome Assertions 寫出清晰的測試

### 學習行程

Day 10 產生測試資料、Day 11 自訂建構器控制範圍、今天把測試參數自動注入。三塊拼起來，Arrange 那段程式碼可以少寫一大半。

## 相關參考資料

- [AutoFixture Cheat Sheet - AutoData Theories](https://github.com/AutoFixture/AutoFixture/wiki/Cheat-Sheet#autodata-theories)
- [AutoFixture.xUnit3 - GitHub Repository](https://github.com/AutoFixture/AutoFixture.xUnit3)

明天來學 NSubstitute 與 AutoFixture 的整合應用。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day12>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十二天。明天會介紹 Day 13 - NSubstitute 與 AutoFixture 的整合應用。**
