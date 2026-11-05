using System.Text.Json;

namespace Nethereum.CoreChain.Rpc
{
    public static class RpcJsonElementExtensions
    {
        public static string RequireRpcString(this JsonElement element, string what)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw RpcException.InvalidParams($"{what} must be a string");
            }

            return element.GetRpcString();
        }

        public static string GetOptionalRpcString(this JsonElement element, string what)
        {
            return element.ValueKind == JsonValueKind.Null ? null : element.RequireRpcString(what);
        }

        public static JsonElement RequireRpcObject(this JsonElement element, string what)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw RpcException.InvalidParams($"{what} must be an object");
            }

            return element;
        }

        public static string GetRpcString(this JsonElement element)
        {
            try
            {
                return element.GetString();
            }
            catch (System.InvalidOperationException)
            {
                throw RpcException.InvalidParams("string parameter is not valid UTF-16");
            }
        }
    }
}
