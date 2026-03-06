using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Linq;
using System.Numerics;

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
            transactionLog.LogIndex = (long)(log.LogIndex?.Value ?? 0);
            transactionLog.Address = log.Address;
            transactionLog.Data = log.Data;
            transactionLog.BlockNumber = (long)(log.BlockNumber?.Value ?? 0);
            transactionLog.BlockHash = log.BlockHash;
            transactionLog.IsCanonical = true;

            transactionLog.EventHash = log.EventSignature();
            transactionLog.IndexVal1 = log.IndexedVal1();
            transactionLog.IndexVal2 = log.IndexedVal2();
            transactionLog.IndexVal3 = log.IndexedVal3();
        }

        public static FilterLog ToFilterLog(this ITransactionLogView transactionLogView)
        {
            var filterLog = new FilterLog
            {
                TransactionHash = transactionLogView.TransactionHash,
                Address = transactionLogView.Address,
                Data = transactionLogView.Data,
                LogIndex = new Hex.HexTypes.HexBigInteger(new BigInteger(transactionLogView.LogIndex)),
                BlockHash = transactionLogView.BlockHash,
                BlockNumber = new Hex.HexTypes.HexBigInteger(new BigInteger(transactionLogView.BlockNumber)),
                Topics = TrimTrailingNulls(new[] {transactionLogView.EventHash,
                                transactionLogView.IndexVal1,
                                transactionLogView.IndexVal2,
                                transactionLogView.IndexVal3})
            };

            return filterLog;
        }

        private static string[] TrimTrailingNulls(string[] topics)
        {
            int last = topics.Length - 1;
            while (last >= 0 && string.IsNullOrEmpty(topics[last]))
                last--;
            if (last < 0) return new string[0];
            var result = new string[last + 1];
            Array.Copy(topics, result, last + 1);
            return result;
        }
    }
}
