using System;
using Nethereum.ABI.Util;
using Nethereum.Core.Signing.Crypto;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Eth.TransactionManagers;
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

        public ITransactionManager TransactionManager
        {
            get { return Eth.TransactionManager; }
            set { Eth.TransactionManager = value; }
        }

        public UnitConversion Convert { get; private set; }
        public TransactionSigner OfflineTransactionSigner { get; private set; }

        public IClient Client { get; private set; }

        public EthApiService Eth { get; private set; }
        public ShhApiService Shh { get; private set; }

        public NetApiService Net { get; private set; }

        public PersonalApiService Personal { get; private set; }

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
            Eth = new EthApiService(Client);
            Shh = new ShhApiService(Client);
            Net = new NetApiService(Client);
            Personal = new PersonalApiService(Client);
            Convert = new UnitConversion();
            sha3Keccack = new Sha3Keccack();
            OfflineTransactionSigner = new TransactionSigner();
            addressUtil = new AddressUtil();
        }

        private void IntialiseRpcClient(string url)
        {
            Client = new RpcClient(new Uri(url), null,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
        }
    }
}