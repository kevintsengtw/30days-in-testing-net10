using AutoFixture.Kernel;
using AutoFixtureBogusMix.Core.Models;
using Bogus;

namespace AutoFixtureBogusMix.Core.TestData.SpecimenBuilders;

/// <summary>
/// 種子感知的 Bogus SpecimenBuilder
/// </summary>
public class SeedAwareBogusSpecimenBuilder : ISpecimenBuilder
{
    private readonly Dictionary<Type, object> _fakers;
    private readonly int? _seed;

    public SeedAwareBogusSpecimenBuilder(int? seed = null)
    {
        _seed = seed;
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
        // 如果有種子，設定 Bogus 的隨機種子
        if (_seed.HasValue)
        {
            Randomizer.Seed = new Random(_seed.Value);
        }

        // 註冊使用者相關的 Faker
        _fakers[typeof(User)] = new Faker<User>()
                                .UseSeed(_seed ?? Random.Shared.Next())
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
                                   .UseSeed(_seed ?? Random.Shared.Next())
                                   .RuleFor(a => a.Id, f => f.Random.Guid())
                                   .RuleFor(a => a.Street, f => f.Address.StreetAddress())
                                   .RuleFor(a => a.City, f => f.Address.City())
                                   .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
                                   .RuleFor(a => a.Country, f => f.Address.Country());

        // 註冊公司相關的 Faker
        _fakers[typeof(Company)] = new Faker<Company>()
                                   .UseSeed(_seed ?? Random.Shared.Next())
                                   .RuleFor(c => c.Id, f => f.Random.Guid())
                                   .RuleFor(c => c.Name, f => f.Company.CompanyName())
                                   .RuleFor(c => c.Industry, f => f.Commerce.Department())
                                   .RuleFor(c => c.Website, f => f.Internet.Url())
                                   .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
                                   .Ignore(c => c.Address)
                                   .Ignore(c => c.Employees);

        // 註冊產品相關的 Faker
        _fakers[typeof(Product)] = new Faker<Product>()
                                   .UseSeed(_seed ?? Random.Shared.Next())
                                   .RuleFor(p => p.Id, f => f.Random.Guid())
                                   .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                                   .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
                                   .RuleFor(p => p.Price, f => f.Random.Decimal(1, 1000))
                                   .RuleFor(p => p.Category, f => f.Commerce.Categories(1).First())
                                   .RuleFor(p => p.IsActive, f => f.Random.Bool(0.8f));

        // 註冊訂單項目相關的 Faker
        _fakers[typeof(OrderItem)] = new Faker<OrderItem>()
                                     .UseSeed(_seed ?? Random.Shared.Next())
                                     .RuleFor(oi => oi.Id, f => f.Random.Guid())
                                     .RuleFor(oi => oi.Quantity, f => f.Random.Int(1, 10))
                                     .RuleFor(oi => oi.UnitPrice, f => f.Random.Decimal(1, 500))
                                     .Ignore(oi => oi.Product);

        // 註冊訂單相關的 Faker
        _fakers[typeof(Order)] = new Faker<Order>()
                                 .UseSeed(_seed ?? Random.Shared.Next())
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