using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Rpc
{
    internal static class LogFilterParser
    {
        public static async Task<LogFilter> ParseAsync(JsonElement input, RpcContext context)
        {
            input.RequireRpcObject("filter");
            var filter = new LogFilter();

            if (input.TryGetProperty("fromBlock", out var fromBlock))
                filter.FromBlock = await ParseBoundAsync(fromBlock, context);

            if (input.TryGetProperty("toBlock", out var toBlock))
                filter.ToBlock = await ParseBoundAsync(toBlock, context);

            if (input.TryGetProperty("address", out var address))
                filter.Addresses = ParseAddresses(address);

            if (input.TryGetProperty("topics", out var topics))
                filter.Topics = ParseTopics(topics);

            if (input.TryGetProperty("blockHash", out var blockHashEl) && blockHashEl.ValueKind != JsonValueKind.Null)
            {
                var blockHash = blockHashEl.RequireRpcString("blockHash");
                if (filter.FromBlock.HasValue || filter.ToBlock.HasValue)
                    throw RpcException.InvalidParams("cannot specify both blockHash and fromBlock/toBlock");

                var header = await context.Node.GetBlockByHashAsync(blockHash.HexToByteArray());
                if (header == null)
                    throw RpcException.InvalidParams("blockHash not found");

                var number = (BigInteger)header.BlockNumber.ToULong();
                filter.FromBlock = number;
                filter.ToBlock = number;
            }

            LogFilterLimits.Enforce(filter, context.Node.Config.RpcLogQueryLimit);
            return filter;
        }

        private static async Task<BigInteger?> ParseBoundAsync(JsonElement element, RpcContext context)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return null;

            var value = element.RequireRpcString("block bound");
            if (string.IsNullOrEmpty(value))
                return null;

            var named = await BlockTagResolver.TryResolveNamedAsync(value, context);
            if (named.HasValue)
                return named;

            return value.StartsWith("0x") ? value.HexToBigInteger(false) : BigInteger.Parse(value);
        }

        private static List<string> ParseAddresses(JsonElement element)
        {
            var addresses = new List<string>();

            if (element.ValueKind == JsonValueKind.Null)
            {
                return addresses;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                addresses.Add(element.GetRpcString());
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    addresses.Add(item.RequireRpcString("address"));
            }
            else
            {
                throw RpcException.InvalidParams("address must be a string or an array of strings");
            }

            return addresses;
        }

        private static List<List<byte[]>> ParseTopics(JsonElement element)
        {
            var topics = new List<List<byte[]>>();

            if (element.ValueKind == JsonValueKind.Null)
                return topics;

            if (element.ValueKind != JsonValueKind.Array)
                throw RpcException.InvalidParams("topics must be an array");

            foreach (var item in element.EnumerateArray())
            {
                var topicList = new List<byte[]>();

                if (item.ValueKind == JsonValueKind.Null)
                {
                    topics.Add(topicList);
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    topicList.Add(item.GetRpcString().HexToByteArray());
                    topics.Add(topicList);
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    foreach (var subItem in item.EnumerateArray())
                        topicList.Add(subItem.RequireRpcString("topic").HexToByteArray());
                    topics.Add(topicList);
                }
                else
                {
                    throw RpcException.InvalidParams("a topic must be null, a string or an array of strings");
                }
            }

            return topics;
        }
    }
}
