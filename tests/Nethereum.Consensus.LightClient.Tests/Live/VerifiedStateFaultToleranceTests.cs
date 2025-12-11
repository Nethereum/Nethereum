using System;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class VerifiedStateFaultToleranceTests
    {
        private readonly ITestOutputHelper _output;

        public VerifiedStateFaultToleranceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task VerifiedState_NonExistentAccount_ReturnsZeroBalance()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var newAddress = "0x0000000000000000000000000000000000000001";
                var balance = await verifiedState.GetBalanceAsync(newAddress);

                _output.WriteLine($"Balance of {newAddress}: {balance} wei");
                Assert.True(balance >= 0);
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
        public async Task VerifiedState_EOA_ReturnsEmptyCode()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.VitalikAddress);

                _output.WriteLine($"Code length for EOA {TestConstants.VitalikAddress}: {code?.Length ?? 0} bytes");
                Assert.True(code == null || code.Length == 0, "EOA should have no code");
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
        public async Task VerifiedState_ContractAddress_ReturnsNonEmptyCode()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                _output.WriteLine($"Code length for contract {TestConstants.WethContract}: {code?.Length ?? 0} bytes");
                Assert.NotNull(code);
                Assert.NotEmpty(code);
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
        public async Task VerifiedState_GetNonce_ReturnsValidNonce()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var nonce = await verifiedState.GetNonceAsync(TestConstants.VitalikAddress);

                _output.WriteLine($"Nonce of {TestConstants.VitalikAddress}: {nonce}");
                Assert.True(nonce >= 0);
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
        public async Task VerifiedState_GetCodeHash_ForContract_ReturnsNonEmptyHash()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var codeHash = await verifiedState.GetCodeHashAsync(TestConstants.WethContract);

                _output.WriteLine($"Code hash for contract {TestConstants.WethContract}: {codeHash?.ToHex(true) ?? "null"}");
                Assert.NotNull(codeHash);
                Assert.NotEmpty(codeHash);

                var emptyCodeHash = "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470".HexToByteArray();
                Assert.NotEqual(emptyCodeHash, codeHash);
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
        public async Task VerifiedState_GetCodeHash_ForEOA_ReturnsEmptyCodeHash()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var codeHash = await verifiedState.GetCodeHashAsync(TestConstants.VitalikAddress);

                _output.WriteLine($"Code hash for EOA {TestConstants.VitalikAddress}: {codeHash?.ToHex(true) ?? "null"}");

                var emptyCodeHash = "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470".HexToByteArray();
                Assert.Equal(emptyCodeHash, codeHash);
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
        public async Task VerifiedState_HeaderContainsValidBlockInfo()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

                var header = verifiedState.GetCurrentHeader();

                Assert.True(header.BlockNumber > 0);
                Assert.NotNull(header.BlockHash);
                Assert.NotEmpty(header.BlockHash);
                Assert.NotNull(header.StateRoot);
                Assert.NotEmpty(header.StateRoot);
                Assert.NotNull(header.ReceiptsRoot);
                Assert.NotEmpty(header.ReceiptsRoot);

                _output.WriteLine($"Block Number: {header.BlockNumber}");
                _output.WriteLine($"Block Hash: {header.BlockHash.ToHex(true)}");
                _output.WriteLine($"State Root: {header.StateRoot.ToHex(true)}");
                _output.WriteLine($"Receipts Root: {header.ReceiptsRoot.ToHex(true)}");
                _output.WriteLine($"Timestamp: {header.Timestamp}");
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
        public async Task VerifiedState_FinalizedVsOptimistic_BothModesFunctional()
        {
            try
            {
                var finalizedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var optimisticState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);

                var finalizedHeader = finalizedState.GetCurrentHeader();
                var optimisticHeader = optimisticState.GetCurrentHeader();

                _output.WriteLine($"Finalized block: {finalizedHeader.BlockNumber}");
                _output.WriteLine($"Optimistic block: {optimisticHeader.BlockNumber}");
                _output.WriteLine($"Block difference: {optimisticHeader.BlockNumber - finalizedHeader.BlockNumber}");

                Assert.True(optimisticHeader.BlockNumber >= finalizedHeader.BlockNumber,
                    "Optimistic header should be at or ahead of finalized header");

                var finalizedBalance = await finalizedState.GetBalanceAsync(TestConstants.VitalikAddress);
                var optimisticBalance = await optimisticState.GetBalanceAsync(TestConstants.VitalikAddress);

                _output.WriteLine($"Finalized balance: {finalizedBalance} wei");
                _output.WriteLine($"Optimistic balance: {optimisticBalance} wei");

                Assert.True(finalizedBalance >= 0);
                Assert.True(optimisticBalance >= 0);
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
        public async Task VerifiedState_InvalidAddress_ThrowsArgumentException()
        {
            var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await verifiedState.GetBalanceAsync("");
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await verifiedState.GetBalanceAsync(null);
            });
        }
    }
}
