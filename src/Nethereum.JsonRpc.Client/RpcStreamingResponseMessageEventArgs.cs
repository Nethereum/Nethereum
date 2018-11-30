using Nethereum.JsonRpc.Client.RpcMessages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class RpcStreamingResponseMessageEventArgs : EventArgs
    {
        public RpcStreamingResponseMessageEventArgs(RpcStreamingResponseMessage message)
        {
            this.Message = message;
        }

        public RpcStreamingResponseMessage Message { get; private set; }
    }
}
