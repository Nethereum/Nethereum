using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Transaction decoder for Prague onward (also covers Osaka and
    /// OsakaBpo1). Accepts legacy + type bytes 0x01, 0x02, 0x03, 0x04
    /// (EIP-7702 set-code).
    /// </summary>
    public sealed class Eip7702TransactionDecoder : ITransactionDecoder
    {
        public static readonly Eip7702TransactionDecoder Instance = new Eip7702TransactionDecoder();

        public ISignedTransaction Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are empty.", nameof(rawBytes));

            if (rawBytes[0] <= 0x7f
                && rawBytes[0] != 0x01 && rawBytes[0] != 0x02
                && rawBytes[0] != 0x03 && rawBytes[0] != 0x04)
                throw new InvalidOperationException(
                    $"Transaction type byte 0x{rawBytes[0]:x2} is not accepted at Prague-onward (only 0x01, 0x02, 0x03, 0x04 EIP-7702, and legacy).");

            return TransactionFactory.CreateTransaction(rawBytes);
        }
    }
}
