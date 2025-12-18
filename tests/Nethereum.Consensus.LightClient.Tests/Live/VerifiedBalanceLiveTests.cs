using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class VerifiedBalanceLiveTests
    {
        private readonly ITestOutputHelper _output;

        public VerifiedBalanceLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetBalance_OptimisticMode_ReturnsVerifiedBalance_NoFallback()
        {
            TestHelpers.EnsureNativeLibrary();

            var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

            var header = verifiedState.GetCurrentHeader();
            _output.WriteLine($"Using optimistic block: {header.BlockNumber}");
            _output.WriteLine($"State root: {header.StateRoot.ToHex(true)}");

            var balance = await verifiedState.GetBalanceAsync(TestConstants.VitalikAddress);

            Assert.True(balance >= 0, "Balance must be non-negative");
            _output.WriteLine($"Verified balance for {TestConstants.VitalikAddress}: {UnitConversion.Convert.FromWei(balance)} ETH");
            _output.WriteLine("✓ Balance verified via Merkle proof (optimistic mode, no fallback)");
        }

        [Fact]
        public async Task GetBalance_FinalizedMode_MayFailDueToPruning()
        {
            TestHelpers.EnsureNativeLibrary();

            var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

            var header = verifiedState.GetCurrentHeader();
            _output.WriteLine($"Using finalized block: {header.BlockNumber}");
            _output.WriteLine($"State root: {header.StateRoot.ToHex(true)}");

            try
            {
                var balance = await verifiedState.GetBalanceAsync(TestConstants.VitalikAddress);
                Assert.True(balance >= 0, "Balance must be non-negative");
                _output.WriteLine($"Verified balance: {UnitConversion.Convert.FromWei(balance)} ETH");
                _output.WriteLine("✓ Balance verified via Merkle proof (finalized mode)");
            }
            catch (Nethereum.JsonRpc.Client.RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Expected: Finalized block too old for RPC proof window. Error: {ex.Message}");
                _output.WriteLine("⚠ This is expected behavior - finalized blocks are ~12-15 min behind head");
                _output.WriteLine("   Use optimistic mode for standard RPC nodes, or archive node for finalized");
            }
        }

        [Fact]
        public async Task GetBalance_OptimisticBlockIsMoreRecent()
        {
            TestHelpers.EnsureNativeLibrary();

            var finalizedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
            var optimisticState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

            var finalizedHeader = finalizedState.GetCurrentHeader();
            var optimisticHeader = optimisticState.GetCurrentHeader();

            _output.WriteLine($"Finalized block: {finalizedHeader.BlockNumber}");
            _output.WriteLine($"Optimistic block: {optimisticHeader.BlockNumber}");
            _output.WriteLine($"Block difference: {optimisticHeader.BlockNumber - finalizedHeader.BlockNumber} blocks");

            Assert.True(optimisticHeader.BlockNumber >= finalizedHeader.BlockNumber,
                "Optimistic block should be at or ahead of finalized block");

            var blockDiff = optimisticHeader.BlockNumber - finalizedHeader.BlockNumber;
            Assert.True(blockDiff >= 32,
                $"Optimistic should be ~2 epochs (64 blocks) ahead, got {blockDiff} blocks difference");
        }

        [Fact]
        public async Task GetNonce_OptimisticMode_ReturnsVerifiedNonce_NoFallback()
        {
            TestHelpers.EnsureNativeLibrary();

            var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

            var header = verifiedState.GetCurrentHeader();
            _output.WriteLine($"Using optimistic block: {header.BlockNumber}");

            var nonce = await verifiedState.GetNonceAsync(TestConstants.VitalikAddress);

            Assert.True(nonce > 0, "Vitalik should have sent transactions");
            _output.WriteLine($"Verified nonce for {TestConstants.VitalikAddress}: {nonce}");
            _output.WriteLine("✓ Nonce verified via Merkle proof (optimistic mode, no fallback)");
        }
    }
}
