using System;

namespace Nethereum.JsonRpc.Client.Streaming
{
    public class StreamingEventArgs<TEntity> : EventArgs
    {
        public TEntity Response { get; private set; }
        public RpcResponseException Exception { get; private set; }

        public StreamingEventArgs(TEntity entity, RpcResponseException exception = null)
        {
            Response = entity;
        }

        public StreamingEventArgs(RpcResponseException exception)
        {
            Exception = exception;
        }

    }
}
