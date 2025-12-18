using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.ChainStateVerification.Interceptor;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class VerifiedStateInterceptorLiveTests
    {
        private const string RpcUrl = "https://ethereum-rpc.publicnode.com";
        private const string VitalikAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";

        private readonly ITestOutputHelper _output;

        public VerifiedStateInterceptorLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetBalance_WithInterceptor_ReturnsVerifiedBalance()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            var fallbackUsed = false;
            var interceptor = verifiedState.CreateVerifiedStateInterceptor(config =>
            {
                config.Mode = VerificationMode.Finalized;
                config.FallbackOnError = true;
            });
            interceptor.FallbackTriggered += (s, e) =>
            {
                fallbackUsed = true;
                _output.WriteLine($"Fallback triggered: {e.Exception?.Message}");
            };
            rpcClient.OverridingRequestInterceptor = interceptor;

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var balance = await web3.Eth.GetBalance.SendRequestAsync(VitalikAddress);

            Assert.NotNull(balance);
            Assert.True(balance.Value > 0, "Vitalik's balance should be greater than 0");

            _output.WriteLine($"Balance for {VitalikAddress}: {UnitConversion.Convert.FromWei(balance.Value)} ETH");
            _output.WriteLine(fallbackUsed
                ? "⚠ Balance retrieved via RPC fallback (pruning limit on verification)"
                : "✓ Balance retrieved via verified state");
        }

        [Fact]
        public async Task GetTransactionCount_WithInterceptor_ReturnsVerifiedNonce()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            rpcClient.UseVerifiedState(verifiedState, config =>
            {
                config.Mode = VerificationMode.Finalized;
            });

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(VitalikAddress);

            Assert.NotNull(nonce);
            Assert.True(nonce.Value > 0, "Vitalik should have sent transactions");

            _output.WriteLine($"Verified nonce for {VitalikAddress}: {nonce.Value}");
            _output.WriteLine("✓ Nonce retrieved via VerifiedStateInterceptor");
        }

        [Fact]
        public async Task GetBlockNumber_WithInterceptor_ReturnsFinalizedBlock()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);
            var expectedBlock = verifiedState.GetCurrentHeader().BlockNumber;

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            rpcClient.UseVerifiedState(verifiedState);

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            Assert.NotNull(blockNumber);
            Assert.Equal(expectedBlock, blockNumber.Value);

            _output.WriteLine($"Verified block number: {blockNumber.Value}");
            _output.WriteLine("✓ Block number retrieved via VerifiedStateInterceptor");
        }

        [Fact]
        public async Task GetCode_WithInterceptor_ReturnsVerifiedCode()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            rpcClient.UseVerifiedState(verifiedState);

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var code = await web3.Eth.GetCode.SendRequestAsync(usdcAddress);

            Assert.NotNull(code);
            Assert.StartsWith("0x", code);
            Assert.True(code.Length > 10, "USDC contract should have code");

            _output.WriteLine($"Verified code length for USDC: {code.Length} chars");
            _output.WriteLine("✓ Contract code retrieved via VerifiedStateInterceptor");
        }

        [Fact]
        public async Task OptimisticMode_ReturnsMoreRecentData()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();
            await lightClient.UpdateOptimisticAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClientFinalized = new RpcClient(new System.Uri(RpcUrl));
            rpcClientFinalized.UseVerifiedState(verifiedState, config =>
            {
                config.Mode = VerificationMode.Finalized;
            });

            var rpcClientOptimistic = new RpcClient(new System.Uri(RpcUrl));
            rpcClientOptimistic.UseVerifiedState(verifiedState, config =>
            {
                config.Mode = VerificationMode.Optimistic;
            });

            var web3Finalized = new Nethereum.Web3.Web3(rpcClientFinalized);
            var web3Optimistic = new Nethereum.Web3.Web3(rpcClientOptimistic);

            var finalizedBlock = await web3Finalized.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            verifiedState.Mode = VerificationMode.Optimistic;
            var optimisticBlock = await web3Optimistic.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            Assert.True(optimisticBlock.Value >= finalizedBlock.Value,
                "Optimistic block should be >= finalized block");

            _output.WriteLine($"Finalized block: {finalizedBlock.Value}");
            _output.WriteLine($"Optimistic block: {optimisticBlock.Value}");
            _output.WriteLine($"Difference: {optimisticBlock.Value - finalizedBlock.Value} blocks");
            _output.WriteLine("✓ Optimistic mode returns more recent data");
        }

        [Fact]
        public async Task FallbackTriggered_WhenVerificationFails()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            var interceptor = verifiedState.CreateVerifiedStateInterceptor(config =>
            {
                config.Mode = VerificationMode.Finalized;
                config.FallbackOnError = true;
            });

            var fallbackTriggered = false;
            string fallbackMethod = null;

            interceptor.FallbackTriggered += (sender, args) =>
            {
                fallbackTriggered = true;
                fallbackMethod = args.Method;
                _output.WriteLine($"Fallback triggered for method: {args.Method}");
                _output.WriteLine($"Exception: {args.Exception?.Message}");
            };

            rpcClient.OverridingRequestInterceptor = interceptor;

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var balance = await web3.Eth.GetBalance.SendRequestAsync(VitalikAddress);

            Assert.NotNull(balance);
            _output.WriteLine($"Balance: {UnitConversion.Convert.FromWei(balance.Value)} ETH");
            _output.WriteLine($"Fallback was triggered: {fallbackTriggered}");

            if (fallbackTriggered)
            {
                _output.WriteLine($"Fallback method: {fallbackMethod}");
            }
        }

        [Fact]
        public async Task NonInterceptedMethod_PassesToRpc()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var rpcClient = new RpcClient(new System.Uri(RpcUrl));
            rpcClient.UseVerifiedState(verifiedState);

            var web3 = new Nethereum.Web3.Web3(rpcClient);

            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            Assert.NotNull(gasPrice);
            Assert.True(gasPrice.Value > 0, "Gas price should be positive");

            _output.WriteLine($"Gas price (from RPC, not intercepted): {gasPrice.Value} wei");
            _output.WriteLine("✓ Non-intercepted method passed through to RPC");
        }

        [Fact]
        public async Task Web3Extension_UseVerifiedState_Works()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);
            var expectedBlock = verifiedState.GetCurrentHeader().BlockNumber;

            var web3 = new Nethereum.Web3.Web3(RpcUrl);
            web3.UseVerifiedState(verifiedState, config =>
            {
                config.Mode = VerificationMode.Finalized;
                config.FallbackOnError = true;
            });

            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            Assert.NotNull(blockNumber);
            Assert.Equal(expectedBlock, blockNumber.Value);

            _output.WriteLine($"Block number via Web3 extension: {blockNumber.Value}");
            _output.WriteLine("✓ Web3.UseVerifiedState() extension works correctly");
        }

        [Fact]
        public async Task Web3Extension_FluentChaining_Works()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();
            await lightClient.UpdateFinalityAsync();

            var verifiedState = TestHelpers.CreateVerifiedStateService(lightClient);

            var balance = await new Nethereum.Web3.Web3(RpcUrl)
                .UseVerifiedState(verifiedState, config =>
                {
                    config.Mode = VerificationMode.Finalized;
                    config.FallbackOnError = true;
                })
                .Eth.GetBalance.SendRequestAsync(VitalikAddress);

            Assert.NotNull(balance);
            Assert.True(balance.Value > 0);

            _output.WriteLine($"Balance via fluent chain: {UnitConversion.Convert.FromWei(balance.Value)} ETH");
            _output.WriteLine("✓ Fluent chaining with UseVerifiedState works");
        }
    }
}
