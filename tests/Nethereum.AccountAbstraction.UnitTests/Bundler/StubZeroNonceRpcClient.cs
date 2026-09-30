using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    internal sealed class StubZeroNonceRpcClient : ClientBase
    {
        private static readonly string ZeroWord = "0x" + new string('0', 64);

        public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null) =>
            Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, ZeroWord));

        protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
            Task.FromResult(requests.Select(r => new RpcResponseMessage(r.Id, ZeroWord)).ToArray());
    }
}
