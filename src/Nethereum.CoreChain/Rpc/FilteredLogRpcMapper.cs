using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class FilteredLogRpcMapper
    {
        public static FilterLog ToRpcFilterLog(this FilteredLog log)
        {
            var filterLog = new FilterLog
            {
                BlockNumber = new HexBigInteger(log.BlockNumber),
                TransactionHash = log.TransactionHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(log.TransactionIndex),
                BlockHash = log.BlockHash?.ToHex(true),
                LogIndex = new HexBigInteger(log.LogIndex),
                Removed = log.Removed
            };
            SetEventFields(filterLog, log.Address, log.Topics, log.Data);
            return filterLog;
        }

        public static void SetEventFields(FilterLog target, string address, IEnumerable<byte[]> topics, byte[] data)
        {
            target.Address = RpcTransactionAddress.Normalize(address);
            target.Topics = topics != null ? topics.Select(t => (object)t.ToHex(true)).ToArray() : Array.Empty<object>();
            target.Data = data?.ToHex(true) ?? "0x";
        }
        public static async Task FillBlockTimestampsAsync(System.Collections.Generic.IReadOnlyList<FilterLog> logs, Nethereum.CoreChain.IChainNode node)
        {
            var cache = new System.Collections.Generic.Dictionary<BigInteger, HexBigInteger>();
            foreach (var log in logs)
            {
                if (log.BlockNumber == null) continue;
                var blockNumber = log.BlockNumber.Value;
                if (!cache.TryGetValue(blockNumber, out var timestamp))
                {
                    var header = await node.GetBlockByNumberAsync(blockNumber).ConfigureAwait(false);
                    timestamp = header != null ? new HexBigInteger(header.Timestamp) : null;
                    cache[blockNumber] = timestamp;
                }
                log.BlockTimestamp = timestamp;
            }
        }
    }
}
