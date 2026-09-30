using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Transaction decoder for pre-EIP-2718 forks (Frontier through
    /// Berlin-1). Accepts only legacy transactions (RLP list, first byte
    /// &gt;= 0xc0). Rejects any typed envelope.
    /// </summary>
    public sealed class LegacyOnlyTransactionDecoder : ITransactionDecoder
    {
        public static readonly LegacyOnlyTransactionDecoder Instance = new LegacyOnlyTransactionDecoder();

        public ISignedTransaction Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are empty.", nameof(rawBytes));

            if (rawBytes[0] <= 0x7f)
                throw new InvalidOperationException(
                    "Typed transaction envelope (EIP-2718) is not valid at a pre-Berlin fork.");

            return TransactionFactory.CreateTransaction(rawBytes);
        }
    }
}
