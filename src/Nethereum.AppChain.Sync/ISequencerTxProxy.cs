using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.AppChain.Sync
{
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
