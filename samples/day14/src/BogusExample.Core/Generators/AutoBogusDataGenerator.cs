using BogusExample.Core.Models;

namespace BogusExample.Core.Generators;

/// <summary>
/// 純 Bogus 資料產生器（原 AutoBogus 版本已改寫）
/// 使用 Faker&lt;T&gt; 取代已停維的 AutoBogus
/// </summary>
public static class AutoBogusDataGenerator
{
    private static readonly Faker<Product> ProductFaker = new Faker<Product>()
        .RuleFor(p => p.Id, f => f.Random.Int(1, 10000))
        .RuleFor(p => p.Name, f => f.Commerce.ProductName())
        .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
        .RuleFor(p => p.Price, f => f.Random.Decimal(10, 5000))
        .RuleFor(p => p.Category, f => f.Commerce.Categories(1)[0])
        .RuleFor(p => p.CreatedDate, f => f.Date.Past())
        .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 500))
        .RuleFor(p => p.IsActive, f => f.Random.Bool(0.9f))
        .RuleFor(p => p.Tags, f => f.PickRandom(
            new[] { "New", "Sale", "Featured", "Limited", "Premium", "Eco-Friendly" },
            f.Random.Int(0, 3)).ToList());

    private static readonly Faker<User> UserFaker = new Faker<User>()
        .RuleFor(u => u.Id, f => f.Random.Guid())
        .RuleFor(u => u.FirstName, f => f.Person.FirstName)
        .RuleFor(u => u.LastName, f => f.Person.LastName)
        .RuleFor(u => u.Email, f => f.Internet.Email())
        .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber())
        .RuleFor(u => u.Age, f => f.Random.Int(18, 80))
        .RuleFor(u => u.IsPremium, f => f.Random.Bool())
        .RuleFor(u => u.Points, f => f.Random.Int(0, 10000))
        .RuleFor(u => u.Nickname, f => f.Internet.UserName())
        .RuleFor(u => u.Department, f => f.PickRandom("IT", "HR", "Finance", "Marketing"))
        .RuleFor(u => u.Role, f => f.PickRandom("Admin", "User", "Manager"))
        .RuleFor(u => u.CreatedAt, f => f.Date.Past());

    private static readonly Faker<TaiwanPerson> TaiwanPersonFaker = new Faker<TaiwanPerson>()
        .RuleFor(t => t.Id, f => f.Random.Guid())
        .RuleFor(t => t.Name, f => f.Name.FullName())
        .RuleFor(t => t.City, f => f.Address.City())
        .RuleFor(t => t.University, f => f.Company.CompanyName() + "大學")
        .RuleFor(t => t.Company, f => f.Company.CompanyName())
        .RuleFor(t => t.IdCard, f => f.Random.String2(10, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"))
        .RuleFor(t => t.Mobile, f => "09" + f.Random.String2(8, "0123456789"));

    private static readonly Faker<OrderItem> OrderItemFaker = new Faker<OrderItem>()
        .RuleFor(i => i.Id, f => f.Random.Guid())
        .RuleFor(i => i.ProductName, f => f.Commerce.ProductName())
        .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
        .RuleFor(i => i.UnitPrice, f => f.Random.Decimal(10, 1000));

    private static readonly Faker<Order> OrderFaker = new Faker<Order>()
        .RuleFor(o => o.Id, f => f.Random.Guid())
        .RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10))
        .RuleFor(o => o.CustomerName, f => f.Name.FullName())
        .RuleFor(o => o.CustomerEmail, f => f.Internet.Email())
        .RuleFor(o => o.OrderDate, f => f.Date.Past())
        .RuleFor(o => o.Status, f => f.PickRandom<OrderStatus>())
        .RuleFor(o => o.Items, f => OrderItemFaker.Generate(f.Random.Int(1, 5)))
        .RuleFor(o => o.SubTotal, (f, o) => o.Items.Sum(i => i.TotalPrice))
        .RuleFor(o => o.TaxAmount, (f, o) => o.SubTotal * 0.05m)
        .RuleFor(o => o.ShippingFee, f => f.Random.Decimal(0, 200))
        .RuleFor(o => o.TotalAmount, (f, o) => o.SubTotal + o.TaxAmount + o.ShippingFee);

    private static readonly Faker<Project> ProjectFaker = new Faker<Project>()
        .RuleFor(p => p.Id, f => f.Random.Guid())
        .RuleFor(p => p.Name, f => f.Commerce.ProductName())
        .RuleFor(p => p.Description, f => f.Lorem.Sentence())
        .RuleFor(p => p.StartDate, f => f.Date.Past(2))
        .RuleFor(p => p.EndDate, f => f.Date.Past(1))
        .RuleFor(p => p.Technologies, f => f.PickRandom(
            new[] { "C#", ".NET", "SQL", "Redis", "Docker", "Azure", "React", "TypeScript" },
            f.Random.Int(1, 4)).ToList());

    private static readonly Faker<Employee> EmployeeFaker = new Faker<Employee>()
        .RuleFor(e => e.Id, f => f.Random.Guid())
        .RuleFor(e => e.EmployeeId, f => f.Random.AlphaNumeric(8))
        .RuleFor(e => e.FirstName, f => f.Person.FirstName)
        .RuleFor(e => e.LastName, f => f.Person.LastName)
        .RuleFor(e => e.Email, f => f.Internet.Email())
        .RuleFor(e => e.Age, f => f.Random.Int(22, 60))
        .RuleFor(e => e.Level, f => f.PickRandom("Junior", "Senior", "Lead", "Principal"))
        .RuleFor(e => e.Salary, f => f.Random.Decimal(30000, 150000))
        .RuleFor(e => e.HireDate, f => f.Date.Past(5))
        .RuleFor(e => e.Department, f => f.Commerce.Department())
        .RuleFor(e => e.Skills, f => f.PickRandom(
            new[] { "C#", ".NET", "SQL", "Redis", "Docker", "Azure", "React", "TypeScript" },
            f.Random.Int(2, 5)).ToList())
        .RuleFor(e => e.Projects, (f, e) => ProjectFaker.Generate(f.Random.Int(1, 3)));

    private static readonly Faker<CreateOrderItemRequest> OrderItemRequestFaker = new Faker<CreateOrderItemRequest>()
        .RuleFor(i => i.ProductId, f => f.Random.Int(1, 1000))
        .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
        .RuleFor(i => i.Notes, f => f.Lorem.Sentence().OrNull(f));

    private static readonly Faker<CreateOrderRequest> OrderRequestFaker = new Faker<CreateOrderRequest>()
        .RuleFor(r => r.CustomerName, f => f.Name.FullName())
        .RuleFor(r => r.CustomerEmail, f => f.Internet.Email())
        .RuleFor(r => r.ShippingAddress, f => f.Address.FullAddress())
        .RuleFor(r => r.Items, f => OrderItemRequestFaker.Generate(f.Random.Int(1, 5)))
        .RuleFor(r => r.Notes, f => f.Lorem.Sentence().OrNull(f));

    /// <summary>
    /// 產生產品（原 AutoBogus 版本）
    /// </summary>
    public static Product CreateProductWithAutoBogus() => ProductFaker.Generate();

    /// <summary>
    /// 產生使用者（原 AutoBogus 版本）
    /// </summary>
    public static User CreateUserWithAutoBogus() => UserFaker.Generate();

    /// <summary>
    /// 產生台灣人（原 AutoBogus 版本）
    /// </summary>
    public static TaiwanPerson CreateTaiwanPersonWithAutoBogus() => TaiwanPersonFaker.Generate();

    /// <summary>
    /// 產生訂單（原 AutoBogus 版本）
    /// </summary>
    public static Order CreateOrderWithAutoBogus() => OrderFaker.Generate();

    /// <summary>
    /// 產生員工（原 AutoBogus 版本）
    /// </summary>
    public static Employee CreateEmployeeWithAutoBogus() => EmployeeFaker.Generate();

    /// <summary>
    /// 產生多個產品
    /// </summary>
    public static List<Product> CreateMultipleProducts(int count) => ProductFaker.Generate(count);

    /// <summary>
    /// 產生多個使用者
    /// </summary>
    public static List<User> CreateMultipleUsers(int count) => UserFaker.Generate(count);

    /// <summary>
    /// 產生多個台灣人
    /// </summary>
    public static List<TaiwanPerson> CreateMultipleTaiwanPeople(int count) => TaiwanPersonFaker.Generate(count);

    /// <summary>
    /// 產生多個訂單
    /// </summary>
    public static List<Order> CreateMultipleOrders(int count) => OrderFaker.Generate(count);

    /// <summary>
    /// 產生多個員工
    /// </summary>
    public static List<Employee> CreateMultipleEmployees(int count) => EmployeeFaker.Generate(count);

    /// <summary>
    /// 建立客製化使用者（使用 Bogus Faker 做客製化）
    /// </summary>
    public static User CreateCustomUser(string email, int age)
    {
        var faker = new Faker<User>()
            .RuleFor(u => u.Email, email)
            .RuleFor(u => u.Age, age)
            .RuleFor(u => u.FirstName, f => f.Person.FirstName)
            .RuleFor(u => u.LastName, f => f.Person.LastName)
            .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(u => u.Points, f => f.Random.Int(0, 1000))
            .RuleFor(u => u.IsPremium, f => f.Random.Bool());

        return faker.Generate();
    }

    /// <summary>
    /// 建立客製化產品（使用 Bogus Faker 做客製化）
    /// </summary>
    public static Product CreateCustomProduct(string name, decimal price)
    {
        var faker = new Faker<Product>()
            .RuleFor(p => p.Name, name)
            .RuleFor(p => p.Price, price)
            .RuleFor(p => p.Id, f => f.Random.Int(1, 1000))
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Category, f => f.Commerce.Categories(1)[0])
            .RuleFor(p => p.CreatedDate, f => f.Date.Past())
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 500))
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.9f))
            .RuleFor(p => p.Tags, f => f.PickRandom(
                new[] { "New", "Sale", "Featured", "Limited", "Premium", "Eco-Friendly" },
                f.Random.Int(0, 3)).ToList());

        return faker.Generate();
    }

    /// <summary>
    /// 產生建立訂單請求
    /// </summary>
    public static CreateOrderRequest CreateOrderRequest() => OrderRequestFaker.Generate();
}
