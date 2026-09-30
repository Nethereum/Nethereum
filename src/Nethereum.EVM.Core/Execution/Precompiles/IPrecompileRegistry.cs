using System.Collections.Generic;

namespace Nethereum.EVM.Execution.Precompiles
{
    public interface IPrecompileRegistry
    {
        bool CanHandle(int address);

        IPrecompileHandler Get(int address);

        long GetGasCost(int address, byte[] input);

        byte[] Execute(int address, byte[] input);

        IEnumerable<int> GetAddresses();
    }
}
