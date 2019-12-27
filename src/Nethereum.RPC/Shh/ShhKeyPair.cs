using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh
{
    public class ShhKeyPair : RpcClientWrapper, IShhKeyPair
    {
        public ShhKeyPair(IClient client) : base(client)
        {
            AddPrivateKey = new ShhAddPrivateKey(client);
            NewKeyPair = new ShhNewKeyPair(client);
        }

        public IShhNewKeyPair NewKeyPair { get; private set; }

        public IShhAddPrivateKey AddPrivateKey { get; private set; }
    }
}
