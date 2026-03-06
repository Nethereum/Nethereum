using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.IntegrationTests
{
    public static class TestLoggerFactory
    {
        private static ILoggerFactory? _factory;

        public static ILogger CreateConsoleLogger(string categoryName = "DevChain")
        {
            _factory ??= LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            return _factory.CreateLogger(categoryName);
        }

        public static ILogger<T> CreateConsoleLogger<T>()
        {
            _factory ??= LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            return _factory.CreateLogger<T>();
        }

        public static ILoggerFactory GetOrCreateFactory()
        {
            _factory ??= LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            return _factory;
        }

        public static void Dispose()
        {
            _factory?.Dispose();
            _factory = null;
        }
    }
}
