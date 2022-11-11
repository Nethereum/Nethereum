using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using System;
using System.Collections.Generic;
using System.Numerics;
using KeyPath = NBitcoin.KeyPath;

namespace Nethereum.HdWallet
{
    public class Wallet
    {
        public const string DEFAULT_PATH = "m/44'/60'/0'/0/x";
        public const string ELECTRUM_LEDGER_PATH = "m/44'/60'/0'/x";
        private Dictionary<int, ExtKey> nonHardenedKeys = new Dictionary<int, ExtKey>();
        private Dictionary<int, ExtKey> hardenedKeys = new Dictionary<int, ExtKey>();
        private Dictionary<int, EthECKey> ethereumKeys =  new Dictionary<int, EthECKey>();

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
            var keyPath = new KeyPath(GetMasterPath());
            _masterKey = new ExtKey(Seed).Derive(keyPath);
        }

        private Wallet(string path = DEFAULT_PATH, IRandom random = null)
        {
            Path = path;
#if NETCOREAPP2_1 || NETCOREAPP3_1 || NETSTANDARD2_0 || NET5_0_OR_GREATER || NETSTANDARD2_1
            if (random == null) random = new RandomNumberGeneratorRandom();
#else
            if (random == null) random = new SecureRandom();
#endif
            Random = random;
        }

        private IRandom Random
        {
            get => RandomUtils.Random;
            set => RandomUtils.Random = value;
        }

        public string Seed { get; private set; }
        public string[] Words { get; private set; }

        public bool IsMneumonicValidChecksum { get; private set; }

        public string Path { get; }

        private ExtKey _masterKey;

        private void InitialiseSeed(Wordlist wordlist, WordCount wordCount, string seedPassword = null)
        {
            var mneumonic = new Mnemonic(wordlist, wordCount);
            Seed = mneumonic.DeriveSeed(seedPassword).ToHex();
            Words = mneumonic.Words;
            IsMneumonicValidChecksum = mneumonic.IsValidChecksum;
            var keyPath = new KeyPath(GetMasterPath());
            _masterKey = new ExtKey(Seed).Derive(keyPath);
        }

        private void InitialiseSeed(string words, string seedPassword = null)
        {
            var mneumonic = new Mnemonic(words);
            Seed = mneumonic.DeriveSeed(seedPassword).ToHex();
            Words = mneumonic.Words;
            IsMneumonicValidChecksum = mneumonic.IsValidChecksum;
            var keyPath = new KeyPath(GetMasterPath());
            _masterKey = new ExtKey(Seed).Derive(keyPath);
        }

        private string GetIndexPath(int index)
        {
            return Path.Replace("x", index.ToString());
        }

        private string GetMasterPath()
        {
            return Path.Replace("/x", "");
        }

        public ExtKey GetMasterExtKey()
        {
           return _masterKey;
        }

        public ExtPubKey GetMasterExtPubKey()
        {
            return GetMasterExtKey().Neuter();
        }

        public PublicWallet GetMasterPublicWallet()
        {
            return new PublicWallet(GetMasterExtPubKey());
        }

        public ExtKey GetExtKey(int index, bool hardened = false)
        {
            if (!hardened && nonHardenedKeys.ContainsKey(index)) return nonHardenedKeys[index];
            if (hardened && hardenedKeys.ContainsKey(index)) return hardenedKeys[index];
            if (hardened)
            {
                hardenedKeys.Add(index,_masterKey.Derive(index, true));
                return hardenedKeys[index];
            }
            else
            {
                nonHardenedKeys.Add(index,_masterKey.Derive(index, false));
                return nonHardenedKeys[index];
            }
            
        }

        public ExtPubKey GetExtPubKey(int index, bool hardened = false)
        {
          
            var key = GetExtKey(index, hardened);
            return key.Neuter();
        }

        public EthECKey GetEthereumKey(int index)
        {
            if (ethereumKeys.ContainsKey(index)) return ethereumKeys[index];
            var privateKey = GetPrivateKey(index);
            ethereumKeys.Add(index, new EthECKey(privateKey, true));
            return ethereumKeys[index];
        }

        public byte[] GetPrivateKey(int index)
        {
            var key = GetExtKey(index);
            return key.PrivateKey.ToBytes();
        }

        public byte[] GetPublicKey(int index)
        {
            var key = GetEthereumKey(index);
            return key.GetPubKey();
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

        public Account GetAccount(string address, int maxIndexSearch = 20, BigInteger? chainId = null)
        {
            var privateyKey = GetPrivateKey(address, maxIndexSearch);
            if (privateyKey != null)
                return new Account(privateyKey, chainId);
            return null;
        }

        public Account GetAccount(int index, BigInteger? chainId = null)
        {
            var key = GetEthereumKey(index);
            if (key != null)
                return new Account(key, chainId);
            return null;
        }
    }
}