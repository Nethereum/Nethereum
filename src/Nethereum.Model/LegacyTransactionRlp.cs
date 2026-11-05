using Nethereum.RLP;

namespace Nethereum.Model
{
    public static class LegacyTransactionRlp
    {
        private static readonly LegacyTransactionField[] ScalarFields =
        {
            LegacyTransactionField.Nonce,
            LegacyTransactionField.GasPrice,
            LegacyTransactionField.GasLimit,
            LegacyTransactionField.Value
        };

        private static readonly LegacyTransactionField[] TwoHundredFiftySixBitFields =
        {
            LegacyTransactionField.Nonce,
            LegacyTransactionField.Value
        };

        private const int AddressBytes = 20;
        private const int SixtyFourBitBytes = 8;

        public static void RejectMalformedScalars(byte[] rlp)
        {
            SignedRlpFields fields;
            try
            {
                fields = RLPSignedDataDecoder.ReadFields(rlp, SignedLegacyTransaction.NUMBER_ENCODING_ELEMENTS);
            }
            catch
            {
                return;
            }

            RejectMalformedScalars(fields);
        }

        internal static void RejectMalformedScalars(SignedRlpFields fields)
        {
            foreach (var field in ScalarFields)
            {
                if (!RlpScalar.IsCanonical(fields.Data[(int)field]))
                    throw new NonCanonicalScalarRlpException(field);
            }

            RejectWiderThan64Bits(fields.Data[(int)LegacyTransactionField.GasLimit], LegacyTransactionField.GasLimit);

            foreach (var field in TwoHundredFiftySixBitFields)
            {
                var scalar = fields.Data[(int)field];
                if (!RlpScalar.FitsA256BitField(scalar))
                    throw new ScalarWiderThanItsFieldException(field, scalar.Length);
            }

            var receiver = fields.Data[(int)LegacyTransactionField.ReceiveAddress];
            if (receiver != null && receiver.Length != AddressBytes)
                throw new TransactionFieldFormatException(LegacyTransactionField.ReceiveAddress, receiver.Length + " bytes");

            if (!fields.Signed) return;
            RejectMalformedScalar(fields.V, LegacyTransactionField.SignatureV);
            RejectMalformedScalar(fields.R, LegacyTransactionField.SignatureR);
            RejectMalformedScalar(fields.S, LegacyTransactionField.SignatureS);
        }

        internal static void RejectWiderThan64Bits(byte[] scalar, LegacyTransactionField field)
        {
            if (scalar != null && scalar.Length > SixtyFourBitBytes)
                throw new ScalarWiderThanItsFieldException(field, scalar.Length, SixtyFourBitBytes);
        }

        internal static void RejectMalformedScalar(byte[] scalar, LegacyTransactionField field)
        {
            if (!RlpScalar.IsCanonical(scalar))
                throw new NonCanonicalScalarRlpException(field);
            if (!RlpScalar.FitsA256BitField(scalar))
                throw new ScalarWiderThanItsFieldException(field, scalar.Length);
        }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-decode-exceptions", "ScalarWiderThanItsFieldException")]
    public class ScalarWiderThanItsFieldException : System.Exception
    {
        public LegacyTransactionField Field { get; }

        public int ByteCount { get; }

        public ScalarWiderThanItsFieldException(LegacyTransactionField field, int byteCount)
            : this(field, byteCount, 32)
        {
        }

        public ScalarWiderThanItsFieldException(LegacyTransactionField field, int byteCount, int fieldBytes)
            : base("RLP_WRONGVALUE_" + field.ToString().ToUpperInvariant() +
                   ": the scalar is " + byteCount + " bytes and a " + fieldBytes * 8 + "-bit field holds " + fieldBytes + ".")
        {
            Field = field;
            ByteCount = byteCount;
        }
    }

    public class NonCanonicalScalarRlpException : System.Exception
    {
        public LegacyTransactionField Field { get; }

        public NonCanonicalScalarRlpException(LegacyTransactionField field)
            : base("RLP_LEADING_ZEROS_" + field.ToString().ToUpperInvariant() +
                   ": the scalar is encoded with a leading zero byte, which the Yellow Paper " +
                   "dismisses completely. Zero is the empty string, never 0x00.")
        {
            Field = field;
        }
    }
}
