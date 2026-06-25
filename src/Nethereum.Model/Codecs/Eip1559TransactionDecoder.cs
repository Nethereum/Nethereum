using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Transaction decoder for London through Cancun-1. Accepts legacy +
    /// type bytes 0x01 (EIP-2930), 0x02 (EIP-1559). Rejects 0x03 (EIP-4844)
    /// and later.
    /// </summary>
    public sealed class Eip1559TransactionDecoder : ITransactionDecoder
    {
        public static readonly Eip1559TransactionDecoder Instance = new Eip1559TransactionDecoder();

        public ISignedTransaction Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are empty.", nameof(rawBytes));

            if (rawBytes[0] <= 0x7f && rawBytes[0] != 0x01 && rawBytes[0] != 0x02)
                throw new InvalidOperationException(
                    $"Transaction type byte 0x{rawBytes[0]:x2} is not accepted at London..Cancun-1 (only 0x01 EIP-2930, 0x02 EIP-1559, and legacy).");

            return TransactionFactory.CreateTransaction(rawBytes);
        }
    }
}
