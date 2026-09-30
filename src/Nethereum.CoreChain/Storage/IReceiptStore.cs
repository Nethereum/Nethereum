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

        async Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ReceiptSaveItem> items)
        {
            if (items == null) return;
            foreach (var it in items)
                await SaveAsync(it.Receipt, it.TxHash, blockHash, blockNumber, it.TxIndex,
                    it.GasUsed, it.ContractAddress, it.EffectiveGasPrice).ConfigureAwait(false);
        }

        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }

    public readonly struct ReceiptSaveItem
    {
        public ReceiptSaveItem(Receipt receipt, byte[] txHash, int txIndex,
            BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            Receipt = receipt;
            TxHash = txHash;
            TxIndex = txIndex;
            GasUsed = gasUsed;
            ContractAddress = contractAddress;
            EffectiveGasPrice = effectiveGasPrice;
        }

        public Receipt Receipt { get; }
        public byte[] TxHash { get; }
        public int TxIndex { get; }
        public BigInteger GasUsed { get; }
        public string ContractAddress { get; }
        public BigInteger EffectiveGasPrice { get; }
    }
}
