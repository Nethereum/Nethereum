using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public interface ITxPool
    {
        int PendingCount { get; }
        Task<int> GetPendingCountAsync();
        Task<byte[]> AddAsync(ISignedTransaction transaction);
        Task<ISignedTransaction> GetByHashAsync(byte[] txHash);
        Task<bool> RemoveAsync(byte[] txHash);
        Task<int> RemoveBatchAsync(IEnumerable<byte[]> txHashes);
        Task<bool> ContainsAsync(byte[] txHash);
        Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount);
        Task ClearAsync();
        Task<BigInteger> GetPendingNonceAsync(string senderAddress, BigInteger confirmedNonce);
        void TrackPendingNonce(string senderAddress, BigInteger nonce);
        void ResetPendingNonces();
        int GetSenderTxCount(string senderAddress);
        void IncrementSenderTxCount(string senderAddress);
        int MaxTxsPerSender { get; }
    }

    public interface ITxPoolOrderingStrategy
    {
        IEnumerable<PendingTransaction> Order(IEnumerable<PendingTransaction> transactions);
    }

    public class PendingTransaction
    {
        public ISignedTransaction Transaction { get; set; }
        public byte[] TxHash { get; set; }
        public System.DateTime ReceivedAt { get; set; }
    }
}
