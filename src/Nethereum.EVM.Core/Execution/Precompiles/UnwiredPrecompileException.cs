using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.Precompiles
{
    public class UnwiredPrecompileException : EvmHostException
    {
        public int AddressNumeric { get; }

        public UnwiredPrecompileException(int addressNumeric, string message) : base(message)
        {
            AddressNumeric = addressNumeric;
        }
    }
}
