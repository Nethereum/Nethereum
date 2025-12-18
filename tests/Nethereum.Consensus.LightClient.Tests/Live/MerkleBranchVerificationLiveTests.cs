using System.Threading.Tasks;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class MerkleBranchVerificationLiveTests
    {
        private const string BeaconApiUrl = "https://ethereum-beacon-api.publicnode.com";

        private readonly ITestOutputHelper _output;

        public MerkleBranchVerificationLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task FinalityUpdate_ExecutionBranch_VerifiesAgainstBodyRoot()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var update = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(update?.FinalizedHeader?.Execution);
            Assert.NotNull(update.FinalizedHeader.ExecutionBranch);
            Assert.True(update.FinalizedHeader.ExecutionBranch.Count >= SszBasicTypes.ExecutionBranchDepth,
                $"Expected at least {SszBasicTypes.ExecutionBranchDepth} branch nodes, got {update.FinalizedHeader.ExecutionBranch.Count}");

            var executionRoot = update.FinalizedHeader.Execution.HashTreeRoot();
            _output.WriteLine($"Execution payload hash tree root: {executionRoot.ToHex(true)}");
            _output.WriteLine($"Beacon body root: {update.FinalizedHeader.Beacon.BodyRoot.ToHex(true)}");
            _output.WriteLine($"Execution branch depth: {SszBasicTypes.ExecutionBranchDepth}, index: {SszBasicTypes.ExecutionBranchIndex}");

            var verified = SszMerkleizer.VerifyProof(
                executionRoot,
                update.FinalizedHeader.ExecutionBranch,
                SszBasicTypes.ExecutionBranchDepth,
                SszBasicTypes.ExecutionBranchIndex,
                update.FinalizedHeader.Beacon.BodyRoot);

            Assert.True(verified, "Execution branch Merkle proof verification failed for finalized header");
            _output.WriteLine("✓ Finalized header execution branch verified successfully");
        }

        [Fact]
        public async Task FinalityUpdate_FinalityBranch_VerifiesAgainstStateRoot()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var update = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(update?.FinalizedHeader?.Beacon);
            Assert.NotNull(update?.AttestedHeader?.Beacon);
            Assert.NotNull(update.FinalityBranch);

            _output.WriteLine($"Finality branch actual length: {update.FinalityBranch.Count}");

            for (int i = 0; i < update.FinalityBranch.Count; i++)
            {
                _output.WriteLine($"  Branch[{i}]: {update.FinalityBranch[i].ToHex(true)}");
            }

            var finalizedRoot = update.FinalizedHeader.Beacon.HashTreeRoot();
            _output.WriteLine($"Finalized beacon header hash tree root (leaf): {finalizedRoot.ToHex(true)}");
            _output.WriteLine($"Attested beacon state root (expected root): {update.AttestedHeader.Beacon.StateRoot.ToHex(true)}");

            Assert.True(update.FinalityBranch.Count >= SszBasicTypes.FinalityBranchDepth,
                $"Expected at least {SszBasicTypes.FinalityBranchDepth} branch nodes, got {update.FinalityBranch.Count}");

            _output.WriteLine($"Finality branch depth: {SszBasicTypes.FinalityBranchDepth}, index: {SszBasicTypes.FinalityBranchIndex}");

            var verified = SszMerkleizer.VerifyProof(
                finalizedRoot,
                update.FinalityBranch,
                SszBasicTypes.FinalityBranchDepth,
                SszBasicTypes.FinalityBranchIndex,
                update.AttestedHeader.Beacon.StateRoot);

            Assert.True(verified, "Finality branch Merkle proof verification failed");
            _output.WriteLine("✓ Finality branch verified successfully");
        }

        [Fact]
        public async Task OptimisticUpdate_ExecutionBranch_VerifiesAgainstBodyRoot()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetOptimisticUpdateAsync();
            var update = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(update?.AttestedHeader?.Execution);
            Assert.NotNull(update.AttestedHeader.ExecutionBranch);
            Assert.True(update.AttestedHeader.ExecutionBranch.Count >= SszBasicTypes.ExecutionBranchDepth,
                $"Expected at least {SszBasicTypes.ExecutionBranchDepth} branch nodes, got {update.AttestedHeader.ExecutionBranch.Count}");

            var executionRoot = update.AttestedHeader.Execution.HashTreeRoot();
            _output.WriteLine($"Execution payload hash tree root: {executionRoot.ToHex(true)}");
            _output.WriteLine($"Beacon body root: {update.AttestedHeader.Beacon.BodyRoot.ToHex(true)}");

            var verified = SszMerkleizer.VerifyProof(
                executionRoot,
                update.AttestedHeader.ExecutionBranch,
                SszBasicTypes.ExecutionBranchDepth,
                SszBasicTypes.ExecutionBranchIndex,
                update.AttestedHeader.Beacon.BodyRoot);

            Assert.True(verified, "Execution branch Merkle proof verification failed for optimistic header");
            _output.WriteLine("✓ Optimistic header execution branch verified successfully");
        }

        [Fact]
        public async Task TamperedExecutionPayload_FailsVerification()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var update = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(update?.FinalizedHeader?.Execution);

            var originalBlockNumber = update.FinalizedHeader.Execution.BlockNumber;
            update.FinalizedHeader.Execution.BlockNumber = originalBlockNumber + 1;

            var tamperedExecutionRoot = update.FinalizedHeader.Execution.HashTreeRoot();
            _output.WriteLine($"Original block number: {originalBlockNumber}");
            _output.WriteLine($"Tampered block number: {update.FinalizedHeader.Execution.BlockNumber}");
            _output.WriteLine($"Tampered execution root: {tamperedExecutionRoot.ToHex(true)}");

            var verified = SszMerkleizer.VerifyProof(
                tamperedExecutionRoot,
                update.FinalizedHeader.ExecutionBranch,
                SszBasicTypes.ExecutionBranchDepth,
                SszBasicTypes.ExecutionBranchIndex,
                update.FinalizedHeader.Beacon.BodyRoot);

            Assert.False(verified, "Tampered execution payload should NOT verify");
            _output.WriteLine("✓ Tampered execution payload correctly rejected");
        }

        [Fact]
        public async Task TamperedFinalizedHeader_FailsVerification()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var update = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(update?.FinalizedHeader?.Beacon);

            var originalSlot = update.FinalizedHeader.Beacon.Slot;
            update.FinalizedHeader.Beacon.Slot = originalSlot + 1;

            var tamperedFinalizedRoot = update.FinalizedHeader.Beacon.HashTreeRoot();
            _output.WriteLine($"Original slot: {originalSlot}");
            _output.WriteLine($"Tampered slot: {update.FinalizedHeader.Beacon.Slot}");
            _output.WriteLine($"Tampered finalized root: {tamperedFinalizedRoot.ToHex(true)}");

            var verified = SszMerkleizer.VerifyProof(
                tamperedFinalizedRoot,
                update.FinalityBranch,
                SszBasicTypes.FinalityBranchDepth,
                SszBasicTypes.FinalityBranchIndex,
                update.AttestedHeader.Beacon.StateRoot);

            Assert.False(verified, "Tampered finalized header should NOT verify");
            _output.WriteLine("✓ Tampered finalized header correctly rejected");
        }

        [Fact]
        public async Task FullLightClientUpdate_AllBranchesVerify()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();

            var updated = await lightClient.UpdateFinalityAsync();

            _output.WriteLine($"Finality update applied: {updated}");

            if (updated)
            {
                var state = lightClient.GetState();
                _output.WriteLine($"Finalized slot: {state.FinalizedSlot}");
                _output.WriteLine($"Finalized block: {state.FinalizedExecutionPayload?.BlockNumber}");
                _output.WriteLine($"Finalized block hash: {state.FinalizedExecutionPayload?.BlockHash?.ToHex(true)}");
                _output.WriteLine("✓ Full light client update succeeded with all Merkle branch verifications");
            }
            else
            {
                _output.WriteLine("No new finality update available (state already up to date)");
            }

            Assert.NotNull(lightClient.GetState().FinalizedExecutionPayload);
        }

        [Fact]
        public async Task FullLightClientOptimisticUpdate_ExecutionBranchVerifies()
        {
            TestHelpers.EnsureNativeLibrary();

            var lightClient = await TestHelpers.CreateInitializedLightClientAsync();

            var updated = await lightClient.UpdateOptimisticAsync();

            _output.WriteLine($"Optimistic update applied: {updated}");

            if (updated)
            {
                var state = lightClient.GetState();
                _output.WriteLine($"Optimistic slot: {state.OptimisticSlot}");
                _output.WriteLine($"Optimistic block: {state.OptimisticExecutionPayload?.BlockNumber}");
                _output.WriteLine($"Optimistic block hash: {state.OptimisticExecutionPayload?.BlockHash?.ToHex(true)}");
                _output.WriteLine("✓ Full light client optimistic update succeeded with execution branch verification");
            }

            Assert.True(updated, "Optimistic update should succeed");
        }
    }
}
