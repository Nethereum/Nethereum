namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestBuilder
    {
        public RpcRequestBuilder(string methodName)
        {
            MethodName = methodName;
        }

        public string MethodName { get; }

        public virtual RpcRequest BuildRequest(object id, params object[] paramList)
        {
            if (id == null) id = Configuration.DefaultRequestId;
            return new RpcRequest(id, MethodName, paramList);
        }

        public virtual RpcRequest BuildRequest(object id = null)
        {
            if (id == null) id = Configuration.DefaultRequestId;
            return new RpcRequest(id, MethodName);
        }
    }
}