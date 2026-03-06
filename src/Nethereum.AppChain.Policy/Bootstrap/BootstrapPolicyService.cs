using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Policy.Bootstrap
{
    public class BootstrapPolicyService : IPolicyService
    {
        private readonly BootstrapPolicyConfig _config;
        private readonly IPolicyService? _l1PolicyService;
        private bool _migratedToL1;

        public BootstrapPolicyService(BootstrapPolicyConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (_config.AutoMigrateWhenL1Available &&
                !string.IsNullOrEmpty(_config.L1PolicyContractAddress) &&
                !string.IsNullOrEmpty(_config.L1RpcUrl))
            {
                var policyConfig = new PolicyConfig
                {
                    PolicyContractAddress = _config.L1PolicyContractAddress,
                    TargetRpcUrl = _config.L1RpcUrl,
                    TargetChainId = _config.L1ChainId
                };
                _l1PolicyService = new EvmPolicyService(policyConfig);
            }
        }

        public BootstrapPolicyService(BootstrapPolicyConfig config, IPolicyService? l1PolicyService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _l1PolicyService = l1PolicyService;
        }

        public bool IsMigratedToL1 => _migratedToL1;

        public async Task<PolicyInfo> GetCurrentPolicyAsync()
        {
            if (_l1PolicyService != null && _config.AutoMigrateWhenL1Available)
            {
                try
                {
                    var l1Policy = await _l1PolicyService.GetCurrentPolicyAsync();
                    if (l1Policy.Version > 0)
                    {
                        _migratedToL1 = true;
                        return l1Policy;
                    }
                }
                catch
                {
                }
            }

            return new PolicyInfo
            {
                Version = 0,
                MaxCalldataBytes = _config.MaxCalldataBytes,
                MaxLogBytes = _config.MaxLogBytes,
                BlockGasLimit = _config.BlockGasLimit,
                Sequencer = _config.SequencerAddress ?? string.Empty,
                Epoch = 0
            };
        }

        public Task<byte[]?> GetWritersRootAsync()
        {
            return Task.FromResult<byte[]?>(null);
        }

        public Task<byte[]?> GetAdminsRootAsync()
        {
            return Task.FromResult<byte[]?>(null);
        }

        public Task<byte[]?> GetBlacklistRootAsync()
        {
            return Task.FromResult<byte[]?>(null);
        }

        public Task<BigInteger> GetEpochAsync()
        {
            return Task.FromResult(BigInteger.Zero);
        }

        public async Task<bool> IsValidWriterAsync(string address, byte[][] writerProof, byte[]? blacklistProof = null)
        {
            if (_l1PolicyService != null && _migratedToL1)
            {
                return await _l1PolicyService.IsValidWriterAsync(address, writerProof, blacklistProof);
            }

            if (_config.OpenWriterAccess)
                return true;

            if (_config.AllowedWriters == null || _config.AllowedWriters.Count == 0)
                return true;

            var normalizedAddress = NormalizeAddress(address);
            foreach (var writer in _config.AllowedWriters)
            {
                if (NormalizeAddress(writer) == normalizedAddress)
                    return true;
            }

            return false;
        }

        public bool IsValidAdmin(string address)
        {
            if (_config.OpenAdminAccess)
                return true;

            if (_config.AllowedAdmins == null || _config.AllowedAdmins.Count == 0)
                return true;

            var normalizedAddress = NormalizeAddress(address);
            foreach (var admin in _config.AllowedAdmins)
            {
                if (NormalizeAddress(admin) == normalizedAddress)
                    return true;
            }

            return false;
        }

        public void AddWriter(string address)
        {
            var normalized = NormalizeAddress(address);
            if (!_config.AllowedWriters.Contains(normalized))
            {
                _config.AllowedWriters.Add(normalized);
            }
        }

        public void RemoveWriter(string address)
        {
            var normalized = NormalizeAddress(address);
            _config.AllowedWriters.Remove(normalized);
        }

        public void AddAdmin(string address)
        {
            var normalized = NormalizeAddress(address);
            if (!_config.AllowedAdmins.Contains(normalized))
            {
                _config.AllowedAdmins.Add(normalized);
            }
        }

        public void RemoveAdmin(string address)
        {
            var normalized = NormalizeAddress(address);
            _config.AllowedAdmins.Remove(normalized);
        }

        private static string NormalizeAddress(string address)
        {
            return address?.ToLowerInvariant() ?? "";
        }
    }
}
