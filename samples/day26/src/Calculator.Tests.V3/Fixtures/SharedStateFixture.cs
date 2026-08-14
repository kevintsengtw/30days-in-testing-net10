namespace Calculator.Tests.V3.Fixtures;

/// <summary>
/// xUnit v3 的 assembly fixture：整個測試組件只初始化及清理一次。
/// </summary>
public sealed class SharedStateFixture : IAsyncLifetime
{
    public bool IsInitialized { get; private set; }

    public ValueTask InitializeAsync()
    {
        IsInitialized = true;
        Console.WriteLine("SharedStateFixture initialized");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsInitialized = false;
        Console.WriteLine("SharedStateFixture disposed");
        return ValueTask.CompletedTask;
    }
}
