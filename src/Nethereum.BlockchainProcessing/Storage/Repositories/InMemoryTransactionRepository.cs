using Nethereum.BlockchainProcessing.Storage.Entities;
using Nethereum.BlockchainProcessing.Storage.Entities.Mapping;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public class InMemoryTransactionRepository : ITransactionRepository
    {
        public List<ITransactionView> Records { get; set;}

        public InMemoryTransactionRepository(List<ITransactionView> records)
        {
            Records = records;
        }

        public Task<ITransactionView> FindByBlockNumberAndHashAsync(HexBigInteger blockNumber, string hash)
        {
            return Task.FromResult(Records.FirstOrDefault(r => r.BlockNumber == blockNumber.Value.ToString() && r.Hash == hash));
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract)
        {
            var record = await FindByBlockNumberAndHashAsync(transactionReceiptVO.BlockNumber, transactionReceiptVO.TransactionHash);
            if(record != null ) Records.Remove(record);
            Records.Add(transactionReceiptVO.MapToStorageEntityForUpsert(code, failedCreatingContract));
        }

        public async Task UpsertAsync(TransactionReceiptVO transactionReceiptVO)
        {
            var record = await FindByBlockNumberAndHashAsync(transactionReceiptVO.BlockNumber, transactionReceiptVO.TransactionHash);
            if (record != null) Records.Remove(record);
            Records.Add(transactionReceiptVO.MapToStorageEntityForUpsert());
        }
    }
}
