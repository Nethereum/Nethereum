using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class StreamingEventArgs<TEntity> : EventArgs
    {
        public TEntity Response { get; private set; }

        public StreamingEventArgs(TEntity entity)
        {
            Response = entity;
        }
    }
}
