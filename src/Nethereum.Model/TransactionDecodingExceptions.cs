using Nethereum.Util;

namespace Nethereum.Model
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-decode-exceptions", "TransactionNonceOverflowException")]
    public class TransactionNonceOverflowException : System.Exception
    {
        public EvmUInt256 Nonce { get; }

        public TransactionNonceOverflowException(EvmUInt256 nonce)
            : base("NONCE_OVERFLOW: the transaction nonce " + nonce + " does not fit in 64 bits.")
        {
            Nonce = nonce;
        }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-decode-exceptions", "TransactionRlpLengthException")]
    public class TransactionRlpLengthException : System.Exception
    {
        public TransactionRlpLengthException(int availableBytes, int rlpItemBytes)
            : base("RLP_INVALID_LENGTH: the transaction carries " + availableBytes +
                   " bytes but its RLP item is " + rlpItemBytes + " bytes; a transaction is exactly one RLP item.")
        {
        }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-decode-exceptions", "TransactionFieldFormatException")]
    public class TransactionFieldFormatException : System.Exception
    {
        public LegacyTransactionField Field { get; }

        public TransactionFieldFormatException(LegacyTransactionField field, string found)
            : base("RLP_INVALID_" + field.ToString().ToUpperInvariant() + ": the field is " + found + ".")
        {
            Field = field;
        }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-decode-exceptions", "AuthorisationFormatException")]
    public class AuthorisationFormatException : System.Exception
    {
        public AuthorisationFormatException(string reason)
            : base("TYPE_4_INVALID_AUTHORIZATION_FORMAT: " + reason)
        {
        }
    }
}
