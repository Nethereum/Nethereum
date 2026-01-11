using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthNewFilterHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_newFilter.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterInput = GetJsonElement(request, 0);
            var filter = await ParseFilterAsync(filterInput, context);
            var currentBlock = await context.Node.GetBlockNumberAsync();
            var filterId = context.Node.Filters.CreateLogFilter(filter, currentBlock);
            return Success(request.Id, filterId);
        }

        private async Task<LogFilter> ParseFilterAsync(JsonElement input, RpcContext context)
        {
            var filter = new LogFilter();

            if (input.TryGetProperty("fromBlock", out var fromBlockElement))
            {
                filter.FromBlock = await ParseBlockNumberAsync(fromBlockElement, context);
            }

            if (input.TryGetProperty("toBlock", out var toBlockElement))
            {
                filter.ToBlock = await ParseBlockNumberAsync(toBlockElement, context);
            }

            if (input.TryGetProperty("address", out var addressElement))
            {
                filter.Addresses = ParseAddresses(addressElement);
            }

            if (input.TryGetProperty("topics", out var topicsElement))
            {
                filter.Topics = ParseTopics(topicsElement);
            }

            return filter;
        }

        private async Task<BigInteger?> ParseBlockNumberAsync(JsonElement element, RpcContext context)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return null;

            var value = element.GetString();
            if (string.IsNullOrEmpty(value))
                return null;

            if (value == BlockParameter.BlockParameterType.latest.ToString() || value == BlockParameter.BlockParameterType.pending.ToString())
            {
                return await context.Node.GetBlockNumberAsync();
            }

            if (value == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return 0;
            }

            if (value.StartsWith("0x"))
            {
                return value.HexToBigInteger(false);
            }

            return BigInteger.Parse(value);
        }

        private List<string> ParseAddresses(JsonElement element)
        {
            var addresses = new List<string>();

            if (element.ValueKind == JsonValueKind.String)
            {
                addresses.Add(element.GetString());
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        addresses.Add(item.GetString());
                    }
                }
            }

            return addresses;
        }

        private List<List<byte[]>> ParseTopics(JsonElement element)
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
                    {
                        if (subItem.ValueKind == JsonValueKind.String)
                        {
                            topicList.Add(subItem.GetString().HexToByteArray());
                        }
                    }
                    topics.Add(topicList);
                }
            }

            return topics;
        }
    }
}
