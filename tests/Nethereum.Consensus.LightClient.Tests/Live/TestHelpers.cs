using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.ChainStateVerification;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    public static class TestHelpers
    {
        public static async Task<LightClientService> CreateInitializedLightClientAsync()
        {
            EnsureNativeLibrary();

            var beaconClient = new BeaconApiClient(TestConstants.BeaconApiUrl);
            var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
            var finalityUpdate = LightClientResponseMapper.ToDomain(response);
            var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

            var config = CreateMainnetConfig(weakSubjectivityRoot);
            var nativeBls = new NativeBls(new HerumiNativeBindings());
            await nativeBls.InitializeAsync();

            var store = new InMemoryLightClientStore();
            var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
            await lightClient.InitializeAsync();

            return lightClient;
        }

        public static async Task<VerifiedStateService> CreateVerifiedStateServiceAsync(VerificationMode mode)
        {
            var lightClient = await CreateInitializedLightClientAsync();

            if (mode == VerificationMode.Optimistic)
            {
                await lightClient.UpdateOptimisticAsync();
            }

            var trustedProvider = new TrustedHeaderProvider(lightClient);
            var rpcClient = new RpcClient(new Uri(TestConstants.ExecutionRpcUrl));
            var ethGetProof = new EthGetProof(rpcClient);
            var ethGetCode = new EthGetCode(rpcClient);
            var trieVerifier = new TrieProofVerifier();

            return new VerifiedStateService(trustedProvider, ethGetProof, ethGetCode, trieVerifier) { Mode = mode };
        }

        public static LightClientConfig CreateMainnetConfig(byte[] weakSubjectivityRoot)
        {
            return new LightClientConfig
            {
                GenesisValidatorsRoot = TestConstants.MainnetGenesisValidatorsRoot,
                CurrentForkVersion = TestConstants.MainnetCurrentForkVersion,
                SlotsPerEpoch = 32,
                SecondsPerSlot = 12,
                WeakSubjectivityRoot = weakSubjectivityRoot
            };
        }

        public static void EnsureNativeLibrary()
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

        public static bool IsPrunedHistoryError(Exception ex) =>
            ex?.Message?.IndexOf("old data not available due to pruning", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("missing trie node", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("distance to target block exceeds maximum proof window", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsStateConsistencyError(Exception ex) =>
            ex?.Message?.IndexOf("Hash node inner node hash does not match current hash", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("Account proof did not match", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("Storage proof did not match", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsRateLimitError(Exception ex) =>
            ex?.Message?.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("Too Many Requests", StringComparison.OrdinalIgnoreCase) >= 0 ||
            ex?.Message?.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0;

        public static VerifiedStateService CreateVerifiedStateService(LightClientService lightClient)
        {
            var trustedProvider = new TrustedHeaderProvider(lightClient);
            var rpcClient = new RpcClient(new Uri(TestConstants.ExecutionRpcUrl));
            var ethGetProof = new EthGetProof(rpcClient);
            var ethGetCode = new EthGetCode(rpcClient);
            var trieVerifier = new TrieProofVerifier();

            return new VerifiedStateService(trustedProvider, ethGetProof, ethGetCode, trieVerifier);
        }
    }
}
