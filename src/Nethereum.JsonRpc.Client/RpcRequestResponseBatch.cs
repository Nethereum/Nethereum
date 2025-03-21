using Nethereum.JsonRpc.Client.RpcMessages;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseBatch
    {
        public bool AcceptPartiallySuccessful { get; set; } = false;

        public List<IRpcRequestResponseBatchItem> BatchItems { get; set; } = new List<IRpcRequestResponseBatchItem>();

        public RpcRequestMessage[] GetRpcRequests()
        {
            return BatchItems.Select(x => x.RpcRequestMessage).ToArray();
        }

        public virtual void UpdateBatchItemResponses(IEnumerable<RpcResponseMessage> responses)
        {
            var errors = new List<RpcError>();
            foreach (var response in responses)
            {
                var batchItem = BatchItems.First(x => x.RpcRequestMessage.Id.ToString() == response.Id.ToString());
                if (response.HasError)
                {
                    batchItem.DecodeResponse(response);
                    errors.Add(batchItem.RpcError);
                }
                else
                {
                    batchItem.DecodeResponse(response);
                }
            }

            if (errors.Any() && AcceptPartiallySuccessful == false)
                throw new RpcResponseBatchException(errors.ToArray());
        }

    }
}