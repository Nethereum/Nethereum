using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Web3;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    internal static class FunderInfrastructureConnection
    {
        public static async Task<(IWeb3 FunderWeb3, IAccountAbstractionBundlerService Bundler)> ConnectAsync(
            string nodeRpcUrl, string funderPrivateKey, string bundlerUrl, Action<string> log)
        {
            log($"Connecting to node {nodeRpcUrl}...");
            var probeWeb3 = new Nethereum.Web3.Web3(nodeRpcUrl);
            var chainId = (long)(await probeWeb3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value;

            var funderAccount = new Web3Account(funderPrivateKey, chainId);
            var funderWeb3 = new Nethereum.Web3.Web3(funderAccount, nodeRpcUrl);
            log($"Funder account {funderAccount.Address} on chain {chainId}");

            log($"Connecting to bundler {bundlerUrl}...");
            var bundler = new AccountAbstractionBundlerService(new RpcClient(new Uri(bundlerUrl)));

            return (funderWeb3, bundler);
        }
    }
}
