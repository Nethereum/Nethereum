using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed class ListLogger : ILogger
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries.ToArray();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, formatter(state, exception), exception));
}
