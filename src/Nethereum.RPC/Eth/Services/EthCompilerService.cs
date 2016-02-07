using edjCase.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.Compilation;

namespace Nethereum.Web3
{
    public class EthCompilerService:RpcClientWrapper
    {
        public EthCompilerService(RpcClient client) : base(client)
        {
            CompileLLL = new EthCompileLLL(client);
            CompileSerpent = new EthCompileSerpent(client);
            CompileSolidity = new EthCompileSolidity(client);
            GetCompilers = new EthGetCompilers(client);
        }

        public EthGetCompilers GetCompilers { get; private set; }
        public EthCompileLLL CompileLLL { get; private set; }
        public EthCompileSerpent CompileSerpent { get; private set; }
        public EthCompileSolidity CompileSolidity { get; private set; }
    }
}