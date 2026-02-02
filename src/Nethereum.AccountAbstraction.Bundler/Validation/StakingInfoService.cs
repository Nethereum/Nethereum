using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public interface IStakingInfoService
    {
        Task<EntityInfo> GetEntityInfoAsync(string address, EntityType type, string entryPoint);
        Task<EntityInfo> GetSenderInfoAsync(string senderAddress, string entryPoint);
        Task<EntityInfo?> GetFactoryInfoAsync(byte[]? initCode, string entryPoint);
        Task<EntityInfo?> GetPaymasterInfoAsync(byte[]? paymasterAndData, string entryPoint);
        Task<bool> IsStakedAsync(string address, string entryPoint);
    }

    public class StakingInfoService : IStakingInfoService
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();

        public StakingInfoService(IWeb3 web3, BundlerConfig config)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }
        }

        public async Task<EntityInfo> GetEntityInfoAsync(string address, EntityType type, string entryPoint)
        {
            if (string.IsNullOrEmpty(address))
            {
                return EntityInfo.Create(type, address, isStaked: false);
            }

            if (!_entryPoints.TryGetValue(entryPoint.ToLowerInvariant(), out var epService))
            {
                return EntityInfo.Create(type, address, isStaked: false);
            }

            try
            {
                var depositInfo = await epService.GetDepositInfoQueryAsync(address);

                var isStaked = depositInfo.Info.Staked &&
                               depositInfo.Info.Stake >= _config.MinStake &&
                               depositInfo.Info.UnstakeDelaySec >= _config.MinUnstakeDelaySec;

                return EntityInfo.Create(
                    type,
                    address,
                    isStaked: isStaked,
                    stake: depositInfo.Info.Stake,
                    unstakeDelay: depositInfo.Info.UnstakeDelaySec);
            }
            catch
            {
                return EntityInfo.Create(type, address, isStaked: false);
            }
        }

        public Task<EntityInfo> GetSenderInfoAsync(string senderAddress, string entryPoint)
        {
            return GetEntityInfoAsync(senderAddress, EntityType.Sender, entryPoint);
        }

        public async Task<EntityInfo?> GetFactoryInfoAsync(byte[]? initCode, string entryPoint)
        {
            if (initCode == null || initCode.Length < 20)
            {
                return null;
            }

            var factoryAddress = "0x" + BitConverter.ToString(initCode, 0, 20).Replace("-", "").ToLowerInvariant();
            return await GetEntityInfoAsync(factoryAddress, EntityType.Factory, entryPoint);
        }

        public async Task<EntityInfo?> GetPaymasterInfoAsync(byte[]? paymasterAndData, string entryPoint)
        {
            if (paymasterAndData == null || paymasterAndData.Length < 20)
            {
                return null;
            }

            var paymasterAddress = "0x" + BitConverter.ToString(paymasterAndData, 0, 20).Replace("-", "").ToLowerInvariant();
            return await GetEntityInfoAsync(paymasterAddress, EntityType.Paymaster, entryPoint);
        }

        public async Task<bool> IsStakedAsync(string address, string entryPoint)
        {
            var info = await GetEntityInfoAsync(address, EntityType.None, entryPoint);
            return info.IsStaked;
        }
    }
}
