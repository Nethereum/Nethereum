using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface ITransactionStore
    {
        Task<ISignedTransaction> GetByHashAsync(byte[] txHash);
        Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash);
        Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber);
        Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex);
        Task<TransactionLocation> GetLocationAsync(byte[] txHash);
    }

    public class TransactionLocation
    {
        public byte[] BlockHash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public int TransactionIndex { get; set; }
    }
}
