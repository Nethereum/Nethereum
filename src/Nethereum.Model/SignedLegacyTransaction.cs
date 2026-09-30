using Nethereum.Util;

namespace Nethereum.Model
{
    public enum LegacyTransactionField
    {
        Nonce = 0,
        GasPrice = 1,
        GasLimit = 2,
        ReceiveAddress = 3,
        Value = 4,
        Data = 5
    }

    public abstract class SignedLegacyTransaction: SignedLegacyTransactionBase
    {
        public static RLPSignedDataHashBuilder CreateDefaultRLPSigner(byte[] rawData)
        {
            return new RLPSignedDataHashBuilder(rawData, NUMBER_ENCODING_ELEMENTS);
        }

        //Number of encoding elements (output for transaction)
        public const int NUMBER_ENCODING_ELEMENTS = 6;

        public static readonly EvmUInt256 DEFAULT_GAS_PRICE = new EvmUInt256(20000000000);
        public static readonly EvmUInt256 DEFAULT_GAS_LIMIT = new EvmUInt256(21000);

        private byte[] Field(LegacyTransactionField field) => RlpSignerEncoder.Data[(int)field];

        public byte[] Nonce => Field(LegacyTransactionField.Nonce) ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] Value => Field(LegacyTransactionField.Value) ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] ReceiveAddress => Field(LegacyTransactionField.ReceiveAddress);

        public byte[] GasPrice => Field(LegacyTransactionField.GasPrice) ?? DefaultValues.ZERO_BYTE_ARRAY;

        public byte[] GasLimit => Field(LegacyTransactionField.GasLimit);

        public byte[] Data => Field(LegacyTransactionField.Data);

    }
}
