using System.Threading;

namespace Nethereum.Signer
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.Signing, "signer-secp256k1-contexts", "Secp256k1Contexts")]
    public static class Secp256k1Contexts
    {
        private const string WarmupKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";

        private static readonly object Gate = new object();
        private static int _initialised;

        public static bool IsInitialised => Volatile.Read(ref _initialised) == 1;

        public static void EnsureInitialised()
        {
            if (IsInitialised) return;
            lock (Gate)
            {
                if (IsInitialised) return;
                RunSignAndRecover();
                Volatile.Write(ref _initialised, 1);
            }
        }

        private static void RunSignAndRecover()
        {
#if NETCOREAPP3_1 || NET5_0_OR_GREATER
            var key = new EthECKey(WarmupKey);
            var hash = new byte[32];
            hash[31] = 1;
            var signature = key.SignAndCalculateV(hash);
            EthECKey.RecoverFromSignature(signature, hash);
#endif
        }
    }
}
