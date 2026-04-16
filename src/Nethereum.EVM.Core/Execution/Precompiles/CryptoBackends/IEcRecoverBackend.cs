namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x01 (ECRECOVER). Implementations perform
    /// secp256k1 public-key recovery from a pre-validated <c>(hash, v, r, s)</c>
    /// tuple; the handler is responsible for all consensus-level input
    /// parsing, padding, and bounds checks before the backend is invoked.
    /// </summary>
    public interface IEcRecoverBackend
    {
        /// <summary>
        /// Recovers the 20-byte Ethereum address that produced the given
        /// signature, or returns <c>null</c>/empty on any recovery failure.
        /// </summary>
        byte[] Recover(byte[] hash, byte v, byte[] r, byte[] s);
    }
}
