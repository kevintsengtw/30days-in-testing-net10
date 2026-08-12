using AutoFixture;
using AutoFixtureBogusMix.Core.Models;
using AutoFixtureBogusMix.Core.TestData.Extensions;
using Bogus;

namespace AutoFixtureBogusMix.Core.TestData.Factories;

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