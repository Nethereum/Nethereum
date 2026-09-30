using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Services
{
    public class EnterpriseAccountOperatorService
    {
        private readonly AppChainDeployment _deployment;
        private readonly NethereumAccountService _accountService;
        private readonly SmartSessionService _smartSessionQuery;
        private readonly bool _sponsored;

        public string AccountAddress { get; }

        public EnterpriseAccountOperatorService(IWeb3 web3, IAAClient client, AppChainDeployment deployment, NethereumSmartAccount account, PaymasterConfig? paymaster = null)
        {
            if (web3 == null) throw new ArgumentNullException(nameof(web3));
            if (client == null) throw new ArgumentNullException(nameof(client));
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
            if (account == null) throw new ArgumentNullException(nameof(account));

            AccountAddress = account.Address;
            _sponsored = paymaster != null;
            _accountService = new NethereumAccountService(web3, account.Address);
            var handler = client.Configure(_accountService, account);
            if (paymaster != null)
                handler.WithPaymaster(paymaster);
            _smartSessionQuery = new SmartSessionService(web3, deployment.Modules.SmartSession);
        }

        public async Task<CappedRoleInstallResult> InstallCappedRoleAsync(CappedRoleSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var sessionConfig = BuildTier1SessionConfig(spec);
            var permissionId = await _smartSessionQuery.GetPermissionIdQueryAsync(sessionConfig.ToSession()).ConfigureAwait(false);
            sessionConfig.ModuleAddress = _deployment.Modules.SmartSession;

            var receipt = (AATransactionReceipt)await _accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig).ConfigureAwait(false);
            return new CappedRoleInstallResult(permissionId, receipt);
        }

        public async Task<TieredRoleInstallResult> InstallTieredRolesAsync(CappedRoleSpec tier1, QuorumRoleSpec tier2)
        {
            if (tier1 == null) throw new ArgumentNullException(nameof(tier1));
            if (tier2 == null) throw new ArgumentNullException(nameof(tier2));

            var tier1Session = BuildTier1SessionConfig(tier1).ToSession();
            var tier2Session = BuildTier2SessionConfig(tier2).ToSession();

            var tier1PermissionId = await _smartSessionQuery.GetPermissionIdQueryAsync(tier1Session).ConfigureAwait(false);
            var tier2PermissionId = await _smartSessionQuery.GetPermissionIdQueryAsync(tier2Session).ConfigureAwait(false);

            var combinedInitData = ByteUtil.Merge(
                new[] { (byte)SmartSessionMode.UnsafeEnable },
                new ABIEncode().GetABIParamsEncoded(new SessionArrayDto
                {
                    Sessions = new List<Session> { tier1Session, tier2Session }
                }));

            var receipt = (AATransactionReceipt)await _accountService.InstallModuleRequestAndWaitForReceiptAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _deployment.Modules.SmartSession, combinedInitData).ConfigureAwait(false);

            return new TieredRoleInstallResult(tier1PermissionId, tier2PermissionId, receipt);
        }

        public Task<TransactionReceipt> InstallGuardiansAsync(string socialRecoveryModuleAddress, int threshold, IReadOnlyList<string> guardians)
        {
            if (string.IsNullOrEmpty(socialRecoveryModuleAddress))
                throw new ArgumentException("Social recovery module address is required.", nameof(socialRecoveryModuleAddress));
            if (guardians == null || guardians.Count == 0)
                throw new ArgumentException("At least one guardian is required.", nameof(guardians));
            if (threshold < 1 || threshold > guardians.Count)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 1 and the number of guardians.");

            var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(guardians);
            return _accountService.InstallSocialRecoveryAndWaitForReceiptAsync(socialRecoveryModuleAddress, threshold, sortedGuardians);
        }

        private SmartSessionConfig BuildTier1SessionConfig(CappedRoleSpec spec)
        {
            var policyInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", spec.PolicyRuleId),
                new ABIValue("uint256", spec.Cap));

            var config = new SmartSessionConfig()
                .WithSessionValidator(_deployment.Modules.EcdsaSessionValidator)
                .WithSessionValidatorInitData(spec.SessionKeyAddress)
                .WithSalt(spec.Salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(spec.TargetAddress)
                    .WithSelector(spec.FunctionSelector)
                    .WithPolicy(spec.PolicyAddress, policyInitData)
                    .Build());
            ApplySponsorPermission(config);
            return config;
        }

        private SmartSessionConfig BuildTier2SessionConfig(QuorumRoleSpec spec)
        {
            var sortedOwners = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(spec.OwnerAddresses);
            var validatorInitData = new OwnableValidatorConfig(
                spec.ValidatorAddress, spec.Threshold, sortedOwners).GetInitData();

            var policyInitData = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes32", spec.PolicyRuleId),
                new ABIValue("uint256", spec.Cap));

            var config = new SmartSessionConfig()
                .WithSessionValidator(spec.ValidatorAddress)
                .WithSessionValidatorInitData(validatorInitData)
                .WithSalt(spec.Salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(spec.TargetAddress)
                    .WithSelector(spec.FunctionSelector)
                    .WithPolicy(spec.PolicyAddress, policyInitData)
                    .Build());
            ApplySponsorPermission(config);
            return config;
        }

        private void ApplySponsorPermission(SmartSessionConfig config)
        {
            if (!_sponsored) return;
            config.WithPaymasterPermission(true);
            config.WithSudoPolicy(_deployment.Modules.SudoPolicy);
        }

        [FunctionOutput]
        private class SessionArrayDto
        {
            [Parameter("tuple[]", "sessions", 1)]
            public virtual List<Session> Sessions { get; set; }
        }
    }

    public sealed class CappedRoleSpec
    {
        public string SessionKeyAddress { get; }
        public string TargetAddress { get; }
        public byte[] FunctionSelector { get; }
        public string PolicyAddress { get; }
        public byte[] PolicyRuleId { get; }
        public BigInteger Cap { get; }
        public byte[] Salt { get; }

        public CappedRoleSpec(
            string sessionKeyAddress,
            string targetAddress,
            byte[] functionSelector,
            string policyAddress,
            byte[] policyRuleId,
            BigInteger cap,
            byte[] salt)
        {
            SessionKeyAddress = string.IsNullOrEmpty(sessionKeyAddress)
                ? throw new ArgumentException("Session key address is required.", nameof(sessionKeyAddress))
                : sessionKeyAddress;
            TargetAddress = string.IsNullOrEmpty(targetAddress)
                ? throw new ArgumentException("Target address is required.", nameof(targetAddress))
                : targetAddress;
            FunctionSelector = functionSelector ?? throw new ArgumentNullException(nameof(functionSelector));
            PolicyAddress = string.IsNullOrEmpty(policyAddress)
                ? throw new ArgumentException("Policy address is required.", nameof(policyAddress))
                : policyAddress;
            PolicyRuleId = policyRuleId ?? throw new ArgumentNullException(nameof(policyRuleId));
            Cap = cap;
            Salt = salt ?? throw new ArgumentNullException(nameof(salt));
            if (Salt.Length != 32)
                throw new ArgumentException("Salt must be 32 bytes.", nameof(salt));
        }
    }

    public sealed class QuorumRoleSpec
    {
        public string ValidatorAddress { get; }
        public IReadOnlyList<string> OwnerAddresses { get; }
        public int Threshold { get; }
        public string TargetAddress { get; }
        public byte[] FunctionSelector { get; }
        public string PolicyAddress { get; }
        public byte[] PolicyRuleId { get; }
        public BigInteger Cap { get; }
        public byte[] Salt { get; }

        public QuorumRoleSpec(
            string validatorAddress,
            IReadOnlyList<string> ownerAddresses,
            int threshold,
            string targetAddress,
            byte[] functionSelector,
            string policyAddress,
            byte[] policyRuleId,
            BigInteger cap,
            byte[] salt)
        {
            ValidatorAddress = string.IsNullOrEmpty(validatorAddress)
                ? throw new ArgumentException("Validator address is required.", nameof(validatorAddress))
                : validatorAddress;
            if (ownerAddresses == null || ownerAddresses.Count == 0)
                throw new ArgumentException("At least one owner address is required.", nameof(ownerAddresses));
            var distinctCount = ownerAddresses.Select(a => a.ToLowerInvariant()).Distinct().Count();
            if (distinctCount != ownerAddresses.Count)
                throw new ArgumentException(
                    "Owner addresses must not contain duplicates - a duplicate silently reduces the real " +
                    "quorum, and OwnableValidator's on-chain isSortedAndUniquified() onInstall guard would " +
                    "reject it anyway.", nameof(ownerAddresses));
            OwnerAddresses = ownerAddresses;
            if (threshold < 1 || threshold > ownerAddresses.Count)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 1 and the number of owners.");
            Threshold = threshold;
            TargetAddress = string.IsNullOrEmpty(targetAddress)
                ? throw new ArgumentException("Target address is required.", nameof(targetAddress))
                : targetAddress;
            FunctionSelector = functionSelector ?? throw new ArgumentNullException(nameof(functionSelector));
            PolicyAddress = string.IsNullOrEmpty(policyAddress)
                ? throw new ArgumentException("Policy address is required.", nameof(policyAddress))
                : policyAddress;
            PolicyRuleId = policyRuleId ?? throw new ArgumentNullException(nameof(policyRuleId));
            Cap = cap;
            Salt = salt ?? throw new ArgumentNullException(nameof(salt));
            if (Salt.Length != 32)
                throw new ArgumentException("Salt must be 32 bytes.", nameof(salt));
        }
    }

    public sealed class CappedRoleInstallResult
    {
        public byte[] PermissionId { get; }
        public AATransactionReceipt Receipt { get; }

        public CappedRoleInstallResult(byte[] permissionId, AATransactionReceipt receipt)
        {
            PermissionId = permissionId;
            Receipt = receipt;
        }
    }

    public sealed class TieredRoleInstallResult
    {
        public byte[] Tier1PermissionId { get; }

        public byte[] Tier2PermissionId { get; }

        public AATransactionReceipt Receipt { get; }

        public TieredRoleInstallResult(byte[] tier1PermissionId, byte[] tier2PermissionId, AATransactionReceipt receipt)
        {
            Tier1PermissionId = tier1PermissionId;
            Tier2PermissionId = tier2PermissionId;
            Receipt = receipt;
        }
    }
}
