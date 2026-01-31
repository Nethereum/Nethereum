using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
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

            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText());
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
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }

            // For complex types, serialize to JSON then deserialize
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
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

        protected static void ValidateBlockParameterIsLatest(string blockTag, string methodName)
        {
            if (string.IsNullOrEmpty(blockTag))
                return;

            var normalized = blockTag.ToLowerInvariant();
            if (normalized == "latest" || normalized == "pending")
                return;

            if (normalized == "earliest")
                throw RpcException.InvalidParams($"{methodName}: historical state queries not supported. Use 'latest' or 'pending'.");

            if (normalized.StartsWith("0x") || char.IsDigit(normalized[0]))
                throw RpcException.InvalidParams($"{methodName}: historical state queries not supported. Use 'latest' or 'pending'.");

            throw RpcException.InvalidParams($"{methodName}: invalid block parameter '{blockTag}'. Use 'latest' or 'pending'.");
        }
    }
}
