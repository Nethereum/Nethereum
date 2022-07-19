using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.Unity.Metamask
{
    public class WaitUntilRequestResponse
    {
        public string Id { get; }
        public RpcResponseMessage RpcResponseMessage { get; set; }
        public WaitUntilRequestResponse(string id)
        {
            Id = id;
        }

        public bool HasReceivedResponse()
        {
            return MetamaskRequestRpcClient.RequestResponses.ContainsKey(Id);
        }

    }
}




