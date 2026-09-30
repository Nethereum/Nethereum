using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryTransactionRepository : ITransactionRepository, INonCanonicalTransactionRepository
    {
        private readonly object _lock = new object();

        public List<ITransactionView> Records { get; set;}

        public InMemoryTransactionRepository(List<ITransactionView> records)
        {
            Records = records;
        }

        public Task<ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash)
        {
            lock (_lock)
            {
                var blockNum = (long)blockNumber.Value;
                return Task.FromResult(Records.FirstOrDefault(r => r.BlockNumber == blockNum && r.Hash == hash));
            }
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract)
        {
            var record = await FindByBlockNumberAndHashAsync(transactionReceiptVO.BlockNumber, transactionReceiptVO.TransactionHash).ConfigureAwait(false);
            lock (_lock)
            {
                if (record != null) Records.Remove(record);
                var entity = transactionReceiptVO.MapToStorageEntityForUpsert(code, failedCreatingContract);
                entity.IsCanonical = true;
                Records.Add(entity);
            }
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO)
        {
            var record = await FindByBlockNumberAndHashAsync(transactionReceiptVO.BlockNumber, transactionReceiptVO.TransactionHash).ConfigureAwait(false);
            lock (_lock)
            {
                if (record != null) Records.Remove(record);
                var entity = transactionReceiptVO.MapToStorageEntityForUpsert();
                entity.IsCanonical = true;
                Records.Add(entity);
            }
        }

        public Task UpdateRevertReasonAsync(string txHash, string revertReason)
        {
            lock (_lock)
            {
                foreach (var record in Records)
                {
                    if (record is TransactionBase tx && string.Equals(tx.Hash, txHash, System.StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrEmpty(tx.RevertReason))
                    {
                        tx.RevertReason = revertReason;
                        break;
                    }
                }
            }
            return Task.FromResult(0);
        }

        public Task MarkNonCanonicalAsync(System.Numerics.BigInteger blockNumber)
        {
            lock (_lock)
            {
                var blockNum = (long)blockNumber;
                foreach (var record in Records)
                {
                    if (record is TransactionBase tx && tx.BlockNumber == blockNum)
                    {
                        tx.IsCanonical = false;
                    }
                }
            }

            return Task.FromResult(0);
        }
    }
}
