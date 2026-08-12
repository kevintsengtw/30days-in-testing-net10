using AutoFixture;
using AutoFixtureBogusMix.Core.TestData.Extensions;
using AutoFixtureBogusMix.Core.TestData.Factories;

namespace AutoFixtureBogusMix.Core.TestData.Base;

/// <summary>
/// 測試基底類別，提供統一的資料產生功能
/// </summary>
public abstract class TestBase
{
    protected readonly IFixture Fixture;
    protected readonly HybridTestDataGenerator Generator;
    protected readonly IntegratedTestDataFactory Factory;

    protected TestBase(int? seed = null)
    {
        // 建立統一設定的 AutoFixture
        Fixture = new Fixture()
                  .WithBogus()
                  .WithOmitOnRecursion()
                  .WithRepeatCount(3);

        if (seed.HasValue)
        {
            Fixture.WithSeed(seed.Value);
        }

        // 建立混合產生器
        Generator = new HybridTestDataGenerator(seed);

        // 建立整合工廠
        Factory = new IntegratedTestDataFactory(seed);
    }

    /// <summary>
    /// 快速建立單一物件
    /// </summary>
    protected T Create<T>()
    {
        return Fixture.Create<T>();
    }

    /// <summary>
    /// 快速建立多個物件
    /// </summary>
    protected List<T> CreateMany<T>(int count = 3)
    {
        return Fixture.CreateMany<T>(count).ToList();
    }

    /// <summary>
    /// 建立並設定物件
    /// </summary>
    protected T Create<T>(Action<T> configure)
    {
        var instance = Create<T>();
        configure(instance);
        return instance;
    }

    /// <summary>
    /// 使用混合產生器建立物件
    /// </summary>
    protected T Generate<T>()
    {
        return Generator.Generate<T>();
    }

    /// <summary>
    /// 使用工廠建立物件
    /// </summary>
    protected T FactoryCreate<T>() where T : class
    {
        return Factory.CreateFresh<T>();
    }

    /// <summary>
    /// 清理方法，在測試結束時呼叫
    /// </summary>
    protected virtual void Cleanup()
    {
        Factory.ClearCache();
    }
}