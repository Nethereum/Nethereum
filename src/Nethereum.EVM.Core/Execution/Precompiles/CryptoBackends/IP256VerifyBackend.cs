namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompile 0x100 (P256VERIFY, EIP-7951). Verifies a
    /// secp256r1 (NIST P-256) ECDSA signature over a pre-validated input
    /// tuple. The handler is responsible for the fixed 160-byte input
    /// layout and for packaging the 32-byte <c>0x00..01</c> / empty output
    /// based on the boolean returned here.
    /// </summary>
    public interface IP256VerifyBackend
    {
        /// <summary>
        /// Returns <c>true</c> if <paramref name="r"/> / <paramref name="s"/>
        /// is a valid P-256 ECDSA signature of <paramref name="hash"/>
        /// under the public key <c>(publicKeyX, publicKeyY)</c>. All inputs
        /// are 32-byte big-endian values. Implementations must return
        /// <c>false</c> (never throw) on invalid points, malformed inputs,
        /// or signature mismatches.
        /// </summary>
        bool Verify(byte[] hash, byte[] r, byte[] s, byte[] publicKeyX, byte[] publicKeyY);
    }
}
