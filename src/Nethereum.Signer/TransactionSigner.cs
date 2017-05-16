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

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            return SignTransaction(privateKey, transaction);
        }

        public string SignTransaction(string privateKey, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            return SignTransaction(privateKey, transaction);
        }

        private string SignTransaction(string privateKey, Transaction transaction)
        {
            transaction.Sign(new EthECKey(privateKey.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public bool VerifyTransaction(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }
    }
}