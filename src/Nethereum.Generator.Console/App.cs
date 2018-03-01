using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generator.Console.Generators.Services;

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