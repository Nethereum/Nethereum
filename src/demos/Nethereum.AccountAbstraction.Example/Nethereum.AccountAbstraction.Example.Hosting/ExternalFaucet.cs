using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Core;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed class ExternalFaucet : IDevChainFaucet
    {
        private readonly IWeb3 _funderWeb3;

        public ExternalFaucet(IWeb3 funderWeb3)
        {
            _funderWeb3 = funderWeb3 ?? throw new ArgumentNullException(nameof(funderWeb3));
        }

        public Task FundAsync(string address, decimal ether = 10m) =>
            _funderWeb3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(address, ether);

        public async Task<BigInteger> GetBalanceAsync(string address) =>
            (await _funderWeb3.Eth.GetBalance.SendRequestAsync(address).ConfigureAwait(false)).Value;
    }
}
