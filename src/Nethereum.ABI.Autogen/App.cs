using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.ABI.Autogen
{
    public class App : CommandLineApplication
    {
        public App()
        {
            Commands.Add(new GenerateCommand());
            HelpOption("-h | -? | --help");
        }
    }
}
