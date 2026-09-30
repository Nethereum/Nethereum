using System;
using System.Text.Json;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.Rpc
{
    internal static class RpcValueConverter
    {
        private static JsonSerializerOptions AotOptions => CoreChainJsonContext.Default.Options;

        public static T FromJsonElement<T>(JsonElement element)
        {
            var targetType = typeof(T);

            if (targetType == typeof(string)) return (T)(object)element.GetString();
            if (targetType == typeof(bool)) return (T)(object)element.GetBoolean();
            if (targetType == typeof(int)) return (T)(object)element.GetInt32();
            if (targetType == typeof(long)) return (T)(object)element.GetInt64();
            if (targetType == typeof(double)) return (T)(object)element.GetDouble();
            if (targetType == typeof(JsonElement)) return (T)(object)element;

            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText(), AotOptions);
        }

        public static T FromObject<T>(object value)
        {
            if (value == null) return default;
            if (value is T typed) return typed;
            if (value is JsonElement element) return FromJsonElement<T>(element);

            var targetType = typeof(T);

            if (targetType == typeof(string)) return (T)(object)ToRpcString(value);
            if (targetType.IsPrimitive || targetType == typeof(decimal)) return (T)Convert.ChangeType(value, targetType);
            if (value is JToken jToken) return System.Text.Json.JsonSerializer.Deserialize<T>(jToken.ToString(Formatting.None), AotOptions);

            return FromComplexObject<T>(value, targetType);
        }

        private static string ToRpcString(object value)
        {
            if (value is BlockParameter blockParameter) return blockParameter.GetRPCParam();
            if (value is HexBigInteger hexQuantity) return hexQuantity.HexValue;
            return value.ToString();
        }

        private static T FromComplexObject<T>(object value, Type targetType)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json, AotOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert {value?.GetType().Name} to {targetType.Name}: {ex.Message}", ex);
            }
        }
    }
}
