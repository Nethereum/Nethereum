using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair; 
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
            HasKeyPair = new ShhHasKeyPair(client);
        }

        public IShhNewKeyPair NewKeyPair { get; private set; }

        public IShhAddPrivateKey AddPrivateKey { get; private set; }

        public IShhDeleteKeyPair DeleteKeyPair { get; private set; }

        public IShhHasKeyPair HasKeyPair { get; private set; }

        public IShhGetPublicKey GetPublicKey { get; private set; }

        public IShhGetPrivateKey GetPrivateKey { get; private set; }
    }
}
