using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.Wallet.RpcRequests
{
    public static class RpcParameterHelper
    {
        public static T? GetFirstParamAs<T>(this RpcRequestMessage request)
        {
            if (request.RawParameters is null)
                return default;

            // Handle positional parameters (array)
            if (request.RawParameters is object[] arr && arr.Length > 0)
            {
                return ConvertOrDeserialize<T>(arr[0]);
            }

            return ConvertOrDeserialize<T>(request.RawParameters);
        }

        private static T? ConvertOrDeserialize<T>(object value)
        {
            try
            {
                if (value is T tVal)
                    return tVal;

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default;
            }
        }
    }

}
