using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy;

namespace Nethereum.AppChain.Policy
{
    public class EvmPolicyService : IPolicyService
    {
        private readonly PolicyConfig _config;
        private readonly Web3.Web3? _web3;
        private readonly ILogger<EvmPolicyService>? _logger;
        private readonly AppChainPolicyService? _policyService;

        public EvmPolicyService(PolicyConfig config, ILogger<EvmPolicyService>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;

            if (!string.IsNullOrEmpty(_config.TargetRpcUrl))
            {
                _web3 = new Web3.Web3(_config.TargetRpcUrl);
                if (!string.IsNullOrEmpty(_config.PolicyContractAddress))
                {
                    _policyService = new AppChainPolicyService(_web3, _config.PolicyContractAddress);
                }
            }
        }

        public async Task<PolicyInfo> GetCurrentPolicyAsync()
        {
            var policyInfo = new PolicyInfo
            {
                MaxCalldataBytes = _config.MaxCalldataBytes,
                MaxLogBytes = _config.MaxLogBytes,
                BlockGasLimit = _config.BlockGasLimit
            };

            if (_policyService == null)
            {
                policyInfo.WritersRoot = _config.WritersRoot;
                policyInfo.AdminsRoot = _config.AdminsRoot;
                policyInfo.BlacklistRoot = _config.BlacklistRoot;
                policyInfo.Epoch = _config.Epoch;
                return policyInfo;
            }

            try
            {
                var result = await _policyService.CurrentPolicyQueryAsync();
                policyInfo.Version = result.Version;
                policyInfo.MaxCalldataBytes = result.MaxCalldataBytes;
                policyInfo.MaxLogBytes = result.MaxLogBytes;
                policyInfo.BlockGasLimit = result.BlockGasLimit;
                policyInfo.Sequencer = result.Sequencer;

                policyInfo.WritersRoot = await GetWritersRootAsync();
                policyInfo.AdminsRoot = await GetAdminsRootAsync();
                policyInfo.BlacklistRoot = await GetBlacklistRootAsync();
                policyInfo.Epoch = await GetEpochAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get current policy from contract");
            }

            return policyInfo;
        }

        public async Task<byte[]?> GetWritersRootAsync()
        {
            if (_policyService == null)
            {
                return _config.WritersRoot;
            }

            try
            {
                return await _policyService.WritersRootQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get writers root");
                return _config.WritersRoot;
            }
        }

        public async Task<byte[]?> GetAdminsRootAsync()
        {
            if (_policyService == null)
            {
                return _config.AdminsRoot;
            }

            try
            {
                return await _policyService.AdminsRootQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get admins root");
                return _config.AdminsRoot;
            }
        }

        public async Task<byte[]?> GetBlacklistRootAsync()
        {
            if (_policyService == null)
            {
                return _config.BlacklistRoot;
            }

            try
            {
                return await _policyService.BlacklistRootQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get blacklist root");
                return _config.BlacklistRoot;
            }
        }

        public async Task<BigInteger> GetEpochAsync()
        {
            if (_policyService == null)
            {
                return _config.Epoch;
            }

            try
            {
                return await _policyService.EpochQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get epoch");
                return _config.Epoch;
            }
        }

        public async Task<bool> IsValidWriterAsync(string address, byte[][] writerProof, byte[]? blacklistProof = null)
        {
            if (_policyService == null)
            {
                if (_config.AllowedWriters == null || _config.AllowedWriters.Count == 0)
                    return true;

                return _config.AllowedWriters.Contains(address.ToLowerInvariant()) ||
                       _config.AllowedWriters.Contains(address);
            }

            try
            {
                var writerProofList = new List<byte[]>(writerProof);
                var blacklistProofList = blacklistProof != null
                    ? new List<byte[]> { blacklistProof }
                    : new List<byte[]>();

                return await _policyService.IsValidWriterQueryAsync(address, writerProofList, blacklistProofList);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to verify writer {Address}", address);
                return false;
            }
        }
    }
}
