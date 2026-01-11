using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IReceiptStore
    {
        Task<Receipt> GetByTxHashAsync(byte[] txHash);
        Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash);
        Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash);
        Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber);
        Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice);
    }
}
