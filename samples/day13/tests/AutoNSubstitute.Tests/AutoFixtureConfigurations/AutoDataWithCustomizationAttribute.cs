using AutoFixture.AutoNSubstitute;

namespace AutoNSubstitute.Tests.AutoFixtureConfigurations;

/// <summary>
/// 包含客製化設定的 AutoData 屬性
/// </summary>
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    /// <summary>
    /// 建構函式
    /// </summary>
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    internal static IFixture CreateFixture()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization())
                                   .Customize(new MapsterMapperCustomization());

        return fixture;
    }
}