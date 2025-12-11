using System;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.ChainStateVerification;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class LightClientLiveIntegrationTests
    {
        private const string BeaconApiUrl = "https://ethereum-beacon-api.publicnode.com";
        private const string ExecutionRpcUrl = "https://mainnet.infura.io/v3/2IgHC042dCtS6DwcOWefagLEcIe";
        private const string TestAccountAddress = "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B";

        private readonly ITestOutputHelper _output;

        public LightClientLiveIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GetFinalityUpdate_ReturnsValidData()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(finalityUpdate);
            Assert.NotNull(finalityUpdate.FinalizedHeader);
            Assert.NotNull(finalityUpdate.FinalizedHeader.Beacon);
            Assert.NotNull(finalityUpdate.FinalizedHeader.Execution);
            Assert.True(finalityUpdate.FinalizedHeader.Beacon.Slot > 0);
            Assert.True(finalityUpdate.FinalizedHeader.Execution.BlockNumber > 0);

            _output.WriteLine($"Finalized slot: {finalityUpdate.FinalizedHeader.Beacon.Slot}");
            _output.WriteLine($"Finalized block number: {finalityUpdate.FinalizedHeader.Execution.BlockNumber}");
            _output.WriteLine($"Finalized block hash: {finalityUpdate.FinalizedHeader.Execution.BlockHash.ToHex(true)}");
        }

        [Fact]
        public async Task GetOptimisticUpdate_ReturnsValidData()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetOptimisticUpdateAsync();
            var optimisticUpdate = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(optimisticUpdate);
            Assert.NotNull(optimisticUpdate.AttestedHeader);
            Assert.NotNull(optimisticUpdate.AttestedHeader.Beacon);
            Assert.NotNull(optimisticUpdate.AttestedHeader.Execution);
            Assert.True(optimisticUpdate.AttestedHeader.Beacon.Slot > 0);
            Assert.True(optimisticUpdate.AttestedHeader.Execution.BlockNumber > 0);

            _output.WriteLine($"Optimistic slot: {optimisticUpdate.AttestedHeader.Beacon.Slot}");
            _output.WriteLine($"Optimistic block number: {optimisticUpdate.AttestedHeader.Execution.BlockNumber}");
            _output.WriteLine($"Optimistic block hash: {optimisticUpdate.AttestedHeader.Execution.BlockHash.ToHex(true)}");
        }

        [Fact]
        public async Task LightClient_InitializeAndUpdate_FromLiveFinalityUpdate()
        {
            EnsureNativeLibrary();

            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(response);
            var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

            _output.WriteLine($"Using weak subjectivity root: {weakSubjectivityRoot.ToHex(true)}");

            var config = CreateMainnetConfig(weakSubjectivityRoot);

            var nativeBls = new NativeBls(new HerumiNativeBindings());
            await nativeBls.InitializeAsync();

            var store = new InMemoryLightClientStore();
            var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);

            await lightClient.InitializeAsync();

            var state = lightClient.GetState();
            Assert.NotNull(state);
            Assert.NotNull(state.FinalizedExecutionPayload);
            Assert.True(state.FinalizedSlot > 0);

            _output.WriteLine($"Light client initialized at slot: {state.FinalizedSlot}");
            _output.WriteLine($"Block number: {state.FinalizedExecutionPayload.BlockNumber}");
            _output.WriteLine($"Block hash: {state.FinalizedExecutionPayload.BlockHash.ToHex(true)}");

            var updated = await lightClient.UpdateAsync();
            _output.WriteLine($"Update applied: {updated}");

            var updatedState = lightClient.GetState();
            _output.WriteLine($"Current slot after update: {updatedState.FinalizedSlot}");
        }

        [Fact]
        public async Task VerifiedStateService_GetAccountBalance_FromFinalizedState()
        {
            EnsureNativeLibrary();

            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(response);
            var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

            var config = CreateMainnetConfig(weakSubjectivityRoot);

            var nativeBls = new NativeBls(new HerumiNativeBindings());
            await nativeBls.InitializeAsync();

            var store = new InMemoryLightClientStore();
            var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);

            await lightClient.InitializeAsync();

            var trustedProvider = new TrustedHeaderProvider(lightClient);
            var rpcClient = new RpcClient(new Uri(ExecutionRpcUrl));
            var ethGetProof = new EthGetProof(rpcClient);
            var ethGetCode = new EthGetCode(rpcClient);
            var trieVerifier = new TrieProofVerifier();

            var verifiedState = new VerifiedStateService(trustedProvider, ethGetProof, ethGetCode, trieVerifier);
            verifiedState.Mode = VerificationMode.Finalized;

            try
            {
                var balance = await verifiedState.GetBalanceAsync(TestAccountAddress);

                Assert.True(balance >= 0);

                var balanceInEth = (decimal)balance / 1_000_000_000_000_000_000m;
                _output.WriteLine($"Account: {TestAccountAddress}");
                _output.WriteLine($"Balance: {balance} wei ({balanceInEth:F4} ETH)");

                var nonce = await verifiedState.GetNonceAsync(TestAccountAddress);
                _output.WriteLine($"Nonce: {nonce}");
            }
            catch (RpcResponseException ex) when (IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
                _output.WriteLine("To run this test, use an archive node endpoint.");
            }
        }

        [Fact]
        public async Task VerifiedStateService_GetAccountBalance_FromOptimisticState()
        {
            EnsureNativeLibrary();

            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(response);
            var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

            var config = CreateMainnetConfig(weakSubjectivityRoot);

            var nativeBls = new NativeBls(new HerumiNativeBindings());
            await nativeBls.InitializeAsync();

            var store = new InMemoryLightClientStore();
            var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);

            await lightClient.InitializeAsync();
            await lightClient.UpdateOptimisticAsync();

            var trustedProvider = new TrustedHeaderProvider(lightClient);
            var rpcClient = new RpcClient(new Uri(ExecutionRpcUrl));
            var ethGetProof = new EthGetProof(rpcClient);
            var ethGetCode = new EthGetCode(rpcClient);
            var trieVerifier = new TrieProofVerifier();

            var verifiedState = new VerifiedStateService(trustedProvider, ethGetProof, ethGetCode, trieVerifier);
            verifiedState.Mode = VerificationMode.Optimistic;

            try
            {
                var balance = await verifiedState.GetBalanceAsync(TestAccountAddress);

                Assert.True(balance >= 0);

                var balanceInEth = (decimal)balance / 1_000_000_000_000_000_000m;
                _output.WriteLine($"Account: {TestAccountAddress}");
                _output.WriteLine($"Balance (Optimistic): {balance} wei ({balanceInEth:F4} ETH)");

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Optimistic block: {header.BlockNumber}");
            }
            catch (RpcResponseException ex) when (IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task OptimisticUpdate_HasMoreRecentSlotThanFinalized()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var finalityResponse = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var optimisticResponse = await beaconClient.LightClient.GetOptimisticUpdateAsync();

            var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
            var optimisticUpdate = LightClientResponseMapper.ToDomain(optimisticResponse);

            Assert.NotNull(finalityUpdate);
            Assert.NotNull(optimisticUpdate);

            var finalizedSlot = finalityUpdate.FinalizedHeader.Beacon.Slot;
            var optimisticSlot = optimisticUpdate.AttestedHeader.Beacon.Slot;

            _output.WriteLine($"Finalized slot: {finalizedSlot}");
            _output.WriteLine($"Optimistic slot: {optimisticSlot}");
            _output.WriteLine($"Slot difference: {optimisticSlot - finalizedSlot}");

            Assert.True(optimisticSlot >= finalizedSlot,
                "Optimistic update should be at or ahead of finalized update");
        }

        [Fact]
        public async Task GetBootstrap_FromFinalizedRoot_ReturnsValidData()
        {
            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var finalityResponse = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
            var blockRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

            _output.WriteLine($"Fetching bootstrap for root: {blockRoot.ToHex(true)}");

            var response = await beaconClient.LightClient.GetBootstrapAsync(blockRoot.ToHex(true));
            var bootstrap = LightClientResponseMapper.ToDomain(response);

            Assert.NotNull(bootstrap);
            Assert.NotNull(bootstrap.Header);
            Assert.NotNull(bootstrap.Header.Beacon);
            Assert.NotNull(bootstrap.CurrentSyncCommittee);
            Assert.NotNull(bootstrap.CurrentSyncCommitteeBranch);
            Assert.True(bootstrap.CurrentSyncCommitteeBranch.Count > 0);

            _output.WriteLine($"Bootstrap slot: {bootstrap.Header.Beacon.Slot}");
            _output.WriteLine($"Sync committee pubkeys count: {bootstrap.CurrentSyncCommittee.PubKeys?.Count ?? 0}");
        }

        [Fact]
        public async Task GetUpdates_ReturnsValidUpdateList()
        {
            EnsureNativeLibrary();

            var beaconClient = new BeaconApiClient(BeaconApiUrl);

            var finalityResponse = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
            var currentSlot = finalityUpdate.FinalizedHeader.Beacon.Slot;
            var currentPeriod = currentSlot / (32 * 256);

            _output.WriteLine($"Current period: {currentPeriod}");

            var responses = await beaconClient.LightClient.GetUpdatesAsync(currentPeriod, 1);
            var updates = LightClientResponseMapper.ToDomain(responses);

            Assert.NotNull(updates);

            foreach (var update in updates)
            {
                _output.WriteLine($"Update - Attested slot: {update.AttestedHeader?.Beacon?.Slot}, Finalized slot: {update.FinalizedHeader?.Beacon?.Slot}");
            }
        }

        private static LightClientConfig CreateMainnetConfig(byte[] weakSubjectivityRoot)
        {
            return new LightClientConfig
            {
                GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
                CurrentForkVersion = "0x06000000".HexToByteArray(),
                SlotsPerEpoch = 32,
                SecondsPerSlot = 12,
                WeakSubjectivityRoot = weakSubjectivityRoot
            };
        }

        private static void EnsureNativeLibrary()
        {
            var libraryName = GetLibraryName();
            var baseDirectory = AppContext.BaseDirectory;
            var target = Path.Combine(baseDirectory, libraryName);

            if (File.Exists(target))
            {
                return;
            }

            var runtimeCandidate = Path.Combine(baseDirectory, "runtimes", GetRuntimeIdentifier(), "native", libraryName);
            if (File.Exists(runtimeCandidate))
            {
                File.Copy(runtimeCandidate, target, overwrite: true);
                return;
            }

            var repoRoot = TryLocateRepositoryRoot();
            if (repoRoot != null)
            {
                var repoCandidate = Path.Combine(repoRoot, "src", "Nethereum.Signer.Bls.Herumi", "runtimes", GetRuntimeIdentifier(), "native", libraryName);
                if (File.Exists(repoCandidate))
                {
                    File.Copy(repoCandidate, target, overwrite: true);
                    return;
                }
            }

            throw new InvalidOperationException($"Native library '{libraryName}' not found. Run scripts/build-herumi-bls.(ps1|sh).");
        }

        private static string GetLibraryName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "bls_eth.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "libbls_eth.so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "libbls_eth.dylib";
            throw new PlatformNotSupportedException("Unsupported platform for Herumi BLS.");
        }

        private static string GetRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                return "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                return "osx-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return "osx-arm64";
            throw new PlatformNotSupportedException("Unsupported platform/architecture combination.");
        }

        private static string TryLocateRepositoryRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Nethereum.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }

        private static bool IsPrunedHistoryError(RpcResponseException ex) =>
            ex?.Message?.IndexOf("old data not available due to pruning", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("missing trie node", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("distance to target block exceeds maximum proof window", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
