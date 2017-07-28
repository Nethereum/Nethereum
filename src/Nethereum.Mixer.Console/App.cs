using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Mixer.Console
{
    public class App : CommandLineApplication
    {
        public App()
        {
            Commands.Add(new CreateAccountsAndMixBalancesCommand());
            Commands.Add(new GetBalancesFolderCommand());
            HelpOption("-h | -? | --help");
        }
    }
}