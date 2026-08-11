using System.Collections.Concurrent;

namespace Day08.TestingLoggingOutput.Tests.Logging;

/// <summary>
/// xUnit 測試用的 Logger 實作，將記錄訊息導向測試輸出
/// </summary>
/// <typeparam name="T">Logger 類型</typeparam>
public class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName;
    private readonly LoggerExternalScopeProvider _scopeProvider;

    /// <summary>
    /// XUnitLogger 建構子
    /// </summary>
    /// <param name="testOutputHelper">測試輸出協助器</param>
    /// <param name="scopeProvider">範圍提供者</param>
    public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider)
    {
        _testOutputHelper = testOutputHelper;
        _categoryName = typeof(T).Name;
        _scopeProvider = scopeProvider;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider.Push(state);
    }

    /// <summary>
    /// 開始記錄範圍
    /// </summary>
    /// <typeparam name="TState">狀態類型</typeparam>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    /// <typeparam name="TState"></typeparam>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logLine += $"\n{exception}";
        }

        _testOutputHelper.WriteLine(logLine);
    }
}

/// <summary>
/// class CompositeLogger - 組合 Logger，支援同時使用多個 Logger 實作
/// </summary>
/// <typeparam name="T">Logger 類型</typeparam>
public class CompositeLogger<T> : ILogger<T>
{
    private readonly ILogger<T>[] _loggers;

    /// <summary>
    /// CompositeLogger 建構子
    /// </summary>
    /// <param name="loggers">The logger.</param>
    public CompositeLogger(params ILogger<T>[] loggers)
    {
        _loggers = loggers;
    }

    /// <summary>
    /// 判斷指定的記錄層級是否啟用
    /// </summary>
    /// <param name="logLevel">記錄層級</param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return _loggers.Any(logger => logger.IsEnabled(logLevel));
    }

    /// <summary>
    /// 開始記錄範圍
    /// </summary>
    /// <typeparam name="TState">狀態類型</typeparam>
    /// <param name="state">狀態</param>
    /// <returns></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var scopes = _loggers.Select(logger => logger.BeginScope(state)).ToArray();
        return new CompositeDisposable(scopes);
    }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    /// <typeparam name="TState">狀態類型</typeparam>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}

/// <summary>
/// class CompositeDisposable - 組合 Disposable 實作
/// </summary>
public class CompositeDisposable : IDisposable
{
    private readonly IDisposable?[] _disposables;

    /// <summary>
    /// CompositeDisposable 建構子
    /// </summary>
    /// <param name="disposables">要組合的 Disposable 實作</param>
    public CompositeDisposable(IDisposable?[] disposables)
    {
        _disposables = disposables;
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
    }
}

/// <summary>
/// class TestLogger - 測試用 Logger，支援記錄收集與驗證
/// </summary>
/// <typeparam name="T">Logger 類型</typeparam>
public class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _logs = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new NoOpDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    /// <typeparam name="TState"></typeparam>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
            State = state as IEnumerable<KeyValuePair<string, object>>,
            Exception = exception
        });
    }

    /// <summary>
    /// 取得記錄
    /// </summary>
    /// <param name="level">記錄層級</param>
    /// <returns></returns>
    public IList<LogEntry> GetLogs(LogLevel? level = null)
    {
        return level.HasValue ? _logs.Where(l => l.Level == level).ToList() : _logs.ToList();
    }

    /// <summary>
    /// 清除所有記錄
    /// </summary>
    public void ClearLogs()
    {
        _logs.Clear();
    }
}

/// <summary>
/// 記錄項目
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 記錄層級
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 記錄狀態
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? State { get; set; }

    /// <summary>
    /// 例外資訊
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// class ConcurrentTestLogger - 並行測試用 Logger
/// </summary>
/// <typeparam name="T">Logger 類型</typeparam>
public class ConcurrentTestLogger<T> : ILogger<T>
{
    private readonly ConcurrentBag<LogEntry> _logs = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new NoOpDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 記錄訊息
    /// </summary>
    /// <param name="logLevel">記錄層級</param>
    /// <param name="eventId">事件編號</param>
    /// <param name="state">狀態</param>
    /// <param name="exception">例外</param>
    /// <param name="formatter">格式化函數</param>
    /// <typeparam name="TState"></typeparam>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                            Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logs.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
            State = state as IEnumerable<KeyValuePair<string, object>>,
            Exception = exception
        });
    }

    /// <summary>
    /// 取得記錄
    /// </summary>
    /// <param name="level">記錄層級</param>
    /// <returns></returns>
    public IList<LogEntry> GetLogs(LogLevel? level = null)
    {
        var allLogs = _logs.ToList();
        return level.HasValue ? allLogs.Where(l => l.Level == level).ToList() : allLogs;
    }
}

/// <summary>
/// class NoOpDisposable - 無操作的 Disposable 實作
/// </summary>
public class NoOpDisposable : IDisposable
{
    public void Dispose()
    {
        // 無操作
    }
}