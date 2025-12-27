using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;
using Nethereum.Web3;

namespace Nethereum.TokenServices.ERC20.Events
{
    public interface ITokenEventScanner
    {
        Task<TokenEventScanResult> ScanTransferEventsAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default);

        Task<List<string>> GetAffectedTokenAddressesAsync(
            IWeb3 web3,
            string accountAddress,
            BigInteger fromBlock,
            BigInteger? toBlock = null,
            CancellationToken cancellationToken = default);
    }

    public class TokenEventScanResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<TokenTransferEvent> Transfers { get; set; } = new();
        public List<string> AffectedTokenAddresses { get; set; } = new();
        public BigInteger? LatestBlockScanned { get; set; }
    }

    public class TokenTransferEvent
    {
        public string TokenAddress { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger BlockNumber { get; set; }
        public string TransactionHash { get; set; }
        public bool IsIncoming { get; set; }
    }
}
