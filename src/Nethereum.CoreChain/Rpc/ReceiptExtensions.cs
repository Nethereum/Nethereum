using System.Linq;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class ReceiptExtensions
    {
        public static TransactionReceipt ToTransactionReceipt(this ReceiptInfo info, string from, string to)
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
                Status = new HexBigInteger(info.Receipt.HasSucceeded == true ? 1 : 0),
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
                    LogIndex = new HexBigInteger(index),
                    Removed = false
                }).ToArray()
            };
        }
    }
}
