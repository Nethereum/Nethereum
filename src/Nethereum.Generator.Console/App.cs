using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generator.Console.Commands;

namespace Nethereum.Generator.Console
{
    public class App : CommandLineApplication
    {
        public App()
        {
            Commands.Add(new GenerateFromProjectCommand());
            Commands.Add(new GenerateFromAbiCommand());
            HelpOption("-h | -? | --help");
        }
    }
}