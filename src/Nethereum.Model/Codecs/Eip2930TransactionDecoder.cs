using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Transaction decoder for Berlin (EIP-2930 access lists). Accepts
    /// legacy + type byte 0x01. Rejects 0x02 (EIP-1559) and later.
    /// </summary>
    public sealed class Eip2930TransactionDecoder : ITransactionDecoder
    {
        public static readonly Eip2930TransactionDecoder Instance = new Eip2930TransactionDecoder();

        public ISignedTransaction Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are empty.", nameof(rawBytes));

            if (rawBytes[0] <= 0x7f && rawBytes[0] != 0x01)
                throw new InvalidOperationException(
                    $"Transaction type byte 0x{rawBytes[0]:x2} is not accepted at Berlin (only 0x01 EIP-2930 and legacy).");

            return TransactionFactory.CreateTransaction(rawBytes);
        }
    }
}
