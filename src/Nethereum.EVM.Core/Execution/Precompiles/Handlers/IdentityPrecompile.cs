using System;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x04 — IDENTITY (datacopy). Returns the input unchanged.
    /// Available in every fork.
    ///
    /// Gas cost lives on the fork's <see cref="PrecompileGasCalculators"/>.
    /// See Frontier specification and Yellow Paper Appendix E.
    /// </summary>
    public sealed class IdentityPrecompile : PrecompileHandlerBase
    {
        public override int AddressNumeric => 4;

        public override byte[] Execute(byte[] input)
        {
            return input ?? new byte[0];
        }
    }
}
