using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    internal sealed class StubConfigurableNonceRpcClient : ClientBase
    {
        private readonly string _word;

        public StubConfigurableNonceRpcClient(BigInteger onChainNonce)
        {
            var valueBytes = onChainNonce.ToByteArray(isUnsigned: true, isBigEndian: true);
            var word = new byte[32];
            Array.Copy(valueBytes, 0, word, 32 - valueBytes.Length, valueBytes.Length);
            _word = "0x" + word.ToHex();
        }

        public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null) =>
            Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _word));

        protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
            Task.FromResult(requests.Select(r => new RpcResponseMessage(r.Id, _word)).ToArray());
    }
}
