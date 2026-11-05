using System.Text.Json;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round2
{
    public class EnvelopeAndTimeoutTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string LoopContract = "0x1000000000000000000000000000000000000001";
        private const string LoopCode = "0x5b600056";
        private const string LoopOverride = "{\"" + LoopContract + "\":{\"code\":\"" + LoopCode + "\"}}";

        [Fact]
        [Trait("Finding", "S4-R2-02")]
        public async Task Given_AnEndlessLoopContractAndAShortRpcEvmTimeout_When_EthCallRunsIt_Then_ItIsAbortedWithinTheTimeout()
        {
            await using var host = await DevChainTimeoutHost.StartAsync();
            host.Node.Config.RpcEvmTimeout = TimeSpan.FromMilliseconds(1);
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0x2faf080\"}";

            var body = await host.CallAsync("eth_call", "[" + call + ",\"latest\"," + LoopOverride + "]");

            Assert.Contains("execution aborted", body);
        }

        [Fact]
        [Trait("Finding", "S4-R2-02")]
        public async Task Given_AnEndlessLoopContractAndAShortRpcEvmTimeout_When_EthEstimateGasRunsIt_Then_ItIsAbortedWithinTheTimeout()
        {
            await using var host = await DevChainTimeoutHost.StartAsync();
            await host.CallAsync("hardhat_setCode", "[\"" + LoopContract + "\",\"" + LoopCode + "\"]");
            host.Node.Config.RpcEvmTimeout = TimeSpan.FromMilliseconds(1);
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\"}";

            var body = await host.CallAsync("eth_estimateGas", "[" + call + ",\"latest\"]");

            Assert.Contains("execution aborted", body);
        }

        [Fact]
        [Trait("Finding", "S4-R2-02")]
        public async Task Given_AnEndlessLoopContractAndAShortRpcEvmTimeout_When_EthCreateAccessListRunsIt_Then_ItIsAbortedWithinTheTimeout()
        {
            await using var host = await DevChainTimeoutHost.StartAsync();
            await host.CallAsync("hardhat_setCode", "[\"" + LoopContract + "\",\"" + LoopCode + "\"]");
            host.Node.Config.RpcEvmTimeout = TimeSpan.FromMilliseconds(1);
            var call = "{\"from\":\"" + Sender + "\",\"to\":\"" + LoopContract + "\",\"gas\":\"0x2faf080\"}";

            var body = await host.CallAsync("eth_createAccessList", "[" + call + ",\"latest\"]");

            Assert.Contains("execution aborted", body);
        }
    }
}
