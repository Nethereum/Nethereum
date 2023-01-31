using System.Numerics;

namespace Nethereum.Model
{

    public abstract class SignedLegacyTransaction: SignedLegacyTransactionBase
    {
        public static RLPSignedDataHashBuilder CreateDefaultRLPSigner(byte[] rawData)
        {
            return new RLPSignedDataHashBuilder(rawData, NUMBER_ENCODING_ELEMENTS);  
        }

        //Number of encoding elements (output for transaction)
        public const int NUMBER_ENCODING_ELEMENTS = 6;

        public static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("20000000000");
        public static readonly BigInteger DEFAULT_GAS_LIMIT = BigInteger.Parse("21000");

        public byte[] Nonce => RlpSignerEncoder.Data[0] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] Value => RlpSignerEncoder.Data[4] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] ReceiveAddress => RlpSignerEncoder.Data[3];

        public byte[] GasPrice => RlpSignerEncoder.Data[1] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] GasLimit => RlpSignerEncoder.Data[2];

        public byte[] Data => RlpSignerEncoder.Data[5];

    }
}