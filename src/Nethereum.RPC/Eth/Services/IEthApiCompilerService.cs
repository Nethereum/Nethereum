using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.RPC.Eth.Services
{
    public interface IEthApiCompilerService
    {
        IEthCompileLLL CompileLLL { get; }
        IEthCompileSerpent CompileSerpent { get; }
        IEthCompileSolidity CompileSolidity { get; }
        IEthGetCompilers GetCompilers { get; }
    }
}