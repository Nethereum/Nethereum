using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;

namespace Nethereum.XUnitEthereumClients
{

    public static class Web3Factory
    {
        private static Web3.Web3 _web3;
        public static Web3.Web3 GetWeb33()
        {
            if(_web3 == null) _web3 = new Web3.Web3(AccountFactory.GetAccount(), ClientFactory.GetClient());
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
        }
    }

    public static class AccountFactory
    {
        public static string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public static string Address = "0x12890d2cce102216644c59daE5baed380d84830c";
        public static string Password = "password";

        public static Account GetAccount()
        {
            return new Account(PrivateKey, 444444444500);
        }

        public static ManagedAccount GetManagedAccount()
        {
            return new ManagedAccount(Address, Password);
        }
    }


    public class EthereumClientIntegrationFixture : IDisposable
    {
        public const string ETHEREUM_CLIENT_COLLECTION_DEFAULT = "Ethereum client Test";
        private Process _process;
        private string _exePath;

        private Web3.Web3 _web3;
        public Web3.Web3 GetWeb3()
        {
            if (_web3 == null) _web3 = new Web3.Web3(AccountFactory.GetAccount(), ClientFactory.GetClient());
            return _web3;
        }

        public bool Geth { get; private set; }

        public EthereumClientIntegrationFixture()
        {
            //TODO:
            //Load configuration from file.
            //The file can be at the Solution level.
            //1, Path
            //2, Executable
            //3 Arguments setup
            //4 Arguments.
            // So the tests can run for both Geth and Parity, Windows, Mac and Linux.

            var client = Environment.GetEnvironmentVariable("ETHEREUM_CLIENT");
            
            if(client == null) Console.WriteLine("**************CLIENT NOT CONFIGURED");
            else Console.WriteLine("**************CLIENT " + client.ToString());

            if (string.IsNullOrEmpty(client))
            {
                Geth = true;
            }
            else if (client == "geth")
            {
                Geth = true;
                Console.WriteLine("***** GETH ****************");
            }
            else
            {
                Geth = false;
                Console.WriteLine("***** PARITY ****************");
            }

            //Geth = false;
            if (Geth)
            {

                var location = typeof(EthereumClientIntegrationFixture).GetTypeInfo().Assembly.Location;
                var dirPath = Path.GetDirectoryName(location);
                _exePath = Path.GetFullPath(Path.Combine(dirPath, @"..\..\..\..\..\testchain\clique"));

                DeleteData();

                ProcessStartInfo psiSetup = new ProcessStartInfo(Path.Combine(_exePath, "geth.exe"),
                    @"--datadir=devChain init genesis_clique.json ")
                {
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = true,
                    WorkingDirectory = _exePath

                };

                Process.Start(psiSetup);
                Thread.Sleep(3000);

                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(_exePath, "geth.exe"),
                    @" --nodiscover --rpc --datadir=devChain  --rpccorsdomain "" * "" --mine --rpcapi ""eth, web3, personal, net, miner, admin, debug"" --rpcaddr ""0.0.0.0"" --unlock 0x12890d2cce102216644c59daE5baed380d84830c --password ""pass.txt"" --verbosity 0 console  ")
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
                _exePath = Path.GetFullPath(Path.Combine(dirPath, @"..\..\..\..\..\testchain\parity poa"));

                //DeleteData();

                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(_exePath, "parity.exe"),
                    @" --config node0.toml")
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
            if(!_process.HasExited)
            _process.Kill();
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
