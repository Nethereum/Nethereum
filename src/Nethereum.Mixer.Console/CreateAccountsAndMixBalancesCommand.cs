using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Mixer.Console
{
    public class CreateAccountsAndMixBalancesCommand : CommandLineApplication
    {
        private readonly CommandOption _sourceFolder;
        private readonly CommandOption _destinationFolder;
        private readonly CommandOption _password;
        private readonly CommandOption _rpcAddress;
        private readonly CommandOption _numberOfAccounts;


        public CreateAccountsAndMixBalancesCommand()
        {
            Name = "mix-balances";
            Description = "Generates new accounts in a given folder, and mixes and transfer balances from the accounts in a folder";
            _sourceFolder = Option("-sf | --sourceFolder", "The folder containing the source accounts", CommandOptionType.SingleValue);
            _destinationFolder = Option("-df | --destinationFolder", "The folder to create new accounts", CommandOptionType.SingleValue);
            _password = Option("-p | --password", "The generic password used for all the account files", CommandOptionType.SingleValue);
            _rpcAddress = Option("-url", "The rpc address to connect", CommandOptionType.SingleValue);
            _numberOfAccounts = Option("-na", "Optional: The number of accounts to create, defaults to 4", CommandOptionType.SingleValue);
            HelpOption("-? | -h | --help");
            OnExecute((Func<int>)RunCommand);
        }

        private int RunCommand()
        {
            var sourceFolder = _sourceFolder.Value();
            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                System.Console.WriteLine("The source folder was not specified");
                return 1;
            }

            var destinationFolder = _destinationFolder.Value();
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                System.Console.WriteLine("The destination folder was not specified");
                return 1;
            }

            var password = _password.Value();
            if (string.IsNullOrWhiteSpace(password))
            {
                System.Console.WriteLine("The password was not specified");
                return 1;
            }

            var rpcAddress = _rpcAddress.Value();
            if (string.IsNullOrWhiteSpace(rpcAddress))
            {
                System.Console.WriteLine("The rpcAddress was not specified");
                return 1;
            }

            var numberOfAcccounts = 4;
            if (!string.IsNullOrWhiteSpace(_numberOfAccounts.Value()))
            {
                
                var passed =  int.TryParse(_numberOfAccounts.Value(), out numberOfAcccounts);
                if (!passed)
                {
                    System.Console.WriteLine("Number of accounts is not a valid number");
                    return 1;
                }
            }

            new SimpleMixerService().CreateAccountsAndMixBalances(sourceFolder, password, destinationFolder, numberOfAcccounts, rpcAddress).Wait();
            return 0;
        }
    }
}