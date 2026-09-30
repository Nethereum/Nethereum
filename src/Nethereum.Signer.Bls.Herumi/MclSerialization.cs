using System;

namespace Nethereum.Signer.Bls.Herumi
{
    /// <summary>
    /// Herumi MCL's serialization mode and subgroup-check policy are process-global.
    /// The EVM EIP-2537 precompiles (mode 0) and the beacon light client (eth2 mode 1)
    /// share one process, so every MCL operation is serialised through this guard and
    /// bracketed with the mode the caller needs. ETH mode (1) is the steady state, so
    /// EVM operations restore it in a finally block; a concurrent beacon operation can
    /// never observe an EVM operation's mode-0 window because both hold the same lock.
    /// </summary>
    public static class MclSerialization
    {
        private static readonly object _lock = new object();

        public static T InEvmMode<T>(Func<T> op)
        {
            lock (_lock)
            {
                MclBindings.mclBn_setETHserialization(0);
                MclBindings.mclBn_verifyOrderG1(0);
                MclBindings.mclBn_verifyOrderG2(0);
                try { return op(); }
                finally
                {
                    MclBindings.mclBn_setETHserialization(1);
                    MclBindings.mclBn_verifyOrderG1(1);
                    MclBindings.mclBn_verifyOrderG2(1);
                }
            }
        }

        public static T InEthMode<T>(Func<T> op)
        {
            lock (_lock)
            {
                MclBindings.mclBn_setETHserialization(1);
                MclBindings.mclBn_verifyOrderG1(1);
                MclBindings.mclBn_verifyOrderG2(1);
                return op();
            }
        }
    }
}
