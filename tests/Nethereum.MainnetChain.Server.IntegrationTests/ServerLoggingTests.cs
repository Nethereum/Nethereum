using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.MainnetChain.Server;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class ServerLoggingTests
    {
        private static ILoggerFactory FactoryFor(bool verbose, params string[] args)
        {
            var configuration = new ConfigurationManager();
            configuration.AddCommandLine(args);
            configuration.Sources.Insert(0, ServerLogging.DefaultLevels(verbose));
            return LoggerFactory.Create(lb => ServerLogging.Configure(lb, configuration));
        }

        [Fact]
        public void Given_no_logging_overrides_When_the_server_logs_Then_Nethereum_is_Information_and_other_categories_are_Warning()
        {
            using var factory = FactoryFor(verbose: false);

            Assert.True(factory.CreateLogger("Nethereum.MainnetChain").IsEnabled(LogLevel.Information));
            Assert.False(factory.CreateLogger("Nethereum.MainnetChain").IsEnabled(LogLevel.Debug));
            Assert.False(factory.CreateLogger("Other.Category").IsEnabled(LogLevel.Information));
            Assert.True(factory.CreateLogger("Other.Category").IsEnabled(LogLevel.Warning));
        }

        [Fact]
        public void Given_the_verbose_flag_When_the_server_logs_Then_Nethereum_is_Debug()
        {
            using var factory = FactoryFor(verbose: true);

            Assert.True(factory.CreateLogger("Nethereum.MainnetChain").IsEnabled(LogLevel.Debug));
        }

        [Fact]
        public void Given_standard_Logging_LogLevel_command_line_overrides_When_the_server_logs_Then_they_win_over_the_defaults()
        {
            using var factory = FactoryFor(
                verbose: false, "--Logging:LogLevel:Default=Debug", "--Logging:LogLevel:Nethereum=Trace");

            Assert.True(factory.CreateLogger("Nethereum.CoreChain.BlockExecutor").IsEnabled(LogLevel.Trace));
            Assert.True(factory.CreateLogger("Other.Category").IsEnabled(LogLevel.Debug));
            Assert.False(factory.CreateLogger("Other.Category").IsEnabled(LogLevel.Trace));
        }

        [Fact]
        public void Given_a_Logging_LogLevel_override_for_one_Nethereum_namespace_When_the_server_logs_Then_only_that_namespace_changes()
        {
            using var factory = FactoryFor(verbose: false, "--Logging:LogLevel:Nethereum.CoreChain.Engine=Debug");

            Assert.True(factory.CreateLogger("Nethereum.CoreChain.Engine.FollowerEngineApiService").IsEnabled(LogLevel.Debug));
            Assert.False(factory.CreateLogger("Nethereum.CoreChain.Sync.TipFollowerEngine").IsEnabled(LogLevel.Debug));
        }
    }
}
