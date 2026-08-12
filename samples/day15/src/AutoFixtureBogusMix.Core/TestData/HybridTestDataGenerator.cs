using AutoFixture;
using AutoFixtureBogusMix.Core.TestData.SpecimenBuilders;
using Bogus;

namespace AutoFixtureBogusMix.Core.TestData;

/// <summary>
/// 混合資料產生器實作
/// </summary>
public class HybridTestDataGenerator : ITestDataGenerator
{
    private readonly IFixture _fixture;

    public HybridTestDataGenerator(int? seed = null)
    {
        _fixture = new Fixture();

        // 設定 Seed 以確保測試可重現性
        if (seed.HasValue)
        {
            SetSeed(seed.Value);
        }

        // 設定 AutoFixture 的預設行為
        ConfigureAutoFixture();

        // 整合 Bogus 到 AutoFixture
        IntegrateBogus();
    }

    public T Generate<T>()
    {
        return _fixture.Create<T>();
    }

    public IEnumerable<T> Generate<T>(int count)
    {
        return Enumerable.Range(0, count).Select(_ => Generate<T>());
    }

    public T Generate<T>(Action<T> configure)
    {
        var item = Generate<T>();
        configure(item);
        return item;
    }

    public T Generate<T>(params object[] constructorParameters)
    {
        if (constructorParameters.Length == 0)
        {
            return Generate<T>();
        }

        return _fixture.Build<T>()
                       .FromFactory(() => (T)Activator.CreateInstance(typeof(T), constructorParameters)!)
                       .Create();
    }

    /// <summary>
    /// 取得底層的 AutoFixture 執行個體，供進階使用
    /// </summary>
    public IFixture GetFixture()
    {
        return _fixture;
    }

    private void SetSeed(int seed)
    {
        // 設定 AutoFixture 的隨機種子
        var random = new Random(seed);
        _fixture.Register(() => random);

        // 設定 Bogus 的隨機種子（稍後在 SpecimenBuilder 中使用）
        Randomizer.Seed = new Random(seed);
    }

    private void ConfigureAutoFixture()
    {
        // 循環參考處理
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
                .ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // 設定集合長度
        _fixture.RepeatCount = 3;
    }

    private void IntegrateBogus()
    {
        // 先加入屬性層級的整合（優先級較高）
        _fixture.Customizations.Add(new EmailSpecimenBuilder());
        _fixture.Customizations.Add(new PhoneSpecimenBuilder());
        _fixture.Customizations.Add(new NameSpecimenBuilder());
        _fixture.Customizations.Add(new AddressSpecimenBuilder());
        _fixture.Customizations.Add(new WebsiteSpecimenBuilder());
        _fixture.Customizations.Add(new CompanyNameSpecimenBuilder());

        // 再加入類型層級的整合（優先級較低）
        // 使用種子感知的 SpecimenBuilder 以確保一致性
        _fixture.Customizations.Add(new SeedAwareBogusSpecimenBuilder(GetCurrentSeed()));
    }

    private int? GetCurrentSeed()
    {
        // 嘗試從 Randomizer 取得目前的種子
        return Randomizer.Seed?.Next();
    }
}