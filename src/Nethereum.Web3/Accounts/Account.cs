using System;

using Nethereum.RPC.Accounts;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;

namespace Nethereum.Web3.Accounts
{
    public class Account : IAccount
    {

#if !PCL
        [Obsolete(Web3.ObsoleteWarningMsg)]
        public static Account LoadFromKeyStoreFile(string filePath, string password)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromFile(password, filePath);
            return new Account(key);
        }

        public static Account LoadFromKeyStoreFile(string filePath, string password, Web3 web3)
        {
            if (web3 == null)
                throw new ArgumentNullException(nameof(web3));
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromFile(password, filePath);
            return new Account(key, web3);
        }
#endif

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public static Account LoadFromKeyStore(string json, string password)
        {
            return LoadFromKeyStoreInner(json, password, null);
        }

        public static Account LoadFromKeyStore(string json, string password, Web3 web3)
        {
            if (web3 == null)
                throw new ArgumentNullException(nameof(web3));
            return LoadFromKeyStoreInner(json, password, web3);
        }

        private static Account LoadFromKeyStoreInner(string json, string password, Web3 web3)
        {
            var keyStoreService = new Nethereum.KeyStore.KeyStoreService();
            var key = keyStoreService.DecryptKeyStoreFromJson(password, json);
            if (web3 == null)
                return new Account(key);
            return new Account(key, web3);
        }

        public string PrivateKey { get; private set; }

        public Account(EthECKey key, Web3 web3)
        {
            if (web3 == null)
                throw new ArgumentNullException(nameof(web3));
            Initialise(key, web3);
        }

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public Account(EthECKey key)
        {
            Initialise(key, null);
        }

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public Account(string privateKey)
        {
            Initialise(new EthECKey(privateKey), null);
        }

        public Account(string privateKey, Web3 web3)
        {
            if (web3 == null)
                throw new ArgumentNullException(nameof(web3));
            Initialise(new EthECKey(privateKey), web3);
        }

        [Obsolete(Web3.ObsoleteWarningMsg)]
        public Account(byte[] privateKey)
        {
            Initialise(new EthECKey(privateKey, true), null);
        }

        public Account(byte[] privateKey, Web3 web3)
        {
            if (web3 == null)
                throw new ArgumentNullException(nameof(web3));
            Initialise(new EthECKey(privateKey, true), web3);
        }

        private void Initialise(EthECKey key, Web3 web3)
        {
            PrivateKey = key.GetPrivateKey();
            Address = key.GetPublicAddress();
            InitialiseDefaultTransactionManager(web3);
        }

        protected virtual void InitialiseDefaultTransactionManager(Web3 web3)
        {
            TransactionManager = new AccountSignerTransactionManager(web3, this);
        }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }
        public INonceService NonceService { get; set; }
    }
}