using Nethereum.JsonRpc.Client.RpcMessages;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseBatch
    {
        public List<IRpcRequestResponseBatchItem> BatchItems { get; set; } = new List<IRpcRequestResponseBatchItem>();

        public RpcRequestMessage[] GetRpcRequests()
        {
            return BatchItems.Select(x => x.RpcRequestMessage).ToArray();
        }

        public void UpdateBatchItemResponses(IEnumerable<RpcResponseMessage> responses)
        {
            foreach(var response in responses)
            {
                var batchItem = BatchItems.First(x => x.RpcRequestMessage.Id.ToString() == response.Id.ToString());
                batchItem.DecodeResponse(response);
            }
        }
    }
}