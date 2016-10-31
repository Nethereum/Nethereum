using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using NBitcoin.Crypto;
using Nethereum.Core;
using Nethereum.Core.Signing.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Web3
{
    

    public class TransactionSigning
    {
        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce)
        {
            var transaction = new Core.Transaction(to, amount, nonce);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, string data)
        {
            var transaction = new Core.Transaction(to, amount, nonce, data);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit)
        {
            var transaction = new Core.Transaction(to, amount, nonce, gasPrice, gasLimit);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public string SignTransaction(string key, string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data) 
        {
            var transaction = new Core.Transaction(to, amount, nonce, gasPrice, gasLimit, data);
            transaction.Sign(new ECKey(key.HexToByteArray(), true));
            return transaction.GetRLPEncoded().ToHex();
        }

        public bool VerifyTransaction(string rlp)
        {
            var transaction = new Core.Transaction(rlp.HexToByteArray());
            return transaction.Key.VerifyAllowingOnlyLowS(transaction.RawHash, transaction.Signature);
        }

        public byte[] GetPublicKey(string rlp)
        {
            var transaction = new Core.Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPubKey(false);
        }

        public string GetSenderAddress(string rlp)
        {
            var transaction = new Core.Transaction(rlp.HexToByteArray());
            return transaction.Key.GetPublicAddress();
        }
    }
}
