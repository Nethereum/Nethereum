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
            var filter = new LogFilter();

            if (input.TryGetProperty("fromBlock", out var fromBlock))
                filter.FromBlock = await ParseBoundAsync(fromBlock, context);

            if (input.TryGetProperty("toBlock", out var toBlock))
                filter.ToBlock = await ParseBoundAsync(toBlock, context);

            if (input.TryGetProperty("address", out var address))
                filter.Addresses = ParseAddresses(address);

            if (input.TryGetProperty("topics", out var topics))
                filter.Topics = ParseTopics(topics);

            if (input.TryGetProperty("blockHash", out var blockHashEl) && blockHashEl.ValueKind == JsonValueKind.String)
            {
                if (filter.FromBlock.HasValue || filter.ToBlock.HasValue)
                    throw RpcException.InvalidParams("cannot specify both blockHash and fromBlock/toBlock");

                var header = await context.Node.GetBlockByHashAsync(blockHashEl.GetString().HexToByteArray());
                if (header == null)
                    throw RpcException.InvalidParams("blockHash not found");

                var number = (BigInteger)header.BlockNumber.ToULong();
                filter.FromBlock = number;
                filter.ToBlock = number;
            }

            return filter;
        }

        private static async Task<BigInteger?> ParseBoundAsync(JsonElement element, RpcContext context)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return null;

            var value = element.GetString();
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

            if (element.ValueKind == JsonValueKind.String)
            {
                addresses.Add(element.GetString());
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String)
                        addresses.Add(item.GetString());
            }

            return addresses;
        }

        private static List<List<byte[]>> ParseTopics(JsonElement element)
        {
            var topics = new List<List<byte[]>>();

            if (element.ValueKind != JsonValueKind.Array)
                return topics;

            foreach (var item in element.EnumerateArray())
            {
                var topicList = new List<byte[]>();

                if (item.ValueKind == JsonValueKind.Null)
                {
                    topics.Add(topicList);
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    topicList.Add(item.GetString().HexToByteArray());
                    topics.Add(topicList);
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    foreach (var subItem in item.EnumerateArray())
                        if (subItem.ValueKind == JsonValueKind.String)
                            topicList.Add(subItem.GetString().HexToByteArray());
                    topics.Add(topicList);
                }
            }

            return topics;
        }
    }
}
