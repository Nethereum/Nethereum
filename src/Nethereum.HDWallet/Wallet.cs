using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;

namespace Nethereum.HdWallet
{
    public class Wallet
    {
        public const string DEFAULT_PATH = "m/44'/60'/0'/0/x";
        public const string ELECTRUM_LEDGER_PATH = "m/44'/60'/0'/x";

        public Wallet(Wordlist wordList, WordCount wordCount, string seedPassword = null, string path = DEFAULT_PATH,
            IRandom random = null) : this(path, random)
        {
            InitialiseSeed(wordList, wordCount, seedPassword);
        }

        public Wallet(string words, string seedPassword, string path = DEFAULT_PATH, IRandom random = null) : this(path,
            random)
        {
            InitialiseSeed(words, seedPassword);
        }

        public Wallet(byte[] seed, string path = DEFAULT_PATH, IRandom random = null) : this(path, random)
        {
            Seed = seed.ToHex();
        }

        private Wallet(string path = DEFAULT_PATH, IRandom random = null)
        {
            Path = path;
            if (random == null) random = new SecureRandom();
            Random = random;
        }

        private IRandom Random
        {
            get => RandomUtils.Random;
            set => RandomUtils.Random = value;
        }

        public string Seed { get; private set; }
        public string[] Words { get; private set; }
        public string Path { get; }

        private void InitialiseSeed(Wordlist wordlist, WordCount wordCount, string seedPassword = null)
        {
            var mneumonic = new Mnemonic(wordlist, wordCount);
            Seed = mneumonic.DeriveSeed(seedPassword).ToHex();
            Words = mneumonic.Words;
        }

        private void InitialiseSeed(string words, string seedPassword = null)
        {
            var mneumonic = new Mnemonic(words);
            Seed = mneumonic.DeriveSeed(seedPassword).ToHex();
            Words = mneumonic.Words;
        }

        private string GetIndexPath(int index)
        {
            return Path.Replace("x", index.ToString());
        }

        private ExtKey GetKey(int index)
        {
            var masterKey = new ExtKey(Seed);
            var keyPath = new KeyPath(GetIndexPath(index));
            return masterKey.Derive(keyPath);
        }

        private EthECKey GetEthereumKey(int index)
        {
            var privateKey = GetPrivateKey(index);
            return new EthECKey(privateKey, true);
        }

        public byte[] GetPrivateKey(int index)
        {
            var key = GetKey(index);
            return key.PrivateKey.ToBytes();
        }

        public byte[] GetPrivateKey(int startIndex, string address, int maxIndexSearch = 20)
        {
            var checkSumAddress = new AddressUtil().ConvertToChecksumAddress(address);
            for (var i = startIndex; i < startIndex + maxIndexSearch; i++)
            {
                var ethereumKey = GetEthereumKey(i);
                if (ethereumKey.GetPublicAddress() == checkSumAddress)
                    return ethereumKey.GetPrivateKeyAsBytes();
            }
            return null;
        }

        public byte[] GetPrivateKey(string address, int maxIndexSearch = 20)
        {
            return GetPrivateKey(0, address, maxIndexSearch);
        }

        public string[] GetAddresses(int numberOfAddresses = 20)
        {
            var addresses = new string[numberOfAddresses];
            for (var i = 0; i < numberOfAddresses; i++)
            {
                var ethereumKey = GetEthereumKey(i);
                addresses[i] = ethereumKey.GetPublicAddress();
            }
            return addresses;
        }

        public Account GetAccount(string address, int maxIndexSearch = 20)
        {
            var privateyKey = GetPrivateKey(address, maxIndexSearch);
            if (privateyKey != null)
                return new Account(privateyKey);
            return null;
        }

        public Account GetAccount(int index)
        {
            var privateyKey = GetPrivateKey(index);
            if (privateyKey != null)
                return new Account(privateyKey);
            return null;
        }
    }
}