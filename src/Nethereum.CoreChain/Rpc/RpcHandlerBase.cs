using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

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

        protected static RpcResponseMessage RevertedError(object id, CallResult result)
        {
            var reason = !string.IsNullOrEmpty(result.RevertReason) ? result.RevertReason : "revert";
            return Error(id, 3, $"execution reverted: {reason}", result.ReturnData?.ToHex(true) ?? "0x");
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

                return RpcValueConverter.FromObject<T>(array[index]);
            }

            if (rawParams is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != JsonValueKind.Array)
                    throw RpcException.InvalidParams("Parameters must be an array");

                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                return RpcValueConverter.FromJsonElement<T>(jsonElement[index]);
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

                return RpcValueConverter.FromObject<T>(array[index]);
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

                return RpcValueConverter.FromJsonElement<T>(element);
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

        protected static string ToHex(int value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(long value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(ulong value) => ((BigInteger)value).ToHex(false);
        protected static string ToHex(BigInteger value) => value.ToHex(false);
        protected static string ToHex(byte[] data) => data.ToHex(true);

        protected static Task<BigInteger> ResolveBlockNumberAsync(string blockTag, RpcContext context)
            => BlockTagResolver.ResolveAsync(blockTag, context);

        protected static long ParseHexOrDecimalLong(RpcRequestMessage request, int index)
        {
            var rawParams = request.RawParameters;
            if (rawParams is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                return ParseHexOrDecimalLong(jsonElement[index]);
            }
            return GetParam<long>(request, index);
        }

        protected static long ParseHexOrDecimalLong(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
                return 0;

            if (element.ValueKind == JsonValueKind.Number)
                return element.GetInt64();

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (string.IsNullOrEmpty(str))
                    return 0;

                if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    var bigValue = str.HexToBigInteger(false);
                    if (bigValue < long.MinValue || bigValue > long.MaxValue)
                        throw RpcException.InvalidParams($"Value {str} exceeds valid range");
                    return (long)bigValue;
                }

                if (long.TryParse(str, out var parsed))
                    return parsed;
            }

            throw RpcException.InvalidParams($"Expected a numeric or hex-quantity value, got: {element.GetRawText()}");
        }
    }
}
