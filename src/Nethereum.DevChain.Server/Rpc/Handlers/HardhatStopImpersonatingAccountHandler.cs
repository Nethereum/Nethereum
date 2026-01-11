using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain.Server.Rpc.Handlers
{
    public class HardhatStopImpersonatingAccountHandler : RpcHandlerBase
    {
        public override string MethodName => "hardhat_stopImpersonatingAccount";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var accountManager = context.GetRequiredService<DevAccountManager>();
            accountManager.StopImpersonatingAccount(address);
            return Task.FromResult(Success(request.Id, true));
        }
    }
}
