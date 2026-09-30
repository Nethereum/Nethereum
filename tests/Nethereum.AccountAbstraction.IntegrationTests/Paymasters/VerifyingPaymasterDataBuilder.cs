using System;

namespace Nethereum.AccountAbstraction.IntegrationTests.Paymasters
{
    internal static class VerifyingPaymasterDataBuilder
    {
        public static byte[] Build(ulong validUntil, ulong validAfter, byte[] signature)
        {
            var data = new byte[6 + 6 + signature.Length];
            WriteUInt48BigEndian(validUntil, data, 0);
            WriteUInt48BigEndian(validAfter, data, 6);
            Buffer.BlockCopy(signature, 0, data, 12, signature.Length);
            return data;
        }

        private static void WriteUInt48BigEndian(ulong value, byte[] target, int offset)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 2, target, offset, 6);
        }
    }
}
