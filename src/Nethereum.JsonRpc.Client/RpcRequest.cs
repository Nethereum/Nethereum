namespace Nethereum.JsonRpc.Client
{
    public class RpcRequest
    {
        public RpcRequest(object id, string method, params object[] parameterList)
        {
            Id = id;
            Method = method;
            RawParameters = parameterList;
        }

        public object Id { get; set; }
        public string Method { get; private set; }
        public object[] RawParameters { get; private set; }
    }
}