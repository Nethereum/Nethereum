using System;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.Util;

namespace Nethereum.Web3
{
    public class Web3
    {
        public UnitConversion Convert { get; private set; }
        private readonly Sha3Keccack sha3Keccack;

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            Eth = new Eth(Client);
            Shh = new Shh(Client);
            Net = new Net(Client);
            Personal = new Personal(Client);
            Convert = new UnitConversion();
            sha3Keccack = new Sha3Keccack();
        }

        public RpcClient Client { get; private set; }

        public Eth Eth { get; private set; }
        public Shh Shh { get; private set; }

        public Net Net { get; private set; }

        public Personal Personal { get; private set; }

        private void IntialiseRpcClient(string url)
        {
            Client = new RpcClient(new Uri(url));
        }

        public string Sha3(string value)
        {
            return sha3Keccack.CalculateHash(value);
        }
    }
}