using System.CommandLine;

namespace Nethereum.AppChain.Server
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (AppChainCli.TryHandleAdvancedHelp(args, Console.Out))
                return 0;

            var (rootCommand, options) = AppChainCli.CreateRootCommand();

            rootCommand.SetHandler(async (context) =>
            {
                var layeredConfiguration = AppChainCli.BuildLayeredConfiguration(args);
                var config = AppChainCli.BuildConfig(context.ParseResult, options, layeredConfiguration);

                try
                {
                    await AppChainServerRunner.RunAsync(config);
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"AppChain failed to start: {ex.Message}");
                    Console.Error.WriteLine("Run with --help for usage.");
                    context.ExitCode = 1;
                }
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}
