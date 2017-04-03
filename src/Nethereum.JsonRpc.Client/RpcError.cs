using Newtonsoft.Json.Linq;

namespace Nethereum.JsonRpc.Client
{
    public class RpcError
    {
        public RpcError(int code, string message, JToken data = null)
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public int Code { get; private set; }
        public string Message { get; private set; }
        public JToken Data { get; private set; }
    }
}