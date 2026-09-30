using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Example.Core;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed class DevChainFaucet : IDevChainFaucet
    {
        private readonly InProcessBundlerHost _bootstrap;

        public DevChainFaucet(InProcessBundlerHost bootstrap)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        }

        public Task FundAsync(string address, decimal ether = 10m) =>
            _bootstrap.Node.SetBalanceAsync(address, Nethereum.Web3.Web3.Convert.ToWei(ether));

        public async Task<BigInteger> GetBalanceAsync(string address) =>
            (await _bootstrap.OperatorWeb3.Eth.GetBalance.SendRequestAsync(address).ConfigureAwait(false)).Value;
    }
}
