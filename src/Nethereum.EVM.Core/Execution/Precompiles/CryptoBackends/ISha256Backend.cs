namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x02 (SHA256). A straight hash over the input
    /// bytes; the handler passes through whatever CALL data it received.
    /// </summary>
    public interface ISha256Backend
    {
        /// <summary>
        /// Returns the 32-byte SHA-256 digest of <paramref name="input"/>.
        /// </summary>
        byte[] Hash(byte[] input);
    }
}
