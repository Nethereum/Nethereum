using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.Execution
{
    public interface IPrecompileProvider
    {
        IEnumerable<string> GetHandledAddresses();
        bool CanHandle(string address);
        BigInteger GetGasCost(string address, byte[] data);
        byte[] Execute(string address, byte[] data);
    }
}
