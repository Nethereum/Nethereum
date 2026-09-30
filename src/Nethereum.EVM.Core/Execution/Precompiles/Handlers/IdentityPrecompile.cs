using System;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class IdentityPrecompile : PrecompileHandlerBase
    {
        public override int AddressNumeric => 4;

        public override byte[] Execute(byte[] input)
        {
            return input ?? new byte[0];
        }
    }
}
