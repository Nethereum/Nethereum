using Microsoft.Extensions.CommandLineUtils;

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