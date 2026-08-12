using System.Reflection;
using AutoFixture.Kernel;
using Bogus;

namespace AutoFixtureBogusMix.Core.TestData.SpecimenBuilders;

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