using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.CoreChain.Models;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.Rpc
{
    public abstract class RpcHandlerBase : IRpcHandler
    {
        public abstract string MethodName { get; }

        public abstract Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context);

        protected static RpcResponseMessage Success(object id, object result)
        {
            return new RpcResponseMessage(id, result);
        }

        protected static RpcResponseMessage Error(object id, int code, string message, object data = null)
        {
            return new RpcResponseMessage(id, new RpcError
            {
                Code = code,
                Message = message,
                Data = data
            });
        }

        protected static T GetParam<T>(RpcRequestMessage request, int index)
        {
            var rawParams = request.RawParameters;

            if (rawParams == null)
                throw RpcException.InvalidParams($"Missing parameter at index {index}");

            if (rawParams is object[] array)
            {
                if (index >= array.Length)
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                return ConvertValue<T>(array[index]);
            }

            if (rawParams is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != JsonValueKind.Array)
                    throw RpcException.InvalidParams("Parameters must be an array");

                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                return DeserializeJsonElement<T>(jsonElement[index]);
            }

            throw RpcException.InvalidParams("Parameters must be an array");
        }

        protected static T GetOptionalParam<T>(RpcRequestMessage request, int index, T defaultValue = default(T))
        {
            var rawParams = request.RawParameters;

            if (rawParams == null)
                return defaultValue;

            if (rawParams is object[] array)
            {
                if (index >= array.Length)
                    return defaultValue;

                return ConvertValue<T>(array[index]);
            }

            if (rawParams is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != JsonValueKind.Array)
                    return defaultValue;

                if (index >= jsonElement.GetArrayLength())
                    return defaultValue;

                var element = jsonElement[index];
                if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                    return defaultValue;

                return DeserializeJsonElement<T>(element);
            }

            return defaultValue;
        }

        protected static int GetParamCount(RpcRequestMessage request)
        {
            var rawParams = request.RawParameters;

            if (rawParams == null)
                return 0;

            if (rawParams is object[] array)
                return array.Length;

            if (rawParams is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                return jsonElement.GetArrayLength();

            return 0;
        }

        protected static JsonElement GetJsonElement(RpcRequestMessage request, int index)
        {
            var rawParams = request.RawParameters;

            if (rawParams is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                return jsonElement[index];
            }

            if (rawParams is object[] array)
            {
                if (index >= array.Length)
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                var item = array[index];
                if (item is JsonElement elementItem)
                    return elementItem;

                var json = JsonConvert.SerializeObject(item);
                return JsonDocument.Parse(json).RootElement;
            }

            throw RpcException.InvalidParams("Parameters must be a JSON array");
        }

        private static readonly JsonSerializerOptions AotOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = CoreChainJsonContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static T DeserializeJsonElement<T>(JsonElement element)
        {
            var targetType = typeof(T);

            if (targetType == typeof(string))
                return (T)(object)element.GetString();

            if (targetType == typeof(bool))
                return (T)(object)element.GetBoolean();

            if (targetType == typeof(int))
                return (T)(object)element.GetInt32();

            if (targetType == typeof(long))
                return (T)(object)element.GetInt64();

            if (targetType == typeof(double))
                return (T)(object)element.GetDouble();

            if (targetType == typeof(JsonElement))
                return (T)(object)element;

            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText(), AotOptions);
        }

        private static T ConvertValue<T>(object value)
        {
            if (value == null)
                return default(T);

            if (value is T typed)
                return typed;

            if (value is JsonElement element)
                return DeserializeJsonElement<T>(element);

            var targetType = typeof(T);

            // For string target, convert via ToString or get string property
            if (targetType == typeof(string))
            {
                // Try to call GetRPCParam if it's a BlockParameter
                var getRPCParamMethod = value.GetType().GetMethod("GetRPCParam");
                if (getRPCParamMethod != null)
                {
                    var rpcParam = getRPCParamMethod.Invoke(value, null);
                    return (T)(object)rpcParam?.ToString();
                }
                // Try to get HexValue property if it's a HexBigInteger or similar
                var hexValueProperty = value.GetType().GetProperty("HexValue");
                if (hexValueProperty != null)
                {
                    var hexValue = hexValueProperty.GetValue(value);
                    return (T)(object)hexValue?.ToString();
                }
                return (T)(object)value.ToString();
            }

            // For primitive types that implement IConvertible
            if (targetType.IsPrimitive || targetType == typeof(decimal))
            {
                return (T)Convert.ChangeType(value, targetType);
            }

            // Handle Newtonsoft.Json JToken types (JObject, JArray, etc.)
            if (value is JToken jToken)
            {
                var json = jToken.ToString(Formatting.None);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json, AotOptions);
            }

            // For complex types, serialize to JSON then deserialize
            try
            {
                // Use default serializer for serialization (handles anonymous types and unknown types)
                // then use AOT context for deserialization to the known target type
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json, AotOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert {value?.GetType().Name} to {targetType.Name}: {ex.Message}", ex);
            }
        }

        protected static string ToHex(int value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(long value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(ulong value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(BigInteger value) => value.ToHex(false);
        protected static string ToHex(byte[] data) => data.ToHex(true);

        protected static async Task<BigInteger> ResolveBlockNumberAsync(string blockTag, RpcContext context)
        {
            if (blockTag == BlockParameter.BlockParameterType.latest.ToString() ||
                blockTag == BlockParameter.BlockParameterType.pending.ToString() ||
                blockTag == BlockParameter.BlockParameterType.safe.ToString() ||
                blockTag == BlockParameter.BlockParameterType.finalized.ToString())
            {
                return await context.Node.GetBlockNumberAsync();
            }

            if (blockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return 0;
            }

            return blockTag.HexToBigInteger(false);
        }

        protected static async Task<LogFilter> ParseLogFilterAsync(JsonElement input, RpcContext context)
        {
            var filter = new LogFilter();

            if (input.TryGetProperty("fromBlock", out var fromBlockElement))
            {
                filter.FromBlock = await ParseFilterBlockNumberAsync(fromBlockElement, context);
            }

            if (input.TryGetProperty("toBlock", out var toBlockElement))
            {
                filter.ToBlock = await ParseFilterBlockNumberAsync(toBlockElement, context);
            }

            if (input.TryGetProperty("address", out var addressElement))
            {
                filter.Addresses = ParseFilterAddresses(addressElement);
            }

            if (input.TryGetProperty("topics", out var topicsElement))
            {
                filter.Topics = ParseFilterTopics(topicsElement);
            }

            return filter;
        }

        protected static async Task<BigInteger?> ParseFilterBlockNumberAsync(JsonElement element, RpcContext context)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return null;

            var value = element.GetString();
            if (string.IsNullOrEmpty(value))
                return null;

            if (value == BlockParameter.BlockParameterType.latest.ToString() ||
                value == BlockParameter.BlockParameterType.pending.ToString() ||
                value == BlockParameter.BlockParameterType.safe.ToString() ||
                value == BlockParameter.BlockParameterType.finalized.ToString())
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

        internal static List<string> ParseFilterAddresses(JsonElement element)
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

        internal static List<List<byte[]>> ParseFilterTopics(JsonElement element)
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

        protected static string GetSenderAddress(ISignedTransaction tx)
        {
            try
            {
                var signature = tx.Signature;
                if (signature == null) return null;

                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key?.GetPublicAddress();
            }
            catch
            {
                return null;
            }
        }

        protected static string GetReceiverAddress(ISignedTransaction tx)
        {
            if (tx == null) return null;

            return tx switch
            {
                LegacyTransaction legacy => legacy.ReceiveAddress is { Length: > 0 } addr ? addr.ToHex(true) : null,
                LegacyTransactionChainId legacyChainId => legacyChainId.ReceiveAddress is { Length: > 0 } addr ? addr.ToHex(true) : null,
                Transaction1559 eip1559 => eip1559.ReceiverAddress,
                Transaction2930 eip2930 => eip2930.ReceiverAddress,
                Transaction7702 eip7702 => eip7702.ReceiverAddress,
                _ => null
            };
        }

        protected static FilterLog ConvertToRpcLog(FilteredLog log)
        {
            return new FilterLog
            {
                Address = log.Address,
                Topics = log.Topics?.Select(t => (object)t.ToHex(true)).ToArray() ?? Array.Empty<object>(),
                Data = log.Data?.ToHex(true) ?? "0x",
                BlockNumber = new HexBigInteger(log.BlockNumber),
                TransactionHash = log.TransactionHash?.ToHex(true),
                TransactionIndex = new HexBigInteger(log.TransactionIndex),
                BlockHash = log.BlockHash?.ToHex(true),
                LogIndex = new HexBigInteger(log.LogIndex),
                Removed = log.Removed
            };
        }

        protected static long ParseHexOrDecimalLong(RpcRequestMessage request, int index)
        {
            var rawParams = request.RawParameters;
            if (rawParams is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                var element = jsonElement[index];
                if (element.ValueKind == JsonValueKind.Number)
                    return element.GetInt64();
                if (element.ValueKind == JsonValueKind.String)
                {
                    var str = element.GetString();
                    if (str != null && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        var bigValue = str.HexToBigInteger(false);
                        if (bigValue < long.MinValue || bigValue > long.MaxValue)
                            throw RpcException.InvalidParams($"Value {str} exceeds valid range");
                        return (long)bigValue;
                    }
                    if (long.TryParse(str, out var parsed))
                        return parsed;
                }
            }
            return GetParam<long>(request, index);
        }
    }
}
