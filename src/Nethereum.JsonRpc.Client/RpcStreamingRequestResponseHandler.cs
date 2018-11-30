using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public class RpcStreamingRequestResponseHandler<T> : IStreamingRpcRequestHandler<StreamingEventArgs<T>>         
    {
        private object id;
        private string subscriptionId;

        public RpcStreamingRequestResponseHandler(IStreamingClient client, string methodName)
        {
            MethodName = methodName;
            Client = client;

            Client.StreamingMessageReceived += RpcStreamingMessageResponseHandler;
        }

        public string MethodName { get; }

        public IStreamingClient Client { get; }

        public event EventHandler<StreamingEventArgs<T>> MessageRecieved;

        protected Task SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            this.id = request.Id;

            return Client.SendRequestAsync(request);
        }

        public RpcRequest BuildRequest(object id, params object[] paramList)
        {
            if (id == null) id = Configuration.DefaultRequestId;
            return new RpcRequest(id, MethodName, paramList);
        }

        private void RpcMessageResponseHandler(object sender, RpcResponseMessageEventArgs e)
        {
            if (e.Message.Id != this.id)
            {
                return;
            }

            // this is the ID we can use to filter future subscription messages on, if for example we were listening to block headers
            // and new pending transactions
            subscriptionId = e.Message.GetResult<string>();
        }

        private void RpcStreamingMessageResponseHandler(object sender, RpcStreamingResponseMessageEventArgs e)
        {
            if (e.Message.Params.Subscription != this.subscriptionId)
            {
                return;
            }

            // do something with e.Message
            var handler = MessageRecieved;
            if (handler != null)
            {
                try
                {
                    var args = new StreamingEventArgs<T>(e.Message.GetResult<T>());
                    handler(this, args);
                }
                catch (FormatException formatException)
                {
                    throw new RpcResponseFormatException("Invalid format found in RPC streaming response", formatException);
                }
            }
        }
    }
}