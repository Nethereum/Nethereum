using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Transaction decoder for Cancun through Prague-1. Accepts legacy +
    /// type bytes 0x01, 0x02, 0x03 (EIP-4844 blob). Rejects 0x04 (EIP-7702)
    /// and later.
    /// </summary>
    public sealed class Eip4844TransactionDecoder : ITransactionDecoder
    {
        public static readonly Eip4844TransactionDecoder Instance = new Eip4844TransactionDecoder();

        public ISignedTransaction Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are empty.", nameof(rawBytes));

            if (rawBytes[0] <= 0x7f && rawBytes[0] != 0x01 && rawBytes[0] != 0x02 && rawBytes[0] != 0x03)
                throw new InvalidOperationException(
                    $"Transaction type byte 0x{rawBytes[0]:x2} is not accepted at Cancun..Prague-1 (only 0x01, 0x02, 0x03 EIP-4844, and legacy).");

            return TransactionFactory.CreateTransaction(rawBytes);
        }
    }
}
