using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System.Numerics;

namespace Nethereum.Signer
{
    public class TransactionFactory
    {
        public static SignedTransactionBase CreateTransaction(string rlpHex)
        {
            return CreateTransaction(rlpHex.HexToByteArray());
        }

        public static SignedTransactionBase CreateTransaction(byte[] rlp)
        {
            var rlpSigner = SignedTransactionBase.CreateDefaultRLPSigner(rlp);
            return rlpSigner.IsVSignatureForChain()
                ? (SignedTransactionBase) new TransactionChainId(rlpSigner)
                : new Transaction(rlpSigner);
        }

        public static SignedTransactionBase CreateTransaction(string to, BigInteger gas, BigInteger gasPrice, BigInteger amount, string data, BigInteger nonce, string r, string s, string v)
        {
            var rBytes = r.HexToByteArray();
            var sBytes = s.HexToByteArray();
            var vBytes = v.HexToByteArray();
            
            var signature = EthECDSASignatureFactory.FromComponents(rBytes, sBytes, vBytes);
            if (signature.IsVSignedForChain())
            {
                var vBigInteger = vBytes.ToBigIntegerFromRLPDecoded();
                var chainId = EthECKey.GetChainFromVChain(vBigInteger);
                return new TransactionChainId(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(), gas.ToBytesForRLPEncoding(),
                    to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray(), chainId.ToBytesForRLPEncoding(), rBytes, sBytes, vBytes);
            }
            else
            {
                return new Transaction(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(), gas.ToBytesForRLPEncoding(),
                    to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray(), rBytes, sBytes, vBytes[0]);
            }
        }
        
    }
}