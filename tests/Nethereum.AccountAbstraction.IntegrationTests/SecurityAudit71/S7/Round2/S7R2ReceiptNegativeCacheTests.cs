using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round2
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R2ReceiptNegativeCacheTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public S7R2ReceiptNegativeCacheTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("Rule", "S7-R2-15")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationReceipt")]
        public async Task GetUserOperationReceipt_WhenTheSameUnknownHashIsAskedTwice_TheSecondLookupDoesNotReachTheNode()
        {
            var counting = new CountingRpcClient(_fixture.Web3.Client);
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                AutoBundleIntervalMs = 0,
                ReputationDecayIntervalMs = 0,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };
            using var bundler = new BundlerService(new Nethereum.Web3.Web3(counting), config);
            var unknownHash = "0x" + new string('c', 64);

            var first = await bundler.GetUserOperationReceiptAsync(unknownHash);
            var requestsAfterFirst = counting.Requests;
            var second = await bundler.GetUserOperationReceiptAsync(unknownHash);

            Assert.Null(first);
            Assert.Null(second);
            Assert.True(requestsAfterFirst > 0);
            Assert.Equal(requestsAfterFirst, counting.Requests);
        }

        private sealed class CountingRpcClient : ClientBase
        {
            private readonly IClient _inner;
            private int _requests;

            public CountingRpcClient(IClient inner)
            {
                _inner = inner;
            }

            public int Requests => Volatile.Read(ref _requests);

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                Interlocked.Increment(ref _requests);
                return _inner.SendAsync(rpcRequestMessage, route);
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
            {
                throw new NotSupportedException();
            }
        }
    }
}
