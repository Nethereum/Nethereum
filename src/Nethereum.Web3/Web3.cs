
    using System;
    using System.Collections.Generic;
    using edjCase.JsonRpc.Client;

namespace Nethereum.Web3
{
    public class Web3
    {
        public RpcClient Client { get; private set; }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            Eth = new Eth(Client);
        }

        public Eth Eth { get; private set; }

        private void IntialiseRpcClient(string url)
        {
            this.Client = new RpcClient(new Uri(url));
        }
    }
}

