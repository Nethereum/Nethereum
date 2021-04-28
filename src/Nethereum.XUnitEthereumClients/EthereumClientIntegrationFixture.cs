using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;


namespace Nethereum.XUnitEthereumClients
{

    public static class Web3Factory
    {
        private static Web3.Web3 _web3;
        public static Web3.Web3 GetWeb33()
        {
            if (_web3 == null)
            {
                _web3 = new Web3.Web3(AccountFactory.GetAccount(), ClientFactory.GetClient());
            }

            return _web3;
        }

        public static Web3.Web3 GetWeb3Managed()
        {
            return new Web3.Web3(AccountFactory.GetManagedAccount(), ClientFactory.GetClient());
        }
    }

    public class ClientFactory
    {
        public static IClient GetClient()
        {
            return new RpcClient(new Uri("http://localhost:8545"));
           // return new WebSocketClient("ws://127.0.0.1:8546/ws");

        }
    }

    public static class AccountFactory
    {
        public static string PrivateKey { get; set; } = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public static string Address { get; set; } =  "0x12890d2cce102216644c59daE5baed380d84830c";
        public static string Password { get; set; } = "password";
        public static System.Numerics.BigInteger ChainId { get; set; } = 444444444500;

        public static Account GetAccount()
        {
            return new Account(PrivateKey, ChainId);
        }

        public static ManagedAccount GetManagedAccount()
        {
            return new ManagedAccount(Address, Password);
        }
    }

    public enum EthereumClient
    {
        Geth,
        Parity
    }

    public class EthereumClientIntegrationFixture : IDisposable
    {
        public const string ETHEREUM_CLIENT_COLLECTION_DEFAULT = "Ethereum client Test";
        public static string GethClientPath { get; set; }  = @"..\..\..\..\..\testchain\clique";
        public static string ParityClientPath { get; set; } = @"..\..\..\..\..\testchain\parity poa";
        private readonly Process _process;
        private readonly string _exePath;
        

        private Web3.Web3 _web3;
        public Web3.Web3 GetWeb3()
        {
            if (_web3 == null)
            {
                _web3 = new Web3.Web3(AccountFactory.GetAccount(), ClientFactory.GetClient());
            }

            return _web3;
        }

        public EthereumClient EthereumClient { get; private set; }

        public static IConfiguration InitConfiguration()
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.test.json")
                .Build();
            return config;
        }

        public class EthereumTestSettings
        {
            public string GethPath { get; set; }
            public string ParityPath { get; set; }
        }

        public EthereumClientIntegrationFixture()
        {

            var config = InitConfiguration();
            if (config != null)
            {

                var ethereumTestSection = config.GetSection("EthereumTestSettings");

                if (ethereumTestSection != null)
                {
                    var ethereumTestSettings = new EthereumTestSettings();
                    ethereumTestSection.Bind(ethereumTestSettings);
                    if (!string.IsNullOrEmpty(ethereumTestSettings.GethPath)) GethClientPath = ethereumTestSettings.GethPath;
                    if (!string.IsNullOrEmpty(ethereumTestSettings.ParityPath)) ParityClientPath = ethereumTestSettings.ParityPath;
                  
                }
            }
            

            var client = Environment.GetEnvironmentVariable("ETHEREUM_CLIENT");

            if (client == null)
            {
                Console.WriteLine("**************TEST CLIENT NOT CONFIGURED IN ENVIRONMENT USING DEFAULT");
            }
            else
            {
                Console.WriteLine("************ ENVIRONMENT CONFIGURED WITH CLIENT: " + client.ToString());
            }

            if (string.IsNullOrEmpty(client))
            {
                EthereumClient = EthereumClient.Geth;
            }
            else if (client == "geth")
            {
                EthereumClient = EthereumClient.Geth;
                Console.WriteLine("********TESTING WITH GETH****************");
            }
            else
            {
                EthereumClient = EthereumClient.Parity;
                Console.WriteLine("******* TESTING WITH PARITY ****************");
            }

            if (EthereumClient == EthereumClient.Geth)
            {

                var location = typeof(EthereumClientIntegrationFixture).GetTypeInfo().Assembly.Location;
                var dirPath = Path.GetDirectoryName(location);
                _exePath = Path.GetFullPath(Path.Combine(dirPath, GethClientPath));

                DeleteData();

                var psiSetup = new ProcessStartInfo(Path.Combine(_exePath, "geth.exe"),
                    @"--datadir=devChain init genesis_clique.json ")
                {
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = true,
                    WorkingDirectory = _exePath

                };

                Process.Start(psiSetup);
                Thread.Sleep(3000);

                var psi = new ProcessStartInfo(Path.Combine(_exePath, "geth.exe"),
                    @" --nodiscover --rpc --datadir=devChain  --rpccorsdomain "" * "" --mine --rpcapi ""eth, web3, personal, net, miner, admin, debug"" --rpcaddr ""0.0.0.0"" --allow-insecure-unlock --unlock 0x12890d2cce102216644c59daE5baed380d84830c --password ""pass.txt""  --ws  --wsaddr ""0.0.0.0"" --wsapi ""eth, web3, personal, net, miner, admin, debug"" --wsorigins "" * "" --verbosity 0 console  ")
                {
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = true,
                    WorkingDirectory = _exePath

                };
                _process = Process.Start(psi);
            }
            else
            {

                var location = typeof(EthereumClientIntegrationFixture).GetTypeInfo().Assembly.Location;
                var dirPath = Path.GetDirectoryName(location);
                _exePath = Path.GetFullPath(Path.Combine(dirPath, ParityClientPath));

                //DeleteData();

                var psi = new ProcessStartInfo(Path.Combine(_exePath, "parity.exe"),
                    @" --config node0.toml") // --logging debug")
                {
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = true,
                    WorkingDirectory = _exePath

                };
                _process = Process.Start(psi);
                Thread.Sleep(10000);
            }


            Thread.Sleep(3000);
        }

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }

            Thread.Sleep(2000);
            DeleteData();
        }

        private void DeleteData()
        {
            var attempts = 0;
            var success = false;

            while (!success && attempts < 2)
            {
                try
                {
                    InnerDeleteData();
                    success = true;
                }
                catch
                {
                    Thread.Sleep(1000);
                    attempts = attempts + 1;
                }
            }
        }

        private void InnerDeleteData()
        {
            var pathData = Path.Combine(_exePath, @"devChain\geth");
            if (Directory.Exists(pathData))
            {
                Directory.Delete(pathData, true);
            }

        }
    }
}
