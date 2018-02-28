using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Signer
{
    public class PrivateKey
    {
        public PrivateKey(byte[] key)
        {
            this.Key = new EthECKey(key, true);
        }
        public PrivateKey(string keyInHex) : this(keyInHex.HexToByteArray())
        {
        }

        internal EthECKey Key { get; private set; }
    }

    public class TransactionSigner
    {
        private const string ObsoleteWarningMsg = "This maps to the old Homestead-way of signing, which might be vulnerable to Replay Attacks";

        public byte[] GetPublicKey(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPubKey();
        }

        public string GetSenderAddress(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPublicAddress();
        }

        [Obsolete(ObsoleteWarningMsg)]
        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction, null);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, Chain chain)
        {
            return SignTransaction(privateKey, to, amount, nonce, (int)chain);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, int chainId)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction, chainId);
        }

        [Obsolete(ObsoleteWarningMsg)]
        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction, null);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, string data, Chain chain)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction, chain);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, Chain chain)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransaction(privateKey, transaction, chain);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data, Chain chain)
        {
            return SignTransaction(privateKey, to, amount, nonce, gasPrice, gasLimit, data, (int)chain);
        }

        [Obsolete(ObsoleteWarningMsg)]
        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransaction(privateKey, transaction, null);
        }

        public string SignTransaction(PrivateKey privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data, int chainId)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransaction(privateKey, transaction, chainId);
        }

        private string SignTransaction(PrivateKey privateKey, Transaction transaction, Chain chain)
        {
            return SignTransaction(privateKey, transaction, (int)chain);
        }

        private string SignTransaction(PrivateKey privateKey, Transaction transaction, int? chain)
        {
            if (chain != null)
                transaction.Sign(privateKey.Key, chain.Value);
            else
                transaction.Sign(privateKey.Key);
            return transaction.GetRLPEncoded().ToHex();
        }

        public bool VerifyTransaction(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }
    }
}