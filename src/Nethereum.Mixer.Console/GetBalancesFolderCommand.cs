using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using System.Numerics;

namespace Nethereum.Mixer.Console
{
    public class GetBalancesFolderCommand : CommandLineApplication
    {
        private readonly CommandOption _sourceFolder;
        private readonly CommandOption _rpcAddress;

        public GetBalancesFolderCommand()
        {
            Name = "getBalances";
            Description = "Gets the balances of all the accounts in a folder";
            _sourceFolder = Option("-sf | --sourceFolder", "The folder containing the source accounts", CommandOptionType.SingleValue);
            _rpcAddress = Option("-url", "The rpc address to connect", CommandOptionType.SingleValue);

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

            var rpcAddress = _rpcAddress.Value();
            if (string.IsNullOrWhiteSpace(rpcAddress))
            {
                System.Console.WriteLine("The rpcAddress was not specified");
                return 1;
            }

            BigInteger balance = 0;
            Web3.Web3 web3 = new Web3.Web3(rpcAddress);
            foreach (var file in Directory.GetFiles(sourceFolder))
            {
                var service = new KeyStore.KeyStoreService();
                using (var jsonFile = File.OpenText(file))
                {
                    var json = jsonFile.ReadToEnd();
                    var address = service.GetAddressFromKeyStore(json);
                    balance = balance + web3.Eth.GetBalance.SendRequestAsync(address).Result;
                }
            }

            System.Console.WriteLine("Total Balance: " + Web3.Web3.Convert.FromWei(balance));

            return 1;
        }
    }
}