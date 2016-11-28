using System.Numerics;
using NBitcoin.Crypto;
using Nethereum.Core;
using Nethereum.Core.Signing.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Web3
{
    public class TransactionSigning
    {
        public byte[] GetPublicKey(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPubKey(false);
        }

        public string GetSenderAddress(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPublicAddress();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Transaction(to, amount, nonce);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Transaction(to, amount, nonce, data);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data)
        {
            var transaction = new Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public bool VerifyTransaction(string rlp)
        {
            var transaction = new Transaction(rlp.HexToByteArray());
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }
    }
}