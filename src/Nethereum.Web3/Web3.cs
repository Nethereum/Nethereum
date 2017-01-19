using System;
using Nethereum.ABI.Util;
using Nethereum.Core.Signing.Crypto;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;

namespace Nethereum.Web3
{
    public class Web3
    {
        private AddressUtil addressUtil;

        private Sha3Keccack sha3Keccack;

        public Web3(IClient client)
        {
            Client = client;
            InitialiseInnerServices();
        }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseRpcClient(url);
            InitialiseInnerServices();
        }

        public UnitConversion Convert { get; private set; }
        public TransactionSigning OfflineTransactionSigning { get; private set; }

        public IClient Client { get; private set; }

        public Eth Eth { get; private set; }
        public Shh Shh { get; private set; }

        public Net Net { get; private set; }

        public Personal Personal { get; private set; }

        public Admin Admin { get; private set; }

        public DebugGeth DebugGeth { get; private set; }

        public Miner Miner { get; private set; }

        public string GetAddressFromPrivateKey(string privateKey)
        {
            return EthECKey.GetPublicAddress(privateKey);
        }

        public bool IsChecksumAddress(string address)
        {
            return addressUtil.IsChecksumAddress(address);
        }

        public string Sha3(string value)
        {
            return sha3Keccack.CalculateHash(value);
        }

        public string ToChecksumAddress(string address)
        {
            return addressUtil.ConvertToChecksumAddress(address);
        }

        public string ToValid20ByteAddress(string address)
        {
            return addressUtil.ConvertToValid20ByteAddress(address);
        }

        protected virtual void InitialiseInnerServices()
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
            OfflineTransactionSigning = new TransactionSigning();
            addressUtil = new AddressUtil();
        }

        private void IntialiseRpcClient(string url)
        {
            Client = new RpcClient(new Uri(url), null,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
        }
    }
}