using System;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S7.Round2
{
    public class S7R2BatchLimitTests
    {
        private static RpcContext Context() => new RpcContext(
            (Func<IChainNode>)(() => throw new InvalidOperationException("no node")),
            new BigInteger(1),
            new ServiceCollection().BuildServiceProvider());

        [Fact]
        [Trait("Rule", "S7-R2-12")]
        [Trait("Erc", "JSON-RPC batch")]
        public async Task InstrumentedDispatchBatch_WithMoreRequestsThanTheBatchLimit_IsRejectedWholesaleLikeTheCoreDispatcher()
        {
            var requests = Enumerable.Range(0, 1001)
                .Select(i => new RpcRequestMessage(i, "eth_chainId"))
                .ToArray();
            var core = new RpcDispatcher(new RpcHandlerRegistry(), Context());
            using var metrics = new RpcMetrics("1");
            var instrumented = new InstrumentedRpcDispatcher(new RpcHandlerRegistry(), Context(), metrics);

            var coreResponses = await core.DispatchBatchAsync(requests);
            var instrumentedResponses = await instrumented.DispatchBatchAsync(requests);

            Assert.Single(coreResponses);
            Assert.Single(instrumentedResponses);
        }
    }
}
