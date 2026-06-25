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
        public static TransactionReceipt ToTransactionReceipt(
            this ReceiptInfo info,
            string from,
            string to,
            TransactionType txType = TransactionType.LegacyTransaction,
            int startingLogIndex = 0)
        {
            return new TransactionReceipt
            {
                TransactionHash = info.TxHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(info.TransactionIndex),
                BlockHash = info.BlockHash?.ToHex(true),
                BlockNumber = new HexBigInteger(info.BlockNumber),
                From = from,
                To = to,
                CumulativeGasUsed = new HexBigInteger(info.Receipt.CumulativeGasUsed),
                GasUsed = new HexBigInteger(info.GasUsed),
                EffectiveGasPrice = new HexBigInteger(info.EffectiveGasPrice),
                ContractAddress = info.ContractAddress,
                Status = new HexBigInteger(ResolveStatus(info.Receipt)),
                Type = new HexBigInteger(RpcTypeByte(txType)),
                LogsBloom = info.Receipt.Bloom?.ToHex(true),
                Logs = info.Receipt.Logs?.Select((log, index) => new FilterLog
                {
                    Address = log.Address,
                    Topics = log.Topics?.Select(t => (object)t.ToHex(true)).ToArray(),
                    Data = log.Data?.ToHex(true) ?? "0x",
                    BlockNumber = new HexBigInteger(info.BlockNumber),
                    TransactionHash = info.TxHash?.ToHex(true),
                    TransactionIndex = new HexBigInteger(info.TransactionIndex),
                    BlockHash = info.BlockHash?.ToHex(true),
                    LogIndex = new HexBigInteger(startingLogIndex + index),
                    Removed = false
                }).ToArray()
            };
        }

        // Execution clients synthesise status=1 for pre-Byzantium successful txs even though
        // the canonical receipt carries a post-state root rather than a status byte.
        // Heuristic mirrors theirs: a non-empty post-state-root signals successful execution.
        private static int ResolveStatus(Receipt receipt)
        {
            if (receipt.HasSucceeded is bool succeeded)
                return succeeded ? 1 : 0;
            return receipt.PostStateOrStatus != null && receipt.PostStateOrStatus.Length > 1 ? 1 : 0;
        }

        // RPC type byte: legacy (and pre-EIP-2718) → 0; typed txs → their EIP byte.
        private static int RpcTypeByte(TransactionType txType) => txType switch
        {
            TransactionType.LegacyTransaction => 0,
            TransactionType.LegacyChainTransaction => 0,
            _ => (int)txType
        };
    }
}
