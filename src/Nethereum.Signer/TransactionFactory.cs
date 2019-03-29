using Nethereum.Hex.HexConvertors.Extensions;

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
    }
}