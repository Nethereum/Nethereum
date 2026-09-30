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

        public static void RejectMalformedScalars(byte[] rlp)
        {
            var items = DecodedListOrNullWhenStructurallyUnreadable(rlp);
            if (items == null) return;

            RefuseANonCanonicalEncoding(items);
            RefuseAScalarWiderThanItsField(items);
        }

        private static void RefuseANonCanonicalEncoding(RLPCollection items)
        {
            foreach (var field in ScalarFields)
            {
                if ((int)field >= items.Count) return;

                if (!RlpScalar.IsCanonical(items[(int)field].RLPData))
                    throw new NonCanonicalScalarRlpException(field);
            }
        }

        private static void RefuseAScalarWiderThanItsField(RLPCollection items)
        {
            foreach (var field in TwoHundredFiftySixBitFields)
            {
                if ((int)field >= items.Count) return;

                var scalar = items[(int)field].RLPData;
                if (!RlpScalar.FitsA256BitField(scalar))
                    throw new ScalarWiderThanItsFieldException(field, scalar.Length);
            }
        }

        private static RLPCollection DecodedListOrNullWhenStructurallyUnreadable(byte[] rlp)
        {
            if (rlp == null || rlp.Length == 0) return null;

            try
            {
                return RLP.RLP.Decode(rlp) as RLPCollection;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ScalarWiderThanItsFieldException : System.Exception
    {
        public LegacyTransactionField Field { get; }

        public int ByteCount { get; }

        public ScalarWiderThanItsFieldException(LegacyTransactionField field, int byteCount)
            : base("RLP_WRONGVALUE_" + field.ToString().ToUpperInvariant() +
                   ": the scalar is " + byteCount + " bytes and a 256-bit field holds 32.")
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
