using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class ReceiptRPCMapper
    {
        public static Log ToModelLog(this FilterLog log)
        {
            var topics = new List<byte[]>();
            if (log.Topics != null)
            {
                foreach (var topic in log.Topics)
                    topics.Add(topic.ToString().HexToByteArray());
            }

            var data = string.IsNullOrEmpty(log.Data) ? new byte[0] : log.Data.HexToByteArray();
            return Log.Create(data, log.Address, topics.ToArray());
        }

        public static Receipt ToModelReceipt(this TransactionReceipt receipt)
        {
            var logs = new List<Log>();
            if (receipt.Logs != null)
            {
                foreach (var log in receipt.Logs)
                    logs.Add(log.ToModelLog());
            }

            var bloom = string.IsNullOrEmpty(receipt.LogsBloom) ? new byte[256] : receipt.LogsBloom.HexToByteArray();
            var cumulativeGasUsed = receipt.CumulativeGasUsed != null ? receipt.CumulativeGasUsed.Value : BigInteger.Zero;

            Receipt model;
            if (!string.IsNullOrEmpty(receipt.Root))
            {
                model = Receipt.CreatePostStateReceipt(receipt.Root.HexToByteArray(), cumulativeGasUsed, bloom, logs);
            }
            else
            {
                var success = receipt.Status != null && receipt.Status.Value == BigInteger.One;
                model = Receipt.CreateStatusReceipt(success, cumulativeGasUsed, bloom, logs);
            }

            // The receipts root is computed over the typed encoding, so a receipt mapped without
            // its type can no longer reproduce the root its block committed to. A node that omits
            // the field, or a pre-EIP-2718 receipt, is type 0 - which is what this defaulted to
            // for every receipt before.
            model.TransactionType = receipt.Type != null ? (byte)receipt.Type.Value : (byte)0;
            return model;
        }

        public static List<Receipt> ToModelReceipts(this IEnumerable<TransactionReceipt> receipts)
        {
            var result = new List<Receipt>();
            if (receipts == null) return result;
            foreach (var receipt in receipts)
                result.Add(receipt.ToModelReceipt());
            return result;
        }
    }
}
