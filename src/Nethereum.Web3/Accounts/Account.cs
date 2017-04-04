using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;


namespace Nethereum.Web3.Accounts
{
    public class Account : IAccount
    {

#if !PCL
        public static Account LoadFromKeyStoreFile(string filePath, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromFile(password, filePath);
            return new Account(key);
        }
#endif
        public static Account LoadFromKeyStore(string json, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromJson(password, json);
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
            InitialiseDefaultTransactionManager();
        }

        protected virtual void InitialiseDefaultTransactionManager()
        {
            TransactionManager = new AccountSignerTransactionManager(_privateKey);
        }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }
    }
}