using System;
using System.Net.Http.Headers;
using Common.Logging;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;
#if !LITE
using Nethereum.Signer;
#endif
using Nethereum.Util;

namespace Nethereum.Web3
{
    public class Web3 : IWeb3
    {
        private static readonly AddressUtil AddressUtil = new AddressUtil();
        private static readonly Sha3Keccack Sha3Keccack = new Sha3Keccack();

        public Web3(IClient client)
        {
            Client = client;
            InitialiseInnerServices();
            InitialiseDefaultGasAndGasPrice();
        }

        public Web3(IAccount account, IClient client) : this(client)
        {
            TransactionManager = account.TransactionManager;
            TransactionManager.Client = Client;
        }

        public Web3(string url = @"http://localhost:8545/", ILog log = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            InitialiseDefaultRpcClient(url, log, authenticationHeader);
            InitialiseInnerServices();
            InitialiseDefaultGasAndGasPrice();
        }

        public Web3(IAccount account, string url = @"http://localhost:8545/", ILog log = null, AuthenticationHeaderValue authenticationHeader = null) : this(url, log, authenticationHeader)
        {
            TransactionManager = account.TransactionManager;
            TransactionManager.Client = Client;
        }

        public ITransactionManager TransactionManager
        {
            get => Eth.TransactionManager;
            set => Eth.TransactionManager = value;
        }

        public static UnitConversion Convert { get; } = new UnitConversion();

        public IClient Client { get; private set; }

        public IEthApiContractService Eth { get; private set; }
        public IShhApiService Shh { get; private set; }
        public INetApiService Net { get; private set; }
        public IPersonalApiService Personal { get; private set; }
        public IBlockchainProcessingService Processing { get; private set; }

        public FeeSuggestionService FeeSuggestion { get; private set; }

        private void InitialiseDefaultGasAndGasPrice()
        {
#if !LITE
            TransactionManager.DefaultGas = LegacyTransaction.DEFAULT_GAS_LIMIT;
            TransactionManager.DefaultGasPrice = LegacyTransaction.DEFAULT_GAS_PRICE;
#endif
        }

#if !LITE
        public static string GetAddressFromPrivateKey(string privateKey)
        {

            return EthECKey.GetPublicAddress(privateKey);

        }
#endif

        public static bool IsChecksumAddress(string address)
        {
            return AddressUtil.IsChecksumAddress(address);
        }

        public static string Sha3(string value)
        {
            return Sha3Keccack.CalculateHash(value);
        }

        public static string ToChecksumAddress(string address)
        {
            return AddressUtil.ConvertToChecksumAddress(address);
        }

        public static string ToValid20ByteAddress(string address)
        {
            return AddressUtil.ConvertToValid20ByteAddress(address);
        }

        private void InitialiseInnerServices()
        {
            Eth = new EthApiContractService(Client);
            Processing = new BlockchainProcessingService(Eth);
            Shh = new ShhApiService(Client);
            Net = new NetApiService(Client);
            Personal = new PersonalApiService(Client);
            FeeSuggestion = new FeeSuggestionService(Client);
        }

        private void InitialiseDefaultRpcClient(string url, ILog log, AuthenticationHeaderValue authenticationHeader)
        {
            Client = new RpcClient(new Uri(url), authenticationHeader, null, null, log);
        }
    }
}