namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x09 (BLAKE2F, EIP-152). Runs a single
    /// BLAKE2b compression round block. The handler is responsible for
    /// parsing the 213-byte precompile input layout (rounds header, state,
    /// message block, counter, final flag) and formatting the 64-byte
    /// output; the backend only performs the compression primitive.
    /// </summary>
    public interface IBlake2fBackend
    {
        /// <summary>
        /// Runs <paramref name="rounds"/> iterations of the BLAKE2b
        /// compression function over <paramref name="h"/> (8 × u64 state,
        /// mutated in place), <paramref name="m"/> (16 × u64 message
        /// block), the 128-bit counter (<paramref name="t0"/>, <paramref
        /// name="t1"/>), and the <paramref name="finalBlock"/> flag.
        /// </summary>
        void Compress(uint rounds, ulong[] h, ulong[] m, ulong t0, ulong t1, bool finalBlock);
    }
}
