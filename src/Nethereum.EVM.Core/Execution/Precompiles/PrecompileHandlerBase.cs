using Nethereum.Documentation;
using System;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.Precompiles
{
    public abstract class PrecompileHandlerBase : IPrecompileHandler
    {
        public abstract int AddressNumeric { get; }

        public abstract byte[] Execute(byte[] input);

        [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "A handler reports invalid input as a value")]
        public virtual bool TryExecute(byte[] input, out byte[] output)
        {
            return TryThroughThrowingOperation(() => Execute(input), out output);
        }

        protected static bool TryThroughThrowingOperation(Func<byte[]> operation, out byte[] output)
        {
            try
            {
                output = operation();
                return true;
            }
            catch (Exception ex) when (!EvmHostException.IsHostOrSystemFault(ex))
            {
                output = null;
                return false;
            }
        }

        protected static bool HasInputLength(byte[] input, int expected)
        {
            return (input?.Length ?? 0) == expected;
        }

        protected static bool HasInputMultiple(byte[] input, int chunkSize)
        {
            var actual = input?.Length ?? 0;
            return actual != 0 && (actual % chunkSize) == 0;
        }

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
