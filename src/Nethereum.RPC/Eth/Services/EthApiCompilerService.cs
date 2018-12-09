using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiCompilerService : RpcClientWrapper, IEthApiCompilerService
    {
        public EthApiCompilerService(IClient client) : base(client)
        {
            CompileLLL = new EthCompileLLL(client);
            CompileSerpent = new EthCompileSerpent(client);
            CompileSolidity = new EthCompileSolidity(client);
            GetCompilers = new EthGetCompilers(client);
        }

        public IEthGetCompilers GetCompilers { get; private set; }
        public IEthCompileLLL CompileLLL { get; private set; }
        public IEthCompileSerpent CompileSerpent { get; private set; }
        public IEthCompileSolidity CompileSolidity { get; private set; }
    }
}