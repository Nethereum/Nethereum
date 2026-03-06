using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class Web3Sha3Handler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.web3_sha3.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var dataHex = GetParam<string>(request, 0);
            var data = dataHex.HexToByteArray();
            var hash = new Sha3Keccack().CalculateHash(data);
            return Task.FromResult(Success(request.Id, hash.ToHex(true)));
        }
    }
}
