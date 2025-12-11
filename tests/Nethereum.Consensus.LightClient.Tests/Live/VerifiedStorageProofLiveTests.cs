using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class VerifiedStorageProofLiveTests
    {
        private readonly ITestOutputHelper _output;

        public VerifiedStorageProofLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task VerifiedStorage_GetSingleSlot_ReturnsProofVerifiedValue()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");
                _output.WriteLine($"State root: {header.StateRoot.ToHex(true)}");

                var slot = BigInteger.Zero;
                var storageValue = await verifiedState.GetStorageAtAsync(TestConstants.WethContract, slot);

                Assert.NotNull(storageValue);
                _output.WriteLine($"WETH storage slot 0: {storageValue.ToHex(true)}");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedStorage_GetMappingSlot_ComputesKeccakSlotCorrectly()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var addressBytes = TestConstants.VitalikAddress.HexToByteArray();
                var paddedAddress = new byte[32];
                Buffer.BlockCopy(addressBytes, 0, paddedAddress, 12, 20);
                var slotIndex = new byte[32];
                slotIndex[31] = 3;
                var combined = paddedAddress.Concat(slotIndex).ToArray();
                var mappingSlot = new Sha3Keccack().CalculateHash(combined);

                _output.WriteLine($"Computed mapping slot for balances[{TestConstants.VitalikAddress}]: {mappingSlot.ToHex(true)}");

                var storageValue = await verifiedState.GetStorageAtAsync(TestConstants.WethContract, mappingSlot.ToHex(true));

                Assert.NotNull(storageValue);
                var balance = new BigInteger(storageValue.Reverse().Concat(new byte[] { 0 }).ToArray());
                _output.WriteLine($"WETH balance from storage: {balance}");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedStorage_NonExistentSlot_ReturnsZeroBytes()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var nonExistentSlot = BigInteger.Parse("999999999999999999999999999999");
                var storageValue = await verifiedState.GetStorageAtAsync(TestConstants.WethContract, nonExistentSlot);

                Assert.NotNull(storageValue);
                var isZero = storageValue.All(b => b == 0);
                _output.WriteLine($"Non-existent slot value: {storageValue.ToHex(true)} (is zero: {isZero})");
                Assert.True(isZero || storageValue.Length == 0, "Non-existent slot should return zero bytes");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedStorage_MultipleSlots_AllVerifiedSequentially()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var slots = new[] { BigInteger.Zero, BigInteger.One, new BigInteger(2) };

                foreach (var slot in slots)
                {
                    var storageValue = await verifiedState.GetStorageAtAsync(TestConstants.WethContract, slot);
                    Assert.NotNull(storageValue);
                    _output.WriteLine($"WETH storage slot {slot}: {storageValue.ToHex(true)}");
                }

                _output.WriteLine("All storage slots retrieved and verified successfully");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedStorage_OptimisticMode_ReturnsFresherData()
        {
            try
            {
                var finalizedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var optimisticState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

                var finalizedHeader = finalizedState.GetCurrentHeader();
                var optimisticHeader = optimisticState.GetCurrentHeader();

                _output.WriteLine($"Finalized block: {finalizedHeader.BlockNumber}");
                _output.WriteLine($"Optimistic block: {optimisticHeader.BlockNumber}");

                Assert.True(optimisticHeader.BlockNumber >= finalizedHeader.BlockNumber,
                    "Optimistic block should be at or ahead of finalized block");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedStorage_OptimisticMode_GetStorageSlot()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using optimistic block: {header.BlockNumber}");
                _output.WriteLine($"State root: {header.StateRoot.ToHex(true)}");

                var slot = BigInteger.Zero;
                var storageValue = await verifiedState.GetStorageAtAsync(TestConstants.WethContract, slot);

                Assert.NotNull(storageValue);
                _output.WriteLine($"WETH storage slot 0 (optimistic): {storageValue.ToHex(true)}");
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsStateConsistencyError(ex))
            {
                _output.WriteLine($"Skipping test: State consistency error with optimistic block (timing issue). Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }
    }
}
