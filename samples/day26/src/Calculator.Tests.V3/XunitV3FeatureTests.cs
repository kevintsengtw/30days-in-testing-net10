using Calculator.Tests.V3.Fixtures;
using System.Diagnostics;

namespace Calculator.Tests.V3;

public sealed class XunitV3FeatureTests(SharedStateFixture fixture)
{
    public static MatrixTheoryData<int, int> AdditionMatrix => new(
        [1, 10],
        [2, 20]);

    [Fact]
    public void AssemblyFixture_測試開始前已完成初始化()
    {
        fixture.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task TestContext_提供取消權杖與附件功能()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);

        TestContext.Current.AddAttachment(
            "calculation.txt",
            "10 + 20 = 30");

        cancellationToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public void Console與Trace輸出_由Runner擷取()
    {
        Console.WriteLine("console output from xUnit v3");
        Trace.WriteLine("trace output from xUnit v3");

        new Calculator.Core.Calculator().Add(1, 2).Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(AdditionMatrix))]
    public void MatrixTheoryData_產生輸入值的笛卡兒積(int a, int b)
    {
        new Calculator.Core.Calculator().Add(a, b).Should().Be(a + b);
    }

    [Fact]
    public void AssertSkip_可在執行期間決定跳過()
    {
        if (Environment.GetEnvironmentVariable("DAY26_OPTIONAL_TEST") != "1")
        {
            Assert.Skip("設定 DAY26_OPTIONAL_TEST=1 才執行此選用測試");
        }

        new Calculator.Core.Calculator().Square(5).Should().Be(25);
    }

    [Fact(Explicit = true)]
    public void ExplicitTest_只在明確要求時執行()
    {
        new Calculator.Core.Calculator().Multiply(6, 7).Should().Be(42);
    }
}
