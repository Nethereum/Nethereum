using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.RPC;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public sealed class EnrolledUser
    {
        public string UserId { get; }
        public EthECKey OwnerKey { get; }
        public string OwnerAddress { get; }
        public string AccountAddress { get; }
        public BigInteger Salt { get; }
        public bool IsActive { get; set; }

        public EnrolledUser(string userId, EthECKey ownerKey, string accountAddress, BigInteger salt, bool isActive)
        {
            UserId = string.IsNullOrEmpty(userId) ? throw new ArgumentException("userId is required.", nameof(userId)) : userId;
            OwnerKey = ownerKey ?? throw new ArgumentNullException(nameof(ownerKey));
            OwnerAddress = ownerKey.GetPublicAddress();
            AccountAddress = string.IsNullOrEmpty(accountAddress) ? throw new ArgumentException("accountAddress is required.", nameof(accountAddress)) : accountAddress;
            Salt = salt;
            IsActive = isActive;
        }
    }

    public sealed record EnrolledUserSummary(string UserId, string OwnerAddress, string AccountAddress, bool IsActive, bool IsSelected);

    public sealed class EnrolledCappedRole
    {
        public string UserId { get; }
        public EthECKey SessionKey { get; }
        public string SessionKeyAddress { get; }
        public byte[] PermissionId { get; }
        public BigInteger Cap { get; }

        public EnrolledCappedRole(string userId, EthECKey sessionKey, byte[] permissionId, BigInteger cap)
        {
            UserId = string.IsNullOrEmpty(userId) ? throw new ArgumentException("userId is required.", nameof(userId)) : userId;
            SessionKey = sessionKey ?? throw new ArgumentNullException(nameof(sessionKey));
            SessionKeyAddress = sessionKey.GetPublicAddress();
            PermissionId = permissionId ?? throw new ArgumentNullException(nameof(permissionId));
            Cap = cap;
        }
    }

    public sealed class OffboardProgress
    {
        public string UserId { get; }
        public EthECKey SessionKey { get; }
        public string SessionKeyAddress { get; }
        public byte[] PermissionId { get; }
        public BigInteger Cap { get; }
        public IReadOnlyList<EthECKey> GuardianKeys { get; }
        public int GuardianThreshold { get; }

        public EthECKey? NewOwnerKey { get; private set; }

        public bool IsRotated { get; private set; }
        public bool IsSessionRevoked { get; private set; }
        public bool IsModuleUninstalled { get; private set; }
        public bool IsSwept { get; private set; }
        public bool IsBanned { get; private set; }

        public OffboardProgress(
            string userId,
            EthECKey sessionKey,
            byte[] permissionId,
            BigInteger cap,
            IReadOnlyList<EthECKey> guardianKeys,
            int guardianThreshold)
        {
            UserId = string.IsNullOrEmpty(userId) ? throw new ArgumentException("userId is required.", nameof(userId)) : userId;
            SessionKey = sessionKey ?? throw new ArgumentNullException(nameof(sessionKey));
            SessionKeyAddress = sessionKey.GetPublicAddress();
            PermissionId = permissionId ?? throw new ArgumentNullException(nameof(permissionId));
            Cap = cap;
            GuardianKeys = guardianKeys ?? throw new ArgumentNullException(nameof(guardianKeys));
            if (guardianThreshold < 1 || guardianThreshold > guardianKeys.Count)
                throw new ArgumentOutOfRangeException(nameof(guardianThreshold), "Threshold must be between 1 and the number of guardians.");
            GuardianThreshold = guardianThreshold;
        }

        public void RecordRotated(EthECKey newOwnerKey)
        {
            NewOwnerKey = newOwnerKey ?? throw new ArgumentNullException(nameof(newOwnerKey));
            IsRotated = true;
        }

        public void RecordSessionRevoked() => IsSessionRevoked = true;
        public void RecordModuleUninstalled() => IsModuleUninstalled = true;
        public void RecordSwept() => IsSwept = true;
        public void RecordBanned() => IsBanned = true;

        public EthECKey RequireNewOwnerKey() =>
            NewOwnerKey ?? throw new InvalidOperationException($"userId '{UserId}' has not had its owner rotated yet - run Rotate Owner first.");
    }

    public sealed class TieredRolesProgress
    {
        public string UserId { get; }

        public EthECKey OperatorKey { get; }
        public string OperatorAddress { get; }
        public byte[] Tier1PermissionId { get; }
        public BigInteger SmallCap { get; }

        public IReadOnlyList<EthECKey> MemberKeys { get; }
        public byte[] Tier2PermissionId { get; }
        public int Threshold { get; }
        public BigInteger LargeCap { get; }

        public bool IsQuorumPaid { get; private set; }

        public TieredRolesProgress(
            string userId,
            EthECKey operatorKey,
            byte[] tier1PermissionId,
            BigInteger smallCap,
            IReadOnlyList<EthECKey> memberKeys,
            byte[] tier2PermissionId,
            int threshold,
            BigInteger largeCap)
        {
            UserId = string.IsNullOrEmpty(userId) ? throw new ArgumentException("userId is required.", nameof(userId)) : userId;
            OperatorKey = operatorKey ?? throw new ArgumentNullException(nameof(operatorKey));
            OperatorAddress = operatorKey.GetPublicAddress();
            Tier1PermissionId = tier1PermissionId ?? throw new ArgumentNullException(nameof(tier1PermissionId));
            SmallCap = smallCap;
            if (memberKeys == null || memberKeys.Count == 0)
                throw new ArgumentException("At least one tier-2 member key is required.", nameof(memberKeys));
            MemberKeys = memberKeys;
            Tier2PermissionId = tier2PermissionId ?? throw new ArgumentNullException(nameof(tier2PermissionId));
            if (threshold < 1 || threshold > memberKeys.Count)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 1 and the number of member keys.");
            Threshold = threshold;
            LargeCap = largeCap;
        }

        public void RecordQuorumPaid() => IsQuorumPaid = true;
    }

    public partial class SessionState : ObservableObject
    {
        public AppChainDeployment? Deployment { get; set; }
        public IAAClient? Client { get; set; }
        public AppChainAccountAdminService? AdminService { get; set; }
        public IWeb3? Web3 { get; set; }
        public IAccountAbstractionBundlerService? Bundler { get; set; }

        public byte[]? CapRuleId { get; set; }

        public string? ValueCapCombinatorAddress { get; set; }

        public string? PayableTargetAddress { get; set; }

        public string? OwnableValidatorAddress { get; set; }

        public IAsyncDisposable? InfraResource { get; set; }

        public PaymasterConfig? Sponsor => string.IsNullOrEmpty(Deployment?.SponsoredPaymasterAddress)
            ? null
            : new PaymasterConfig(Deployment.SponsoredPaymasterAddress);

        public void ApplySponsor(AAContractHandler handler)
        {
            if (Sponsor is not null)
                handler.WithPaymaster(Sponsor);
        }

        [ObservableProperty]
        private bool _isReady;

        public event Action? InfraChanged;

        [ObservableProperty]
        private string? _activeUserId;

        private readonly Dictionary<string, EthECKey> _pendingOwnerKeys = new();
        private readonly Dictionary<string, EnrolledUser> _enrolledUsers = new();
        private readonly Dictionary<string, EnrolledCappedRole> _cappedRoles = new();
        private readonly Dictionary<string, OffboardProgress> _offboardProgress = new();
        private readonly Dictionary<string, TieredRolesProgress> _tieredRoles = new();

        public IReadOnlyDictionary<string, EnrolledUser> EnrolledUsers => _enrolledUsers;

        public IAsyncDisposable? PublishInfra(ProvisionedEnterpriseInfra infra)
        {
            if (infra is null) throw new ArgumentNullException(nameof(infra));
            var previousResource = InfraResource;
            Deployment = infra.Deployment;
            Client = infra.Client;
            AdminService = infra.AdminService;
            Web3 = infra.Web3;
            Bundler = infra.Bundler;
            CapRuleId = infra.CapRuleId;
            ValueCapCombinatorAddress = infra.ValueCapCombinatorAddress;
            PayableTargetAddress = infra.PayableTargetAddress;
            OwnableValidatorAddress = infra.OwnableValidatorAddress;
            InfraResource = infra.Resource;
            IsReady = true;
            ActiveUserId = null;
            InfraChanged?.Invoke();
            return previousResource;
        }

        public void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("Provision the AppChain infrastructure first.");
        }

        public void SelectActiveUser(string userId)
        {
            RequireEnrolledUser(userId);
            ActiveUserId = userId;
        }

        public EnrolledUser RequireActiveUser() =>
            string.IsNullOrWhiteSpace(ActiveUserId)
                ? throw new InvalidOperationException("Select a user on the Create / Select user tab first.")
                : RequireEnrolledUser(ActiveUserId);

        public void RegisterOwnerKey(string userId, EthECKey ownerKey)
        {
            if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required.", nameof(userId));
            _pendingOwnerKeys[userId] = ownerKey ?? throw new ArgumentNullException(nameof(ownerKey));
        }

        public string? ResolveOwnerAddress(string userId) =>
            _pendingOwnerKeys.TryGetValue(userId, out var key) ? key.GetPublicAddress() : null;

        public EnrolledUser RecordEnrolled(string userId, string accountAddress, BigInteger salt, bool isActive)
        {
            if (!_pendingOwnerKeys.TryGetValue(userId, out var ownerKey))
                throw new InvalidOperationException($"No owner key was registered for userId '{userId}' before enrollment.");

            var user = new EnrolledUser(userId, ownerKey, accountAddress, salt, isActive);
            _enrolledUsers[userId] = user;
            return user;
        }

        public bool TryGetEnrolledUser(string userId, out EnrolledUser? user) => _enrolledUsers.TryGetValue(userId, out user);

        public EnrolledUser RequireEnrolledUser(string userId) =>
            _enrolledUsers.TryGetValue(userId, out var user)
                ? user
                : throw new InvalidOperationException($"userId '{userId}' has not been enrolled - enroll it in Admin first.");

        public void RecordCappedRole(string userId, EnrolledCappedRole role) => _cappedRoles[userId] = role;

        public EnrolledCappedRole RequireCappedRole(string userId) =>
            _cappedRoles.TryGetValue(userId, out var role)
                ? role
                : throw new InvalidOperationException($"userId '{userId}' has no installed capped role - install one in Operator first.");

        public void RecordOffboardSetup(string userId, OffboardProgress progress) => _offboardProgress[userId] = progress;

        public bool TryGetOffboardProgress(string userId, out OffboardProgress? progress) => _offboardProgress.TryGetValue(userId, out progress);

        public OffboardProgress RequireOffboardProgress(string userId) =>
            _offboardProgress.TryGetValue(userId, out var progress)
                ? progress
                : throw new InvalidOperationException($"userId '{userId}' has no offboard set up - run Setup in Offboard first.");

        public void RecordTieredRoles(string userId, TieredRolesProgress progress) => _tieredRoles[userId] = progress;

        public bool TryGetTieredRoles(string userId, out TieredRolesProgress? progress) => _tieredRoles.TryGetValue(userId, out progress);

        public TieredRolesProgress RequireTieredRoles(string userId) =>
            _tieredRoles.TryGetValue(userId, out var progress)
                ? progress
                : throw new InvalidOperationException($"userId '{userId}' has no tiered roles installed - run Install Tier-2 Role first.");
    }
}
