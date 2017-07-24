using System;
using Nethereum.ABI.Util;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;

namespace Nethereum.Web3
{
    public class Web3
    {
        private static AddressUtil addressUtil = new AddressUtil();
        private static Sha3Keccack sha3Keccack = new Sha3Keccack();
        private static TransactionSigner transactionSigner = new TransactionSigner();
        private static UnitConversion unitConversion = new UnitConversion();

        public Web3(IClient client)
        {
            Client = client;
            InitialiseInnerServices();
        }

        public Web3(IAccount account, IClient client):this(client)
        {
            this.TransactionManager = account.TransactionManager;
            this.TransactionManager.Client = this.Client;
        }

        public Web3(string url = @"http://localhost:8545/")
        {
            IntialiseDefaultRpcClient(url);
            InitialiseInnerServices();
        }

        public Web3(IAccount account, string url = @"http://localhost:8545/"):this(url)
        {
            this.TransactionManager = account.TransactionManager;
            this.TransactionManager.Client = this.Client;
        }

        public ITransactionManager TransactionManager
        {
            get { return Eth.TransactionManager; }
            set { Eth.TransactionManager = value; }
        }

        public static UnitConversion Convert { get { return unitConversion; } }

        public static TransactionSigner OfflineTransactionSigner { get { return transactionSigner; } }

        public IClient Client { get; private set; }

        public EthApiContractService Eth { get; private set; }
        public ShhApiService Shh { get; private set; }

        public NetApiService Net { get; private set; }

        public PersonalApiService Personal { get; private set; }

        public static string GetAddressFromPrivateKey(string privateKey)
        {
            return EthECKey.GetPublicAddress(privateKey);
        }

        public static bool IsChecksumAddress(string address)
        {
            return addressUtil.IsChecksumAddress(address);
        }

        public static string Sha3(string value)
        {
            return sha3Keccack.CalculateHash(value);
        }

        public static string ToChecksumAddress(string address)
        {
            return addressUtil.ConvertToChecksumAddress(address);
        }

        public static string ToValid20ByteAddress(string address)
        {
            return addressUtil.ConvertToValid20ByteAddress(address);
        }

        protected virtual void InitialiseInnerServices()
        {
            Eth = new EthApiContractService(Client);
            Shh = new ShhApiService(Client);
            Net = new NetApiService(Client);
            Personal = new PersonalApiService(Client);   
        }

        private void IntialiseDefaultRpcClient(string url)
        {
            Client = new RpcClient(new Uri(url));
        }
    }
}