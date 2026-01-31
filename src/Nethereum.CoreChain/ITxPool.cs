using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public interface ITxPool
    {
        int PendingCount { get; }
        Task<byte[]> AddAsync(ISignedTransaction transaction);
        Task<ISignedTransaction> GetByHashAsync(byte[] txHash);
        Task<bool> RemoveAsync(byte[] txHash);
        Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount);
        Task ClearAsync();
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
