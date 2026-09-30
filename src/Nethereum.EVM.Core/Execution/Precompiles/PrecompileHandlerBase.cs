using System;

namespace Nethereum.EVM.Execution.Precompiles
{
    public abstract class PrecompileHandlerBase : IPrecompileHandler
    {
        public abstract int AddressNumeric { get; }

        public abstract byte[] Execute(byte[] input);

        protected static void RequireInputLength(byte[] input, int expected, string name)
        {
            var actual = input?.Length ?? 0;
            if (actual != expected)
                throw new ArgumentException(
                    $"{name}: expected {expected} bytes, got {actual}");
        }

        protected static void RequireInputMultiple(byte[] input, int chunkSize, string name)
        {
            var actual = input?.Length ?? 0;
            if (actual == 0 || (actual % chunkSize) != 0)
                throw new ArgumentException(
                    $"{name}: expected non-empty multiple of {chunkSize} bytes, got {actual}");
        }

        protected static byte[] OrEmpty(byte[] input)
        {
            return input ?? new byte[0];
        }
    }
}
