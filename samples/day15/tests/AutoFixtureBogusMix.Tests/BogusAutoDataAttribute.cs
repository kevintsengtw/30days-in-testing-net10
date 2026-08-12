using AutoFixture;
using AutoFixture.Xunit3;
using AutoFixtureBogusMix.Core.TestData.Extensions;

namespace AutoFixtureBogusMix.Tests;

/// <summary>
/// 整合 Bogus 的 AutoData 屬性
/// </summary>
public class BogusAutoDataAttribute : AutoDataAttribute
{
    public BogusAutoDataAttribute() : base(() => new Fixture().WithBogus())
    {
    }
}