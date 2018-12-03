using Nethereum.JsonRpc.Client.RpcMessages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class RpcResponseMessageEventArgs : EventArgs
    {
        public RpcResponseMessageEventArgs(RpcResponseMessage message)
        {
            this.Message = message;
        }

        public RpcResponseMessage Message { get; private set; }
    }
}
