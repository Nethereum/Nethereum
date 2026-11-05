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

                RequireValue<T>(array[index] == null || array[index] is JsonElement { ValueKind: JsonValueKind.Null }, index);
                return RpcValueConverter.FromObject<T>(array[index]);
            }

            if (rawParams is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != JsonValueKind.Array)
                    throw RpcException.InvalidParams("Parameters must be an array");

                if (index >= jsonElement.GetArrayLength())
                    throw RpcException.InvalidParams($"Missing parameter at index {index}");

                RequireValue<T>(jsonElement[index].ValueKind == JsonValueKind.Null, index);
                return RpcValueConverter.FromJsonElement<T>(jsonElement[index]);
            }

            throw RpcException.InvalidParams("Parameters must be an array");
        }

        private static void RequireValue<T>(bool isNull, int index)
        {
            if (isNull && Nullable.GetUnderlyingType(typeof(T)) == null)
                throw RpcException.InvalidParams($"missing value for required argument {index}");
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

        protected static T ParseClientInput<T>(Func<T> parse)
        {
            try
            {
                return parse();
            }
            catch (Exception ex) when (
                ex is FormatException
                || ex is InvalidOperationException
                || ex is ArgumentException
                || ex is OverflowException
                || ex is InvalidCastException
                || ex is System.Text.Json.JsonException
                || ex is Newtonsoft.Json.JsonException)
            {
                throw RpcException.InvalidParams("invalid params");
            }
        }

        protected static JsonElement RequireObject(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw RpcException.InvalidParams($"invalid {name}: expected an object");

            return element;
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

        protected static string GetOptionalBlockParam(RpcRequestMessage request, int index, string defaultValue)
        {
            if (index >= GetParamCount(request))
                return defaultValue;

            var element = GetJsonElement(request, index);
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return defaultValue;
                case JsonValueKind.String:
                    return element.GetRpcString();
                case JsonValueKind.Object:
                    if (element.TryGetProperty("blockHash", out var hash) && hash.ValueKind == JsonValueKind.String)
                        return hash.GetRpcString();
                    if (element.TryGetProperty("blockNumber", out var number) && number.ValueKind == JsonValueKind.String)
                        return number.GetRpcString();
                    throw RpcException.InvalidParams("invalid block parameter object: expected blockHash or blockNumber");
                default:
                    throw RpcException.InvalidParams("invalid block parameter");
            }
        }

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
            {
                if (!element.TryGetInt64(out var number))
                    throw RpcException.InvalidParams("numeric value exceeds the valid range");
                return number;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetRpcString();
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
