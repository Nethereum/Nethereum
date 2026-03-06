using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Policy
{
    public class PolicySyncWorker : IHostedService, IDisposable
    {
        private readonly IPolicyService _policyService;
        private readonly PolicyConfig _localConfig;
        private readonly ILogger<PolicySyncWorker>? _logger;

        private Timer? _timer;
        private bool _isRunning;
        private PolicyInfo? _cachedPolicy;
        private BigInteger _lastEpoch;

        public event Action<PolicyInfo>? OnPolicyUpdated;

        public PolicySyncWorker(
            IPolicyService policyService,
            PolicyConfig localConfig,
            ILogger<PolicySyncWorker>? logger = null)
        {
            _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
            _localConfig = localConfig ?? throw new ArgumentNullException(nameof(localConfig));
            _logger = logger;
        }

        public PolicyInfo? CachedPolicy => _cachedPolicy;
        public bool IsRunning => _isRunning;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_localConfig.Enabled)
            {
                _logger?.LogInformation("Policy sync worker is disabled");
                return;
            }

            _logger?.LogInformation("Policy sync worker starting");

            await SyncPolicyAsync();

            _isRunning = true;
            _timer = new Timer(
                async _ => await SyncPolicyAsync(),
                null,
                TimeSpan.FromMilliseconds(_localConfig.SyncIntervalMs),
                TimeSpan.FromMilliseconds(_localConfig.SyncIntervalMs));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Policy sync worker stopping");
            _isRunning = false;
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async Task SyncPolicyAsync()
        {
            try
            {
                var policy = await _policyService.GetCurrentPolicyAsync();
                var newEpoch = await _policyService.GetEpochAsync();

                if (newEpoch != _lastEpoch || _cachedPolicy == null)
                {
                    _logger?.LogInformation("Policy updated: epoch {OldEpoch} -> {NewEpoch}",
                        _lastEpoch, newEpoch);

                    _cachedPolicy = policy;
                    _lastEpoch = newEpoch;

                    UpdateLocalConfig(policy);
                    OnPolicyUpdated?.Invoke(policy);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error syncing policy");
            }
        }

        private void UpdateLocalConfig(PolicyInfo policy)
        {
            _localConfig.MaxCalldataBytes = policy.MaxCalldataBytes;
            _localConfig.MaxLogBytes = policy.MaxLogBytes;
            _localConfig.BlockGasLimit = policy.BlockGasLimit;
            _localConfig.WritersRoot = policy.WritersRoot;
            _localConfig.AdminsRoot = policy.AdminsRoot;
            _localConfig.BlacklistRoot = policy.BlacklistRoot;
            _localConfig.Epoch = policy.Epoch;
        }

        public async Task ForceSyncAsync()
        {
            await SyncPolicyAsync();
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
