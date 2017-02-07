using Nethereum.RPC.Eth.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Web3.Transactions;


namespace Nethereum.Web3.Accounts
{
    public class Account : IAccount
    {

#if !PCL
        public static Account LoadFromKeyStoreFile(string filePath, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromFile(filePath, password);
            return new Account(key);
        }
#endif
        public static Account LoadFromKeyStore(string json, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromJson(json, password);
            return new Account(key);
        }

        private string _privateKey;

        public Account(EthECKey key)
        {
            Initialise(key);
        }

        public Account(string privateKey)
        {
            Initialise(new EthECKey(privateKey));
        }

        public Account(byte[] privateKey)
        {
            Initialise(new EthECKey(privateKey, true));
        }

        private void Initialise(EthECKey key)
        {
            _privateKey = key.GetPrivateKey();
            Address = key.GetPublicAddress();
        }

        protected virtual void InitialiseDefaultTransactionManager()
        {
            TransactionManager = new SignedTransactionManager(_privateKey, Address);
        }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }
    }
}