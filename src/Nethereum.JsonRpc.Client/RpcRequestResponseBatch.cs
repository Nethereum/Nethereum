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
            var errors = new List<RpcError>();
            foreach(var response in responses)
            {
                var batchItem = BatchItems.First(x => x.RpcRequestMessage.Id.ToString() == response.Id.ToString());
                if (response.HasError)
                {
                    errors.Add(new RpcError(response.Error.Code, response.Error.Message + ": " + batchItem.RpcRequestMessage.Method,
                     response.Error.Data));
                }
                else
                {
                    batchItem.DecodeResponse(response);
                }
            }

            if(errors.Any())
                throw new RpcResponseBatchException(errors.ToArray());
        }

    }
}