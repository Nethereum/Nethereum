using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Signer
{
    public class TransactionSigner
    {
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

        public bool VerifyTransaction(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, data);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransaction(privateKey, transaction);
        }


        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, data);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, gasPrice, gasLimit);
        }

        public string SignTransaction(string privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            return SignTransaction(privateKey.HexToByteArray(), chainId, to, amount, nonce, gasPrice, gasLimit, data);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new TransactionChainId(to, amount, nonce, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, BigInteger string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction);
        }


        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, chainId);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(byte[] privateKey, BigInteger chainId, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new TransactionChainId(to, amount, nonce, gasPrice, gasLimit, data, chainId);
            return SignTransaction(privateKey, transaction);
        }

        private string SignTransaction(byte[] privateKey, Transaction transaction)
        {
            transaction.Sign(new EthECKey(privateKey, true));
            return transaction.GetRLPEncoded().ToHex();
        }

        private string SignTransaction(byte[] privateKey, TransactionChainId transaction)
        {
            transaction.Sign(new EthECKey(privateKey, true));
            return transaction.GetRLPEncoded().ToHex();
        }

    }
}