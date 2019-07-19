using System.Linq;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class TransactionLogMapping
    {
        public static TransactionLog MapToStorageEntityForUpsert(this FilterLogVO filterLog)
        {
            return new TransactionLog().MapToStorageEntityForUpsert(filterLog);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this FilterLogVO filterLog) where TEntity : TransactionLog, new()
        {
            var logEntity = new TEntity();
            return logEntity.MapToStorageEntityForUpsert(filterLog);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity logEntity, FilterLogVO filterLog) where TEntity : TransactionLog
        {
            logEntity.Map(filterLog.Log);
            logEntity.UpdateRowDates();

            return logEntity;
        }

        public static void Map(this TransactionLog transactionLog, FilterLog log)
        {
            transactionLog.TransactionHash = log.TransactionHash;
            transactionLog.LogIndex = log.LogIndex.Value.ToString();
            transactionLog.Address = log.Address;
            transactionLog.Data = log.Data;

            transactionLog.EventHash = log.EventSignature();
            transactionLog.IndexVal1 = log.IndexedVal1();
            transactionLog.IndexVal2 = log.IndexedVal2();
            transactionLog.IndexVal3 = log.IndexedVal3();
        }

        /// <summary>
        /// Create a partially populated FilterLog from the data stored in the view
        /// The view does not contain all fields in 
        /// </summary>
        public static FilterLog ToFilterLog(this ITransactionLogView transactionLogView)
        {
            return new FilterLog
            {
                TransactionHash = transactionLogView.TransactionHash,
                Address = transactionLogView.Address,
                Data = transactionLogView.Data,
                LogIndex = new Hex.HexTypes.HexBigInteger(transactionLogView.LogIndex),
                Topics = new[] {transactionLogView.EventHash, 
                                transactionLogView.IndexVal1, 
                                transactionLogView.IndexVal2, 
                                transactionLogView.IndexVal3}
                    .Where(s =>! string.IsNullOrEmpty(s)).ToArray()
            };
        }
    }
}
