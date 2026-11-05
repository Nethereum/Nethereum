using System;

namespace Nethereum.Signer
{
    internal static class SecureMemoryHelper
    {
        public static void ZeroMemory(byte[] buffer)
        {
            if (buffer == null) return;
#if NETCOREAPP3_1 || NET5_0_OR_GREATER || NETSTANDARD2_1
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer);
#else
            Array.Clear(buffer, 0, buffer.Length);
#endif
        }
    }
}
