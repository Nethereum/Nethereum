using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.TokenServices.MultiAccount.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.MultiAccount
{
    public interface IMultiAccountTokenService
    {
        Task<MultiAccountScanResult> ScanAsync(
            IEnumerable<string> accounts,
            IEnumerable<long> chainIds,
            Func<long, IWeb3> web3Factory,
            IDiscoveryStrategy strategy,
            MultiAccountScanOptions options = null,
            IProgress<MultiAccountProgress> progress = null,
            CancellationToken cancellationToken = default);

        Task<MultiAccountRefreshResult> RefreshBalancesAsync(
            IEnumerable<string> accounts,
            IEnumerable<long> chainIds,
            Func<long, IWeb3> web3Factory,
            Func<string, long, ulong?> getLastScannedBlock,
            MultiAccountScanOptions options = null,
            IProgress<MultiAccountProgress> progress = null,
            CancellationToken cancellationToken = default);

        Task<MultiAccountPriceResult> RefreshPricesAsync(
            IEnumerable<(string account, long chainId, IEnumerable<TokenBalance> tokens)> accountTokens,
            string currency = "usd",
            CancellationToken cancellationToken = default);
    }
}
