using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.SymKey;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh
{
    public class ShhSymKey : RpcClientWrapper, IShhSymKey
    {
        public ShhSymKey(IClient client) : base(client)
        {
            AddSymKey = new ShhAddSymKey(client);
            DeleteSymKey = new ShhDeleteSymKey(client);
            GenerateSymKeyFromPassword = new ShhGenerateSymKeyFromPassword(client);
            GetSymKey = new ShhGetSymKey(client);
            HasSymKey = new ShhHasSymKey(client);
            NewSymKey = new ShhNewSymKey(client);
        }

        public IShhAddSymKey AddSymKey { get; private set; }

        public IShhDeleteSymKey DeleteSymKey { get; private set; }

        public IShhGenerateSymKeyFromPassword GenerateSymKeyFromPassword { get; private set; }

        public IShhGetSymKey GetSymKey { get; private set; }

        public IShhHasSymKey HasSymKey { get; private set; }

        public IShhNewSymKey NewSymKey { get; private set; }
    }
}
