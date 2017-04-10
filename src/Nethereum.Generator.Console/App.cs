using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Generator.Console
{
    public class App : CommandLineApplication
    {
        public App()
        {
            Commands.Add(new GenerateServiceCommand());
            Commands.Add(new GenerateSampleServiceCommand());
            HelpOption("-h | -? | --help");
        }
    }
}