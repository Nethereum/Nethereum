using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class ReceiptExtensions
    {
        public static int ComputeStartingLogIndex(IReadOnlyList<ReceiptInfo> receiptsInBlockOrder, int transactionIndex)
        {
            var startingLogIndex = 0;
            if (receiptsInBlockOrder == null) return startingLogIndex;

            var limit = System.Math.Min(transactionIndex, receiptsInBlockOrder.Count);
            for (var i = 0; i < limit; i++)
                startingLogIndex += receiptsInBlockOrder[i]?.Receipt?.Logs?.Count ?? 0;

            return startingLogIndex;
        }

        public static TransactionReceipt ToTransactionReceipt(
            this ReceiptInfo info,
            string from,
            string to,
            TransactionType txType = TransactionType.LegacyTransaction,
            int startingLogIndex = 0,
            System.Numerics.BigInteger? blobGasUsed = null,
            System.Numerics.BigInteger? blobGasPrice = null,
            long? blockTimestamp = null)
        {
            return new TransactionReceipt
            {
                BlobGasUsed = blobGasUsed.HasValue ? new HexBigInteger(blobGasUsed.Value) : null,
                BlobGasPrice = blobGasPrice.HasValue ? new HexBigInteger(blobGasPrice.Value) : null,
                TransactionHash = info.TxHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(info.TransactionIndex),
                BlockHash = info.BlockHash?.ToHex(true),
                BlockNumber = new HexBigInteger(info.BlockNumber),
                From = from,
                To = to,
                CumulativeGasUsed = new HexBigInteger(info.Receipt.CumulativeGasUsed),
                GasUsed = new HexBigInteger(info.GasUsed),
                EffectiveGasPrice = new HexBigInteger(info.EffectiveGasPrice),
                ContractAddress = RpcTransactionAddress.Normalize(info.ContractAddress),
                Status = info.Receipt.IsStatusReceipt ? new HexBigInteger(ResolveStatus(info.Receipt)) : null,
                Root = info.Receipt.IsStatusReceipt ? null : info.Receipt.PostStateOrStatus?.ToHex(true),
                Type = new HexBigInteger(txType.AsChainByteType()),
                LogsBloom = info.Receipt.Bloom?.ToHex(true),
                Logs = info.Receipt.Logs?.Select((log, index) => ToReceiptLog(info, log, startingLogIndex + index, blockTimestamp)).ToArray()
            };
        }

        private static FilterLog ToReceiptLog(ReceiptInfo info, Log log, int logIndex, long? blockTimestamp)
        {
            var filterLog = new FilterLog
            {
                BlockNumber = new HexBigInteger(info.BlockNumber),
                BlockTimestamp = blockTimestamp.HasValue ? new HexBigInteger(blockTimestamp.Value) : null,
                TransactionHash = info.TxHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(info.TransactionIndex),
                BlockHash = info.BlockHash?.ToHex(true),
                LogIndex = new HexBigInteger(logIndex),
                Removed = false
            };
            FilteredLogRpcMapper.SetEventFields(filterLog, log.Address, log.Topics, log.Data);
            return filterLog;
        }

        private static int ResolveStatus(Receipt receipt)
        {
            if (receipt.HasSucceeded is bool succeeded)
                return succeeded ? 1 : 0;
            return receipt.PostStateOrStatus != null && receipt.PostStateOrStatus.Length > 1 ? 1 : 0;
        }
    }
}
