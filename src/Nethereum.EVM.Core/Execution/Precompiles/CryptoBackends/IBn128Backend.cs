namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    /// <summary>
    /// Backend for precompiles 0x06–0x08 (alt_bn128 G1 addition, scalar
    /// multiplication, and optimal ate pairing — EIP-196 / EIP-197,
    /// repriced by EIP-1108 in Istanbul). Each method takes the full
    /// precompile input and returns the full precompile output; the
    /// handler passes CALL data through unchanged.
    /// </summary>
    public interface IBn128Backend
    {
        /// <summary>0x06 — ECADD on alt_bn128. Returns 64-byte output.</summary>
        byte[] Add(byte[] input);

        /// <summary>0x07 — ECMUL on alt_bn128. Returns 64-byte output.</summary>
        byte[] Mul(byte[] input);

        /// <summary>
        /// 0x08 — ECPAIRING on alt_bn128. Returns 32-byte output:
        /// <c>0x00..01</c> if the pairing product equals 1, <c>0x00..00</c>
        /// otherwise.
        /// </summary>
        byte[] Pairing(byte[] input);
    }
}
