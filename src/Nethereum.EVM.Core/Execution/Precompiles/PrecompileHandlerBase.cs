using System;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Optional abstract base for <see cref="IPrecompileHandler"/>
    /// implementations, mirroring the role of
    /// <c>Nethereum.Wallet.RpcRequests.RpcMethodHandlerBase</c>. Provides a
    /// handful of input-validation helpers and keeps concrete handlers
    /// uniform; handlers may also implement <see cref="IPrecompileHandler"/>
    /// directly if they prefer.
    /// </summary>
    public abstract class PrecompileHandlerBase : IPrecompileHandler
    {
        public abstract int AddressNumeric { get; }

        public abstract byte[] Execute(byte[] input);

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="input"/>
        /// is null or its length is not exactly <paramref name="expected"/>.
        /// Used by fixed-length precompiles (BLS12 G1ADD = 256, G2ADD = 512,
        /// MAP_FP_TO_G1 = 64, KZG point eval = 192, etc.).
        /// </summary>
        protected static void RequireInputLength(byte[] input, int expected, string name)
        {
            var actual = input?.Length ?? 0;
            if (actual != expected)
                throw new ArgumentException(
                    $"{name}: expected {expected} bytes, got {actual}");
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if <paramref name="input"/>
        /// is null/empty or its length is not a positive multiple of
        /// <paramref name="chunkSize"/>. Used by variable-length batched
        /// precompiles (BLS12 MSM = multiple of 160/288, PAIRING = multiple
        /// of 384).
        /// </summary>
        protected static void RequireInputMultiple(byte[] input, int chunkSize, string name)
        {
            var actual = input?.Length ?? 0;
            if (actual == 0 || (actual % chunkSize) != 0)
                throw new ArgumentException(
                    $"{name}: expected non-empty multiple of {chunkSize} bytes, got {actual}");
        }

        /// <summary>
        /// Returns <paramref name="input"/> or an empty array when null,
        /// so implementations can treat null and empty input uniformly.
        /// </summary>
        protected static byte[] OrEmpty(byte[] input)
        {
            return input ?? new byte[0];
        }
    }
}
