using System.Numerics;

namespace Nethereum.EVM.Execution
{
    public interface IPrecompiledContractsExecution
    {
        bool IsPrecompiledAddress(string address);
        BigInteger GetPrecompileGasCost(string address, byte[] data);
        byte[] ExecutePreCompile(string address, byte[] data);
    }
}
