using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface IBlockSource
    {
        IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            CancellationToken ct);

        Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct);

        Task ReportBadBundleAsync(
            ulong blockNumber,
            BadBundleReason reason,
            CancellationToken ct);

        DivergenceSignal LastChainBreak { get; }
    }

    public sealed record DivergenceSignal(
        ulong AtBlock,
        byte[] PeerParentHash,
        byte[] OurParentHash,
        int QuorumPeerCount,
        string SourceName,
        BlockHeader IncomingHeader = null,
        byte[] IncomingHash = null,
        string SourcePeerNodeId = null,
        IReadOnlyList<ISignedTransaction> IncomingTransactions = null);

    public enum BlockSourceHealth { Healthy, Degraded, Unavailable }

    public enum BadBundleReason
    {
        WrongParent,
        StateRootMismatch,
        TxValidationFailed,
        MalformedRlp,
    }
}
