using System.Numerics;
using Nethereum.Model;

namespace Nethereum.Signer
{

    public abstract class SignedLegacyTransaction: SignedLegacyTransactionBase
    {
        public static RLPSigner CreateDefaultRLPSigner(byte[] rawData)
        {
            return new RLPSigner(rawData, NUMBER_ENCODING_ELEMENTS);  
        }

        //Number of encoding elements (output for transaction)
        public const int NUMBER_ENCODING_ELEMENTS = 6;

        public static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("20000000000");
        public static readonly BigInteger DEFAULT_GAS_LIMIT = BigInteger.Parse("21000");

        public byte[] Nonce => SimpleRlpSigner.Data[0] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] Value => SimpleRlpSigner.Data[4] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] ReceiveAddress => SimpleRlpSigner.Data[3];

        public byte[] GasPrice => SimpleRlpSigner.Data[1] ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] GasLimit => SimpleRlpSigner.Data[2];

        public byte[] Data => SimpleRlpSigner.Data[5];

        public override void Sign(EthECKey key)
        {
            SimpleRlpSigner.SignLegacy(key);
        }
    }
}