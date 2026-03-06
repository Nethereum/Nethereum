using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class TransactionMapping
    {
        public static void Map(this TransactionBase to, TransactionReceipt @from)
        {
            to.TransactionIndex = (long)@from.TransactionIndex.Value;
            to.GasUsed = @from.GasUsed?.Value.ToString();
            to.CumulativeGasUsed = @from.CumulativeGasUsed?.Value.ToString();
            to.HasLog = from.HasLogs();
            if (!string.IsNullOrEmpty(@from.RevertReason))
                to.RevertReason = @from.RevertReason;
            to.BlobGasUsed = @from.BlobGasUsed?.Value.ToString();
            to.BlobGasPrice = @from.BlobGasPrice?.Value.ToString();
        }

        public static void Map(this TransactionBase to, Nethereum.RPC.Eth.DTOs.Transaction @from)
        {
            to.BlockHash = @from.BlockHash;
            to.Hash = @from.TransactionHash;
            to.AddressFrom = @from.From;
            to.Value = @from.Value?.Value.ToString();
            to.AddressTo = @from.To ?? string.Empty;
            to.BlockNumber = (long)@from.BlockNumber.Value;
            to.Gas = @from.Gas?.Value.ToString();
            to.GasPrice = @from.GasPrice?.Value.ToString();
            to.Input = @from.Input ?? string.Empty;
            to.Nonce = (long)(@from.Nonce?.Value ?? 0);
            to.MaxFeePerGas = @from.MaxFeePerGas?.Value.ToString();
            to.MaxPriorityFeePerGas = @from.MaxPriorityFeePerGas?.Value.ToString();
            to.TransactionType = (long)(@from.Type?.Value ?? 0);
            to.MaxFeePerBlobGas = @from.MaxFeePerBlobGas?.Value.ToString();
        }

        public static Transaction MapToStorageEntityForUpsert(this TransactionReceiptVO transactionReceiptVO)
        {
            return transactionReceiptVO.MapToStorageEntityForUpsert<Transaction>();
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TransactionReceiptVO transactionReceiptVO) where TEntity: Transaction, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(transactionReceiptVO);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity tx, TransactionReceiptVO transactionReceiptVO) where TEntity : Transaction, new()
        {
            tx.Map(transactionReceiptVO.Transaction);
            tx.Map(transactionReceiptVO.TransactionReceipt);

            tx.NewContractAddress = transactionReceiptVO.TransactionReceipt.ContractAddress;
            tx.Failed = transactionReceiptVO.TransactionReceipt.HasErrors() ?? false;
            tx.TimeStamp = (long)(transactionReceiptVO.BlockTimestamp?.Value ?? 0);
            tx.Error = transactionReceiptVO.Error ?? string.Empty;
            tx.HasVmStack = transactionReceiptVO.HasVmStack;
            tx.EffectiveGasPrice = transactionReceiptVO.TransactionReceipt.EffectiveGasPrice?.Value.ToString();
            tx.IsCanonical = true;
            tx.UpdateRowDates();

            return tx;
        }

        public static Transaction MapToStorageEntityForUpsert(this TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract)
        {
            return transactionReceiptVO.MapToStorageEntityForUpsert<Transaction>(code, failedCreatingContract);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract) where TEntity : Transaction, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(transactionReceiptVO, code, failedCreatingContract);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity tx, TransactionReceiptVO transactionReceiptVO, string code, bool failedCreatingContract) where TEntity : Transaction, new()
        {
            tx.Map(transactionReceiptVO.Transaction);
            tx.Map(transactionReceiptVO.TransactionReceipt);

            tx.NewContractAddress = transactionReceiptVO.TransactionReceipt.ContractAddress;
            tx.Failed = failedCreatingContract;
            tx.TimeStamp = (long)(transactionReceiptVO.BlockTimestamp?.Value ?? 0);
            tx.Error = string.Empty;
            tx.HasVmStack = transactionReceiptVO.HasVmStack;
            tx.EffectiveGasPrice = transactionReceiptVO.TransactionReceipt.EffectiveGasPrice?.Value.ToString();
            tx.IsCanonical = true;

            tx.UpdateRowDates();

            return tx;
        }
    }
}
