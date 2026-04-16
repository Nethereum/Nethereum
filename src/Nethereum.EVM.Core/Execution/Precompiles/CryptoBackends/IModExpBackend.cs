namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x05 (MODEXP, EIP-198). Computes
    /// <c>base^exp mod modulus</c> on arbitrary-length big-endian operands.
    /// The handler is responsible for parsing the precompile's uint256
    /// length header, enforcing EIP-7823 bounds (Osaka), and packaging the
    /// output to the requested <paramref name="modulus"/> length.
    /// </summary>
    public interface IModExpBackend
    {
        /// <summary>
        /// Computes <c>base^exp mod modulus</c> over big-endian operands
        /// and returns the result right-padded (big-endian) to exactly
        /// <paramref name="modulus"/>.Length bytes. Implementations may
        /// assume <paramref name="modulus"/>.Length &gt; 0 and the modulus
        /// is non-zero.
        /// </summary>
        byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus);
    }
}
