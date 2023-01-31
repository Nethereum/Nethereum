using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System.Numerics;

namespace Nethereum.Model
{
    public static class TransactionFactory
    {
        public static ISignedTransaction CreateTransaction(string rlpHex)
        {
            return CreateTransaction(rlpHex.HexToByteArray());
        }

        public static bool IsTypeTransaction(this byte[] bytes)
        {
            if (Enum.IsDefined(typeof(TransactionType),(int)bytes[0]) && (bytes[0] >= 0 && bytes[0] <= 127))
            {
                return true;
            }

            return false;
        }

        public static ITransactionTypeDecoder GetTransactionTypeDecoder(TransactionType transactionType)
        {

            switch (transactionType)
            {
                case TransactionType.LegacyTransaction: case TransactionType.LegacyChainTransaction:
                    throw new NotSupportedException(
                        "Legacy transactions are not supported, use CreateTransaction instead to decode");
                    
                case TransactionType.EIP1559:
                    return new Transaction1559Encoder();
                    
                default:
                    throw new ArgumentOutOfRangeException(nameof(transactionType), transactionType, null);
            }

        }
      

        public static ISignedTransaction CreateTransaction(byte[] rlp)
        {
            if (rlp.IsTypeTransaction())
            {
                var decoder = GetTransactionTypeDecoder((TransactionType) rlp[0]);
                return decoder.DecodeAsGeneric(rlp);
            }
            else
            {
                var rlpSigner = SignedLegacyTransaction.CreateDefaultRLPSigner(rlp);
                return rlpSigner.IsVSignatureForChain()
                    ? (SignedLegacyTransaction) new LegacyTransactionChainId(rlpSigner)
                    : new LegacyTransaction(rlpSigner);
            }
        }

        public static ISignedTransaction CreateLegacyTransaction(string to, BigInteger gas, BigInteger gasPrice, BigInteger amount, string data, BigInteger nonce, string r, string s, string v)
        {
            var rBytes = r.HexToByteArray();
            var sBytes = s.HexToByteArray();
            var vBytes = v.HexToByteArray();
            
            var signature = new Signature(rBytes, sBytes, vBytes);
            if (signature.IsVSignedForChain())
            {
                var vBigInteger = vBytes.ToBigIntegerFromRLPDecoded();
                var chainId = VRecoveryAndChainCalculations.GetChainFromVChain(vBigInteger);
                return new LegacyTransactionChainId(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(), gas.ToBytesForRLPEncoding(),
                    to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray(), chainId.ToBytesForRLPEncoding(), rBytes, sBytes, vBytes);
            }
            else
            {
                return new LegacyTransaction(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(), gas.ToBytesForRLPEncoding(),
                    to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray(), rBytes, sBytes, vBytes[0]);
            }
        }

        public static ISignedTransaction Create1559Transaction(BigInteger? chainId, BigInteger? nonce,
            BigInteger? maxPriorityFeePerGas, BigInteger? maxFeePerGas,
            BigInteger? gasLimit, string to, BigInteger? amount, string data,
            List<AccessListItem> accessList, string r, string s, string v)
        {
            var rBytes = r.HexToByteArray();
            var sBytes = s.HexToByteArray();
            var vBytes = v.HexToByteArray();

            var signature = new Signature(rBytes, sBytes, vBytes);
            return new Transaction1559(chainId ?? 0, nonce ?? 0, maxPriorityFeePerGas ?? 0, maxFeePerGas ?? 0,
                gasLimit ?? 0, to, amount ?? 0, data, accessList,
                signature);
        }



        public static ISignedTransaction CreateTransaction(BigInteger? chainId, byte? transactionType, BigInteger? nonce,
            BigInteger? maxPriorityFeePerGas, BigInteger? maxFeePerGas, BigInteger? gasPrice,
            BigInteger? gasLimit, string to, BigInteger? amount, string data,
            List<AccessListItem> accessList, string r, string s, string v)
        {
            if (transactionType.HasValue && transactionType == (int)TransactionType.EIP1559)
            {
                return Create1559Transaction(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, to, amount,
                    data, accessList, r, s, v);
            }
            
            if(!transactionType.HasValue || transactionType == 0)
            {
                return CreateLegacyTransaction(to, gasLimit ?? 0, gasPrice ?? 0, amount ?? 0, data, nonce ?? 0, r, s,
                    v);
            }

            throw new NotImplementedException(
                "Transaction type has not been implemented: " + transactionType.ToString());

        }
        
    }
}