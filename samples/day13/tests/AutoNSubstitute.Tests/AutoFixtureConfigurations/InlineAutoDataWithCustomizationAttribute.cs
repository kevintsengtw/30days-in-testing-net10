using AutoFixture.AutoNSubstitute;

namespace AutoNSubstitute.Tests.AutoFixtureConfigurations;

/// <summary>
/// class InlineAutoDataWithCustomizationAttribute
/// </summary>
public class InlineAutoDataWithCustomizationAttribute : InlineAutoDataAttribute
{
    public InlineAutoDataWithCustomizationAttribute(params object[] values)
        : base(AutoDataWithCustomizationAttribute.CreateFixture, values)
    {
    }
}