using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class RpcResponseErrorMessageEventArgs : EventArgs
    {
        public RpcResponseErrorMessageEventArgs(RpcError error)
        {
            this.Error = error;
        }

        public RpcError Error { get; private set; }
    }
}
