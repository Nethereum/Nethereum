using System.Diagnostics;
using System.IO;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;

namespace Nethereum.Accounts.IntegrationTests
{
    public static class AccountFactory
    {
        public static string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public static string Address = "0x12890d2cce102216644c59daE5baed380d84830c";
        public static string Password = "password";

        public static Account GetAccount()
        {
            return new Account(PrivateKey);
        }

        public static ManagedAccount GetManagedAccount()
        {
            return new ManagedAccount(Address, Password);
        }
    }


    public abstract class GethTestRunner
    {
        public abstract string ExePath { get; }
        public abstract string ChainDirectory { get; }

        public Process Process { get; private set; }

        public string Arguments { get; set; }

        private string defaultArguments =
            @" --genesis genesis_dev.json --rpc --networkid=39318 --maxpeers=0 --datadir=devChain  --rpccorsdomain ""*"" --rpcapi ""eth,web3,personal,net,miner,admin"" --ipcapi ""eth,web3,personal,net,miner,admin"" --verbosity 0 console";

        public void CleanUp()
        {
            DeleteChainDirectory("chaindata");
            DeleteChainDirectory("dapp");
            DeleteChainDirectory("nodes");
        }

        public void DeleteChainDirectory(string name)
        {
            var path = Path.Combine(ChainDirectory, name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public void StartGeth()
        {

            var args = string.IsNullOrEmpty(Arguments) ? defaultArguments : Arguments;
            ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(ExePath, "geth.exe"), args)
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
                WorkingDirectory = ExePath

            };
            Process = Process.Start(psi);
        }

        public void StopGeth()
        {
            if (Process != null && !Process.HasExited)
            {
                Process.Kill();
                Process = null;
            }
        }
    }
}