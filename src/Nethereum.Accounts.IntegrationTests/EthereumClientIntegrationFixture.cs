using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [CollectionDefinition(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthereumClientFixtureCollection : ICollectionFixture<EthereumClientIntegrationFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class EthereumClientIntegrationFixture : IDisposable
    {
        public const string ETHEREUM_CLIENT_COLLECTION_DEFAULT = "Ethereum client Test";
        private Process _process;

        public EthereumClientIntegrationFixture()
        {
            //TODO:
            //Load configuration from file.
            //The file can be at the Solution level.
            //1, Path
            //2, Executable
            //3, Arguments.
            // So the tests can run for both Geth and Parity, Windows, Mac and Linux.

            var location = typeof(EthereumClientIntegrationFixture).GetTypeInfo().Assembly.Location;
            var dirPath = Path.GetDirectoryName(location);
            var path = Path.GetFullPath(Path.Combine(dirPath, @"..\..\..\..\..\testchain\clique"));

            ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(path, "geth.exe"), @" --nodiscover --rpc --datadir=devChain  --rpccorsdomain "" * "" --mine --rpcapi ""eth, web3, personal, net, miner, admin, debug"" --rpcaddr ""0.0.0.0"" --unlock 0x12890d2cce102216644c59daE5baed380d84830c --password ""pass.txt"" --verbosity 0 console  ")
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
                WorkingDirectory = path

            };
           _process = Process.Start(psi);
        }

        public void Dispose()
        {
            _process.Kill();
        }

       
    }
}
