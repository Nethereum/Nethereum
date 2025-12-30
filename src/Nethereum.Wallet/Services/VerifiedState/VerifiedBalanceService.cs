using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
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
using Nethereum.Wallet.Services.Network;

namespace Nethereum.Wallet.Services.VerifiedState
{
    public class VerifiedBalanceService : IVerifiedBalanceService, IDisposable
    {
        private readonly IChainManagementService _chainManagementService;
        private readonly ConcurrentDictionary<BigInteger, LightClientInstance> _instances = new();
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _disposed;

        public event EventHandler<LightClientStatusChangedEventArgs> StatusChanged;

        public VerifiedBalanceService(IChainManagementService chainManagementService)
        {
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
        }

        public async Task<bool> IsAvailableAsync(BigInteger chainId)
        {
            var chainFeature = await _chainManagementService.GetChainAsync(chainId).ConfigureAwait(false);
            return chainFeature != null &&
                   chainFeature.SupportsLightClient &&
                   chainFeature.LightClientEnabled &&
                   !string.IsNullOrEmpty(chainFeature.BeaconChainApiUrl);
        }

        public async Task<VerifiedBalanceResult> GetBalanceAsync(string address, BigInteger chainId)
        {
            var chainFeature = await _chainManagementService.GetChainAsync(chainId).ConfigureAwait(false);

            if (chainFeature == null)
                return new VerifiedBalanceResult { IsVerified = false, Error = "Chain not found" };

            if (!chainFeature.SupportsLightClient || !chainFeature.LightClientEnabled)
                return new VerifiedBalanceResult { IsVerified = false, Error = "Light client not enabled" };

            if (string.IsNullOrEmpty(chainFeature.BeaconChainApiUrl))
                return new VerifiedBalanceResult { IsVerified = false, Error = "Beacon API URL not configured" };

            var (instance, initError) = await GetOrCreateInstanceAsync(chainId, chainFeature).ConfigureAwait(false);
            if (instance == null)
                return new VerifiedBalanceResult { IsVerified = false, Error = initError ?? "Light client initialization failed" };

            var result = new VerifiedBalanceResult();

            // Try finalized balance (strongest security - ~12 min behind head)
            await TryGetFinalizedBalanceAsync(instance, address, result).ConfigureAwait(false);

            // Try optimistic balance (weaker security - seconds behind head)
            await TryGetOptimisticBalanceAsync(instance, address, result).ConfigureAwait(false);

            // Set overall result based on what we got
            if (result.HasFinalizedBalance)
            {
                result.IsVerified = true;
                result.Balance = result.FinalizedBalance.Value;
                result.BlockNumber = result.FinalizedBlockNumber;
                result.Mode = VerifiedBalanceMode.Finalized;
            }
            else if (result.HasOptimisticBalance)
            {
                result.IsVerified = true;
                result.Balance = result.OptimisticBalance.Value;
                result.BlockNumber = result.OptimisticBlockNumber;
                result.Mode = VerifiedBalanceMode.Optimistic;
            }
            else
            {
                result.IsVerified = false;
                result.Mode = VerifiedBalanceMode.Unavailable;
                result.IsRpcLimitation = IsPrunedHistoryError(result.FinalizedError) && IsPrunedHistoryError(result.OptimisticError);
                result.Error = result.FinalizedError ?? result.OptimisticError ?? "Unable to verify balance";
            }

            return result;
        }

        private async Task TryGetFinalizedBalanceAsync(LightClientInstance instance, string address, VerifiedBalanceResult result)
        {
            try
            {
                await instance.LightClient.UpdateFinalityAsync().ConfigureAwait(false);
                instance.VerifiedStateService.Mode = ChainStateVerification.VerificationMode.Finalized;
                var header = instance.VerifiedStateService.GetCurrentHeader();

                var balance = await instance.VerifiedStateService.GetBalanceAsync(address).ConfigureAwait(false);

                result.FinalizedBalance = balance;
                result.FinalizedBlockNumber = header.BlockNumber;
                result.HasFinalizedBalance = true;
            }
            catch (Exception ex)
            {
                result.FinalizedError = IsPrunedHistoryError(ex) ? "RPC proof window exceeded" : ex.Message;
                result.HasFinalizedBalance = false;
            }
        }

        private async Task TryGetOptimisticBalanceAsync(LightClientInstance instance, string address, VerifiedBalanceResult result)
        {
            try
            {
                var updated = await instance.LightClient.UpdateOptimisticAsync().ConfigureAwait(false);
                if (!updated)
                {
                    result.OptimisticError = "Failed to get optimistic update";
                    result.HasOptimisticBalance = false;
                    return;
                }

                instance.VerifiedStateService.ClearCache();
                instance.VerifiedStateService.Mode = ChainStateVerification.VerificationMode.Optimistic;
                var header = instance.VerifiedStateService.GetCurrentHeader();

                var balance = await instance.VerifiedStateService.GetBalanceAsync(address).ConfigureAwait(false);

                result.OptimisticBalance = balance;
                result.OptimisticBlockNumber = header.BlockNumber;
                result.HasOptimisticBalance = true;
            }
            catch (Exception ex)
            {
                result.OptimisticError = IsPrunedHistoryError(ex) ? "RPC proof window exceeded" : ex.Message;
                result.HasOptimisticBalance = false;
            }
        }

        private static bool IsPrunedHistoryError(Exception ex) =>
            ex?.Message != null && IsPrunedHistoryError(ex.Message);

        private static bool IsPrunedHistoryError(string? errorMessage) =>
            errorMessage != null && (
                errorMessage.IndexOf("old data not available due to pruning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorMessage.IndexOf("missing trie node", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorMessage.IndexOf("distance to target block exceeds maximum proof window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                errorMessage.IndexOf("RPC proof window exceeded", StringComparison.OrdinalIgnoreCase) >= 0);

        public async Task<LightClientStatus> GetStatusAsync(BigInteger chainId)
        {
            if (!_instances.TryGetValue(chainId, out var instance))
            {
                return new LightClientStatus
                {
                    IsInitialized = false,
                    IsSyncing = false,
                    Error = "Light client not initialized"
                };
            }

            try
            {
                var header = instance.VerifiedStateService.GetCurrentHeader();
                return new LightClientStatus
                {
                    IsInitialized = true,
                    IsSyncing = false,
                    FinalizedSlot = header.BlockNumber
                };
            }
            catch (Exception ex)
            {
                return new LightClientStatus
                {
                    IsInitialized = true,
                    IsSyncing = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<(LightClientInstance? Instance, string? Error)> GetOrCreateInstanceAsync(BigInteger chainId, Nethereum.RPC.Chain.ChainFeature chainFeature)
        {
            if (_instances.TryGetValue(chainId, out var existing))
                return (existing, null);

            await _initLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_instances.TryGetValue(chainId, out existing))
                    return (existing, null);

                var (instance, error) = await CreateInstanceAsync(chainId, chainFeature).ConfigureAwait(false);
                if (instance != null)
                {
                    _instances[chainId] = instance;
                    OnStatusChanged(chainId, new LightClientStatus { IsInitialized = true });
                }
                return (instance, error);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<(LightClientInstance? Instance, string? Error)> CreateInstanceAsync(BigInteger chainId, Nethereum.RPC.Chain.ChainFeature chainFeature)
        {
            try
            {
                if (string.IsNullOrEmpty(chainFeature.BeaconChainApiUrl))
                    return (null, "Beacon Chain API URL not configured");

                var beaconClient = new BeaconApiClient(chainFeature.BeaconChainApiUrl);

                // Fetch current fork version from beacon chain
                var forkResponse = await beaconClient.GetStateForkAsync().ConfigureAwait(false);
                var currentForkVersion = forkResponse?.Data?.CurrentVersion?.HexToByteArray() ?? new byte[] { 0x06, 0x00, 0x00, 0x00 };
                System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] Current fork version from beacon: {forkResponse?.Data?.CurrentVersion}");

                var response = await beaconClient.LightClient.GetFinalityUpdateAsync().ConfigureAwait(false);
                var finalityUpdate = LightClientResponseMapper.ToDomain(response);
                var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

                var config = CreateLightClientConfig(chainId, weakSubjectivityRoot, currentForkVersion);

                NativeBls nativeBls;
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] Creating NativeBls...");
                    nativeBls = new NativeBls(new HerumiNativeBindings());
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] Initializing NativeBls...");
                    await nativeBls.InitializeAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] NativeBls initialized successfully");
                }
                catch (TypeInitializationException ex)
                {
                    var innerMsg = ex.InnerException?.Message ?? ex.Message;
                    var fullError = $"BLS initialization failed: {innerMsg}";
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] {fullError}");
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] Inner exception type: {ex.InnerException?.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[VerifiedBalance] Stack: {ex.InnerException?.StackTrace ?? ex.StackTrace}");
                    return (null, fullError);
                }

                var store = new InMemoryLightClientStore();
                var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
                await lightClient.InitializeAsync().ConfigureAwait(false);
                // Don't call UpdateFinalityAsync here - let GetBalanceAsync decide which mode to use

                var trustedProvider = new TrustedHeaderProvider(lightClient);
                var executionRpcUrl = !string.IsNullOrEmpty(chainFeature.ExecutionRpcUrlForProofs)
                    ? chainFeature.ExecutionRpcUrlForProofs
                    : chainFeature.HttpRpcs?.Count > 0 ? chainFeature.HttpRpcs[0] : null;

                if (string.IsNullOrEmpty(executionRpcUrl))
                    return (null, "No execution RPC URL available for proofs");

                var rpcClient = new RpcClient(new Uri(executionRpcUrl));
                var ethGetProof = new EthGetProof(rpcClient);
                var ethGetCode = new EthGetCode(rpcClient);
                var trieVerifier = new TrieProofVerifier();

                var verifiedStateService = new ChainStateVerification.VerifiedStateService(
                    trustedProvider, ethGetProof, ethGetCode, trieVerifier)
                {
                    Mode = ChainStateVerification.VerificationMode.Finalized
                };

                return (new LightClientInstance
                {
                    ChainId = chainId,
                    LightClient = lightClient,
                    VerifiedStateService = verifiedStateService,
                    NativeBls = nativeBls
                }, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private static LightClientConfig CreateLightClientConfig(BigInteger chainId, byte[] weakSubjectivityRoot, byte[] currentForkVersion)
        {
            if (chainId == 1)
            {
                return new LightClientConfig
                {
                    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
                    CurrentForkVersion = currentForkVersion,
                    SlotsPerEpoch = 32,
                    SecondsPerSlot = 12,
                    WeakSubjectivityRoot = weakSubjectivityRoot
                };
            }
            else if (chainId == 11155111)
            {
                return new LightClientConfig
                {
                    GenesisValidatorsRoot = "0xd8ea171f3c94aea21ebc42a1ed61052acf3f9209c00e4efbaaddac09ed9b8078".HexToByteArray(),
                    CurrentForkVersion = currentForkVersion,
                    SlotsPerEpoch = 32,
                    SecondsPerSlot = 12,
                    WeakSubjectivityRoot = weakSubjectivityRoot
                };
            }
            else if (chainId == 17000)
            {
                return new LightClientConfig
                {
                    GenesisValidatorsRoot = "0x9143aa7c615a7f7115e2b6aac319c03529df8242ae705fba9df39b79c59fa8b1".HexToByteArray(),
                    CurrentForkVersion = currentForkVersion,
                    SlotsPerEpoch = 32,
                    SecondsPerSlot = 12,
                    WeakSubjectivityRoot = weakSubjectivityRoot
                };
            }

            return new LightClientConfig
            {
                CurrentForkVersion = currentForkVersion,
                SlotsPerEpoch = 32,
                SecondsPerSlot = 12,
                WeakSubjectivityRoot = weakSubjectivityRoot
            };
        }

        private void OnStatusChanged(BigInteger chainId, LightClientStatus status)
        {
            StatusChanged?.Invoke(this, new LightClientStatusChangedEventArgs(chainId, status));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                foreach (var instance in _instances.Values)
                {
                    instance.VerifiedStateService?.Dispose();
                }
                _instances.Clear();
                _initLock.Dispose();
            }

            _disposed = true;
        }

        private class LightClientInstance
        {
            public BigInteger ChainId { get; set; }
            public LightClientService LightClient { get; set; }
            public ChainStateVerification.VerifiedStateService VerifiedStateService { get; set; }
            public NativeBls NativeBls { get; set; }
        }
    }

    internal static class HexExtensions
    {
        public static byte[] HexToByteArray(this string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);

            if (hex.Length % 2 != 0)
                hex = "0" + hex;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
