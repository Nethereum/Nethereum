using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class TransactionMapping
    {
        public static void Map(this TransactionBase to, TransactionReceipt @from)
        {
            to.TransactionIndex = (long)@from.TransactionIndex.Value;
            to.GasUsed = (long)@from.GasUsed.Value;
            to.CumulativeGasUsed = (long)@from.CumulativeGasUsed.Value;
            to.HasLog = @from.Logs.Count > 0;
        }

        public static void Map(this TransactionBase to, Nethereum.RPC.Eth.DTOs.Transaction @from)
        {
            to.BlockHash = @from.BlockHash;
            to.Hash = @from.TransactionHash;
            to.AddressFrom = @from.From;
            to.Value = @from.Value.Value.ToString();
            to.AddressTo = @from.To ?? string.Empty;
            to.BlockNumber = @from.BlockNumber.Value.ToString();
            to.Gas = (long)@from.Gas.Value;
            to.GasPrice = (long)@from.GasPrice.Value;
            to.Input = @from.Input ?? string.Empty;
            to.Nonce = (long)@from.Nonce.Value;
        }

        public static Transaction MapToStorageEntityForUpsert(this TransactionReceiptVO transactionReceiptVO)
        {
            return transactionReceiptVO.MapToStorageEntityForUpsert<Transaction>();
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TransactionReceiptVO transactionReceiptVO) where TEntity: Transaction, new()
        {
            var tx = new TEntity();

            tx.Map(transactionReceiptVO.Transaction);
            tx.Map(transactionReceiptVO.TransactionReceipt);

            tx.Failed = transactionReceiptVO.TransactionReceipt.HasErrors() ?? false;
            tx.TimeStamp = transactionReceiptVO.BlockTimestamp.ToLong();
            tx.Error = transactionReceiptVO.Error ?? string.Empty;
            tx.HasVmStack = transactionReceiptVO.HasVmStack;

            tx.UpdateRowDates();

            return tx;
        }

        public static Transaction MapToStorageEntityForUpsert(this TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract)
        {
            return transactionReceiptVO.MapToStorageEntityForUpsert<Transaction>(code, failedCreatingContract);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract) where TEntity : Transaction, new()
        {
            var tx = new TEntity();

            tx.Map(transactionReceiptVO.Transaction);
            tx.Map(transactionReceiptVO.TransactionReceipt);

            tx.NewContractAddress = transactionReceiptVO.TransactionReceipt.ContractAddress;
            tx.Failed = failedCreatingContract;
            tx.TimeStamp = transactionReceiptVO.BlockTimestamp.ToLong();
            tx.Error = string.Empty;
            tx.HasVmStack = transactionReceiptVO.HasVmStack;

            tx.UpdateRowDates();

            return tx;
        }
    }
}
