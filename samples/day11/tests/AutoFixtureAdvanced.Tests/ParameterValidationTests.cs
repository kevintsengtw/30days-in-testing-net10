namespace AutoFixtureAdvanced.Tests;

using System;
using AutoFixtureAdvanced.Tests.TestHelpers;
using AwesomeAssertions;

/// <summary>
/// 參數驗證測試
/// </summary>
public class ParameterValidationTests
{
    [Fact]
    public void RandomRangedDateTimeBuilder_當最小日期大於等於最大日期_應拋出ArgumentException()
    {
        // Arrange
        var minDate = new DateTime(2024, 12, 31);
        var maxDate = new DateTime(2024, 1, 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new RandomRangedDateTimeBuilder(minDate, maxDate, "CreatedAt"));

        exception.Message.Should().Contain("最小日期必須小於最大日期");
        exception.ParamName.Should().Be("minDate");
    }

    [Fact]
    public void RandomRangedDateTimeBuilder_當目標屬性為null_應拋出ArgumentException()
    {
        // Arrange
        var minDate = new DateTime(2024, 1, 1);
        var maxDate = new DateTime(2024, 12, 31);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new RandomRangedDateTimeBuilder(minDate, maxDate, null!));

        exception.Message.Should().Contain("必須指定至少一個目標屬性");
        exception.ParamName.Should().Be("targetProperties");
    }

    [Fact]
    public void RandomRangedDateTimeBuilder_當目標屬性為空陣列_應拋出ArgumentException()
    {
        // Arrange
        var minDate = new DateTime(2024, 1, 1);
        var maxDate = new DateTime(2024, 12, 31);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new RandomRangedDateTimeBuilder(minDate, maxDate, new string[0]));

        exception.Message.Should().Contain("必須指定至少一個目標屬性");
        exception.ParamName.Should().Be("targetProperties");
    }

    [Fact]
    public void NumericRangeBuilder_當最小值大於等於最大值_應拋出ArgumentException()
    {
        // Arrange
        var min = 100;
        var max = 50;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new NumericRangeBuilder<int>(min, max, p => p.Name == "Age"));

        exception.Message.Should().Contain("最小值必須小於最大值");
        exception.ParamName.Should().Be("min");
    }

    [Fact]
    public void NumericRangeBuilder_當predicate為null_應拋出ArgumentNullException()
    {
        // Arrange
        var min = 1;
        var max = 100;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NumericRangeBuilder<int>(min, max, null!));

        exception.ParamName.Should().Be("predicate");
    }
}