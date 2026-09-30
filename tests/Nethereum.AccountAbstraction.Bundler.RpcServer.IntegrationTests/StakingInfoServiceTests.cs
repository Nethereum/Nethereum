using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    /// <summary>
    /// Covers the ERC-7562 staked-status lookup fix (StakingInfoService.GetEntityAsync).
    ///
    /// Root cause (confirmed against a real external node, not a mock): the check itself was
    /// correct, and BundlerConfig's defaults (MinStake=1 ETH, MinUnstakeDelaySec=86400) exactly
    /// match ERC-7562's SREP-010 ("staked" = stake >= MIN_STAKE_VALUE AND unstakeDelay >=
    /// MIN_UNSTAKE_DELAY=86400). But eth-infinitism's bundler-spec-tests compliance suite stakes
    /// entities with a 2-SECOND unstake delay (tests/utils.py staked_contract():
    /// addStake(entryPoint, 2) with 1 ETH) - the reference bundler itself is only ever run
    /// against that suite with minUnstakeDelay overridden to 0 (see
    /// Nethereum.XUnitEthereumClients.StrictBundlerFixture's bundler.nethereum.config.json).
    /// BundlerRpcServerConfig had no MinStake/MinUnstakeDelaySec properties at all - the same
    /// host-gap class as MaxVerificationGas/EnableERC7562Validation (B2-T33) - so
    /// ToBundlerConfig() always fell back to the spec-default 86400s and there was no operator
    /// knob to align it with a compliance run's 2-second stakes. A genuinely staked entity was
    /// then correctly-per-spec-but-uselessly-for-compliance read as unstaked.
    ///
    /// Separately, StakingInfoService.GetEntityAsync silently swallowed ANY exception from
    /// the getDepositInfo query (network failure, bad ABI decode) and reported "unstaked" -
    /// masking real infrastructure failures as a security-relevant false negative. That catch
    /// has been removed; the failure now propagates to UserOpValidator.ValidateERC7562Async's
    /// existing try/catch, which reports it as a genuine validation error instead.
    /// </summary>
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class StakingInfoServiceTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public StakingInfoServiceTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<string> StakeFreshEntityAsync(uint unstakeDelaySec, decimal stakeEth = 1m)
        {
            var stakerKey = EthECKey.GenerateKey();
            var stakerAddress = stakerKey.GetPublicAddress();
            await _fixture.FundAccountAsync(stakerAddress, stakeEth + 0.1m);

            var stakerWeb3 = new Nethereum.Web3.Web3(new Account(stakerKey.GetPrivateKey()), _fixture.Web3.Client);
            var stakerEntryPoint = new EntryPointService(stakerWeb3, _fixture.EntryPointService.ContractAddress);

            await stakerEntryPoint.AddStakeRequestAndWaitForReceiptAsync(new AddStakeFunction
            {
                UnstakeDelaySec = unstakeDelaySec,
                AmountToSend = Web3.Web3.Convert.ToWei(stakeEth)
            });

            return stakerAddress;
        }

        [Fact]
        public async Task GetEntityAsync_SuiteStyleStake_ReadsUnstakedUnderSpecDefaultDelay()
        {
            // Mirrors bundler-spec-tests tests/utils.py staked_contract(): addStake(entryPoint, 2)
            // with 1 ETH. Genuinely staked on-chain (EntryPoint.getDepositInfo().staked == true),
            // but 2 seconds does not meet ERC-7562's MIN_UNSTAKE_DELAY=86400 - this is correct,
            // spec-faithful behaviour under the production default, not a bug.
            var stakedAddress = await StakeFreshEntityAsync(2);

            var onChainInfo = await _fixture.EntryPointService.GetDepositInfoQueryAsync(stakedAddress);
            Assert.True(onChainInfo.Info.Staked, "precondition: EntryPoint must report the entity as staked");

            var specDefaultConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var service = new StakingInfoService(_fixture.Web3, specDefaultConfig);
            var info = await service.GetEntityAsync(stakedAddress, EntityType.Sender, _fixture.EntryPointService.ContractAddress);

            Assert.False(info.IsStaked,
                "a 2-second unstake delay must not qualify as staked under the spec's 86400s MIN_UNSTAKE_DELAY default");
        }

        [Fact]
        public async Task GetEntityAsync_SuiteStyleStake_ReadsStakedWhenThresholdLoweredForCompliance()
        {
            var stakedAddress = await StakeFreshEntityAsync(2);

            var complianceConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId,
                MinUnstakeDelaySec = 2
            };

            var service = new StakingInfoService(_fixture.Web3, complianceConfig);
            var info = await service.GetEntityAsync(stakedAddress, EntityType.Sender, _fixture.EntryPointService.ContractAddress);

            Assert.True(info.IsStaked,
                "with MinUnstakeDelaySec lowered to match the compliance suite's 2-second stake, a genuinely staked entity must read as staked");
            Assert.Equal(Web3.Web3.Convert.ToWei(1), info.StakeAmount);
            Assert.Equal((ulong)2, info.UnstakeDelaySec);
        }

        [Fact]
        public async Task GetEntityAsync_NeverStaked_ReadsUnstakedRegardlessOfThreshold()
        {
            var freshAddress = EthECKey.GenerateKey().GetPublicAddress();

            var permissiveConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId,
                MinStake = 0,
                MinUnstakeDelaySec = 0
            };

            var service = new StakingInfoService(_fixture.Web3, permissiveConfig);
            var info = await service.GetEntityAsync(freshAddress, EntityType.Sender, _fixture.EntryPointService.ContractAddress);

            Assert.False(info.IsStaked,
                "an address that never called addStake must read as unstaked even with a zero threshold - no over-correction");
        }

        [Fact]
        public async Task GetEntityAsync_QueryFailure_ThrowsInsteadOfSilentlyReportingUnstaked()
        {
            var notAnEntryPoint = _fixture.AccountFactoryService.ContractAddress;

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { notAnEntryPoint },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var service = new StakingInfoService(_fixture.Web3, config);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                service.GetEntityAsync(_fixture.BeneficiaryAddress, EntityType.Sender, notAnEntryPoint));
        }
    }

    public class BundlerRpcServerConfigStakeThresholdTests
    {
        [Fact]
        public void ToBundlerConfig_PropagatesConfiguredStakeThresholds()
        {
            var serverConfig = new BundlerRpcServerConfig
            {
                BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
                SupportedEntryPoints = new[] { "0x0000000000000000000000000000000000dEaD" },
                MinStake = 5,
                MinUnstakeDelaySec = 2
            };

            var bundlerConfig = serverConfig.ToBundlerConfig();

            Assert.Equal(5, bundlerConfig.MinStake);
            Assert.Equal((uint)2, bundlerConfig.MinUnstakeDelaySec);
        }

        [Fact]
        public void MinStakeAndMinUnstakeDelaySec_DefaultsMatchErc7562Srep010()
        {
            var serverConfig = new BundlerRpcServerConfig();

            Assert.Equal(Web3.Web3.Convert.ToWei(1), serverConfig.MinStake);
            Assert.Equal((uint)86400, serverConfig.MinUnstakeDelaySec);
            Assert.Equal(serverConfig.MinStake, serverConfig.ToBundlerConfig().MinStake);
            Assert.Equal(serverConfig.MinUnstakeDelaySec, serverConfig.ToBundlerConfig().MinUnstakeDelaySec);
        }
    }
}
