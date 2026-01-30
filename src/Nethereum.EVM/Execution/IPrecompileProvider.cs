using System.Numerics;

namespace Nethereum.EVM.Execution
{
    public interface IPrecompileProvider
    {
        bool CanHandle(string address);
        BigInteger GetGasCost(string address, byte[] data);
        byte[] Execute(string address, byte[] data);
    }
}
