using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Nethereum.MainnetChain.Server
{
    public static class ServerLogging
    {
        public static IConfigurationSource DefaultLevels(bool verbose) => new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = nameof(LogLevel.Warning),
                ["Logging:LogLevel:Nethereum"] = verbose ? nameof(LogLevel.Debug) : nameof(LogLevel.Information),
                ["Logging:LogLevel:Microsoft.Extensions.Hosting.Internal.Host"] = nameof(LogLevel.Critical),
            }
        };

        public static void Configure(ILoggingBuilder logging, IConfiguration configuration)
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole(o => o.FormatterName = CompactConsoleFormatter.FormatterName);
            logging.AddConsoleFormatter<CompactConsoleFormatter, ConsoleFormatterOptions>();
        }
    }
}
