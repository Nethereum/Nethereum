using System;
using Nethereum.JsonRpc.Client;
using Nethereum.ABI.Util;
using Newtonsoft.Json;

namespace Nethereum.Web3
{
    public class Web3
    {
        public UnitConversion Convert { get; private set; }
        private Sha3Keccack sha3Keccack;

        public Web3(IClient client)
        {
            this.Client = client;
            InitialiseInnerServices();
        }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            InitialiseInnerServices();
        }

        private void InitialiseInnerServices()
        {
            Eth = new Eth(Client);
            Shh = new Shh(Client);
            Net = new Net(Client);
            Personal = new Personal(Client);
            Miner = new Miner(Client);
            DebugGeth = new DebugGeth(Client);
            Admin = new Admin(Client);
            Convert = new UnitConversion();
            sha3Keccack = new Sha3Keccack();
        }

        public IClient Client { get; private set; }

        public Eth Eth { get; private set; }
        public Shh Shh { get; private set; }

        public Net Net { get; private set; }

        public Personal Personal { get; private set; }

        public Admin Admin { get; private set; }

        public DebugGeth DebugGeth { get; private set; }

        public Miner Miner { get; private set; }

        private void IntialiseRpcClient(string url)
        {
            Client = new RpcClient(new Uri(url), null, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        public string Sha3(string value)
        {
            return sha3Keccack.CalculateHash(value);
        }
    }
}