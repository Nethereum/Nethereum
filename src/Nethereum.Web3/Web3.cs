
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using edjCase.JsonRpc.Client;
    using Nethereum.ABI;
    using Nethereum.ABI.Util;
    using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class Web3
    {
        public RpcClient Client { get; private set; }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            Eth = new Eth(Client);
            Shh = new Shh(Client);
            Net =new Net(Client);
        }

        public Eth Eth { get; private set; }
        public Shh Shh { get; private set; }

        public Net Net { get; private set; }

        private void IntialiseRpcClient(string url)
        {
            this.Client = new RpcClient(new Uri(url));
        }

        public string Sha3(string value)
        {
            return new Sha3Keccack().CalculateHash(value);
        }
    }
    
}

