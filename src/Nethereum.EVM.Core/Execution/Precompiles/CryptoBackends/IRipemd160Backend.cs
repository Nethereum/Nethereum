namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x03 (RIPEMD160). Produces the raw 20-byte
    /// RIPEMD-160 digest; the handler is responsible for right-padding the
    /// result to 32 bytes for EVM consumption.
    /// </summary>
    public interface IRipemd160Backend
    {
        /// <summary>
        /// Returns the 20-byte RIPEMD-160 digest of <paramref name="input"/>.
        /// </summary>
        byte[] Hash(byte[] input);
    }
}
