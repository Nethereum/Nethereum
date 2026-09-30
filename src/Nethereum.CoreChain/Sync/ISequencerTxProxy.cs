using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Sync
{
    [System.Obsolete("The HTTP sequencer proxy was deleted with the legacy AppChain sync; a node forwards transactions over devp2p. This interface has no implementer and will be removed.")]
    public interface ISequencerTxProxy
    {
        Task<byte[]> SendRawTransactionAsync(byte[] rawTransaction, CancellationToken cancellationToken = default);

        Task<ReceiptInfo?> WaitForReceiptAsync(
            byte[] txHash,
            int timeoutMs = 30000,
            int pollIntervalMs = 500,
            CancellationToken cancellationToken = default);

        Task<ReceiptInfo?> GetTransactionReceiptAsync(byte[] txHash, CancellationToken cancellationToken = default);
    }
}
