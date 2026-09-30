using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public partial class TieredApprovalViewModel : TabViewModel
    {
        private const int MemberCount = 3;
        private const int Threshold = 2;

        private readonly SessionState _session;

        [ObservableProperty]
        private BigInteger _smallCap = 100;

        [ObservableProperty]
        private BigInteger _largeCap = 1000;

        [ObservableProperty]
        private BigInteger _quorumPayAmount = 600;

        [ObservableProperty]
        private BigInteger _overCapAmount = 1500;

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private string? _operatorAddress;

        [ObservableProperty]
        private string? _tier1PermissionIdHex;

        [ObservableProperty]
        private string? _tier2PermissionIdHex;

        public ObservableCollection<string> MemberAddresses { get; } = new();

        [ObservableProperty]
        private bool _isQuorumPaid;

        [ObservableProperty]
        private BigInteger _targetBalance;

        [ObservableProperty]
        private bool? _underQuorumRejected;

        [ObservableProperty]
        private bool? _overCapRejected;

        [ObservableProperty]
        private bool? _tier1OverItsTierRejected;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        public TieredApprovalViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.PropertyChanged += OnSessionPropertyChanged;
            PropertyChanged += OnOwnPropertyChanged;
        }

        public string? ActiveUserId => _session.ActiveUserId;

        public bool CanOperate => !IsBusy && ActiveUserId != null;

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SessionState.ActiveUserId)) return;

            IsInstalled = false;
            OperatorAddress = null;
            Tier1PermissionIdHex = null;
            Tier2PermissionIdHex = null;
            MemberAddresses.Clear();
            IsQuorumPaid = false;
            TargetBalance = 0;
            UnderQuorumRejected = null;
            OverCapRejected = null;
            Tier1OverItsTierRejected = null;
            LastReceipt = null;
            StatusMessage = null;
            ErrorMessage = null;
            OnPropertyChanged(nameof(ActiveUserId));
            OnPropertyChanged(nameof(CanOperate));
        }

        private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsBusy))
                OnPropertyChanged(nameof(CanOperate));
        }

        [RelayCommand]
        private Task InstallTier2RoleAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            if (_session.TryGetTieredRoles(user.UserId, out _))
                throw new InvalidOperationException($"Tiered roles are already installed for '{user.UserId}' - each userId installs its tiered roles once per session.");

            var ownerAccount = await _session.Client!.CreateAccountAsync(user.OwnerKey, ToSaltBytes(user.Salt)).ConfigureAwait(false);
            var operatorService = new EnterpriseAccountOperatorService(_session.Web3!, _session.Client!, _session.Deployment!, ownerAccount, _session.Sponsor);

            var depositSelector = new DepositFunction().GetCallData();

            var operatorKey = EthECKey.GenerateKey();
            var tier1Spec = new CappedRoleSpec(
                operatorKey.GetPublicAddress(),
                _session.PayableTargetAddress!,
                depositSelector,
                _session.ValueCapCombinatorAddress!,
                _session.CapRuleId!,
                SmallCap,
                RandomSalt32());

            var memberKeys = Enumerable.Range(0, MemberCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedMemberAddresses = MultiGuardianSignatureBlobBuilder
                .SortAddressesAscending(memberKeys.Select(k => k.GetPublicAddress())).ToList();

            var tier2Spec = new QuorumRoleSpec(
                _session.OwnableValidatorAddress!,
                sortedMemberAddresses,
                Threshold,
                _session.PayableTargetAddress!,
                depositSelector,
                _session.ValueCapCombinatorAddress!,
                _session.CapRuleId!,
                LargeCap,
                RandomSalt32());

            var result = await operatorService.InstallTieredRolesAsync(tier1Spec, tier2Spec).ConfigureAwait(false);
            LastReceipt = result.Receipt;

            if (!result.Receipt.UserOpSuccess)
            {
                StatusMessage = IsModuleAlreadyInstalledRejection(result.Receipt)
                    ? $"'{user.UserId}' already has a capability installed by another tab in this session - " +
                      "the SmartSession module installs once per account. Use a different user for this tab."
                    : result.Receipt.FailureDiagnostic;
                return;
            }

            var progress = new TieredRolesProgress(
                user.UserId,
                operatorKey,
                result.Tier1PermissionId,
                SmallCap,
                memberKeys,
                result.Tier2PermissionId,
                Threshold,
                LargeCap);
            _session.RecordTieredRoles(user.UserId, progress);

            OperatorAddress = operatorKey.GetPublicAddress();
            Tier1PermissionIdHex = result.Tier1PermissionId.ToHex(true);
            Tier2PermissionIdHex = result.Tier2PermissionId.ToHex(true);
            MemberAddresses.Clear();
            foreach (var address in sortedMemberAddresses)
                MemberAddresses.Add(address);

            IsInstalled = true;
            IsQuorumPaid = false;
            UnderQuorumRejected = null;
            OverCapRejected = null;
            Tier1OverItsTierRejected = null;

            StatusMessage = $"Tiered roles installed for '{user.UserId}' - tier-1 operator key {OperatorAddress} capped at {SmallCap} wei, " +
                             $"tier-2 {Threshold}-of-{MemberCount} member quorum ({string.Join(", ", MemberAddresses)}) capped at {LargeCap} wei " +
                             $"(userOpHash {result.Receipt.UserOpHash}).";
        });

        [RelayCommand]
        private Task PayWithQuorumAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = RequireProgress(user);

            var payableTarget = BuildQuorumPayableTarget(user, progress, progress.MemberKeys.Take(progress.Threshold).ToList(), progress.Threshold);

            var balanceBefore = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = QuorumPayAmount }).ConfigureAwait(false);
            LastReceipt = receipt;

            if (!receipt.UserOpSuccess)
            {
                StatusMessage = receipt.FailureDiagnostic;
                return;
            }

            var balanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balanceAfter.Value;
            progress.RecordQuorumPaid();
            IsQuorumPaid = true;

            StatusMessage = $"Deposited {QuorumPayAmount} wei (within the tier-2 cap of {progress.LargeCap}, above the tier-1 cap of {progress.SmallCap}) " +
                             $"via a {progress.Threshold}-of-{progress.MemberKeys.Count} member quorum - the value moved because enough members co-signed " +
                             $"ONE userOp, not because any single key could. PayableTarget's balance moved from {balanceBefore.Value} to {balanceAfter.Value} wei.";
        });

        [RelayCommand]
        private Task TryUnderQuorumAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = RequireProgress(user);
            var payableTarget = BuildUnderQuorumPayableTarget(user, progress);

            try
            {
                var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = QuorumPayAmount }).ConfigureAwait(false);
                LastReceipt = receipt;
                UnderQuorumRejected = false;
                StatusMessage = $"UNEXPECTED: a single member's signature (short of the {progress.Threshold}-of-{progress.MemberKeys.Count} quorum) " +
                                 $"moved {QuorumPayAmount} wei - the authority did not hold.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsUnderQuorumRejection(ex))
                    throw;
                UnderQuorumRejected = true;
            }

            var balanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balanceAfter.Value;
            StatusMessage = $"1-of-{progress.MemberKeys.Count} was REJECTED by the tier-2 AUTHORITY as expected (needs {progress.Threshold}, " +
                             $"InvalidSignature {InvalidSignatureSelector}); PayableTarget's balance stayed at {TargetBalance} wei.";
        });

        [RelayCommand]
        private Task TryOverCapAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = RequireProgress(user);
            var payableTarget = BuildQuorumPayableTarget(user, progress, progress.MemberKeys.Take(progress.Threshold).ToList(), progress.Threshold);

            try
            {
                var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = OverCapAmount }).ConfigureAwait(false);
                LastReceipt = receipt;
                OverCapRejected = false;
                StatusMessage = $"UNEXPECTED: a genuine {progress.Threshold}-of-{progress.MemberKeys.Count} quorum moved {OverCapAmount} wei above " +
                                 $"the tier-2 cap of {progress.LargeCap} - the cap policy did not hold.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsCapPolicyRejection(ex))
                    throw;
                OverCapRejected = true;
            }

            var balanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balanceAfter.Value;
            StatusMessage = $"A genuine {progress.Threshold}-of-{progress.MemberKeys.Count} quorum was REJECTED by the tier-2 CAP POLICY as expected " +
                             $"({OverCapAmount} wei above the cap of {progress.LargeCap}, PolicyViolation {PolicyViolationSelector}, no InvalidSignature " +
                             $"marker); PayableTarget's balance stayed at {TargetBalance} wei.";
        });

        [RelayCommand]
        private Task TryTier1OverItsTierAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = RequireProgress(user);
            var payableTarget = BuildTier1PayableTarget(user, progress);

            try
            {
                var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = QuorumPayAmount }).ConfigureAwait(false);
                LastReceipt = receipt;
                Tier1OverItsTierRejected = false;
                StatusMessage = $"UNEXPECTED: the tier-1 operator key {progress.OperatorAddress} moved {QuorumPayAmount} wei above its own cap " +
                                 $"of {progress.SmallCap} - the tier boundary did not hold.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsCapPolicyRejection(ex))
                    throw;
                Tier1OverItsTierRejected = true;
            }

            var balanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balanceAfter.Value;
            StatusMessage = $"Tier-1 operator key {progress.OperatorAddress} was REJECTED by ITS OWN cap policy as expected ({QuorumPayAmount} wei " +
                             $"above its cap of {progress.SmallCap}, PolicyViolation {PolicyViolationSelector}); PayableTarget's balance stayed at " +
                             $"{TargetBalance} wei.";
        });

        private static readonly string PolicyViolationSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);

        private static readonly string InvalidSignatureSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);

        private static readonly string ModuleAlreadyInstalledSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("ModuleAlreadyInstalled(address)"))[..4].ToHex(true);

        private static bool IsUnderQuorumRejection(Exception ex)
        {
            var text = FlattenMessages(ex);
            return text.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(InvalidSignatureSelector, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCapPolicyRejection(Exception ex)
        {
            var text = FlattenMessages(ex);
            return text.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(PolicyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(InvalidSignatureSelector, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsModuleAlreadyInstalledRejection(AATransactionReceipt receipt) =>
            !string.IsNullOrEmpty(receipt.RevertReason) &&
            receipt.RevertReason.IndexOf(ModuleAlreadyInstalledSelector, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string FlattenMessages(Exception ex)
        {
            var builder = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
                builder.AppendLine(current.Message);
            return builder.ToString();
        }

        private PayableTargetService BuildQuorumPayableTarget(EnrolledUser user, TieredRolesProgress progress, IReadOnlyList<EthECKey> signers, int threshold)
        {
            var signingService = new OwnableValidatorSessionSigningService(progress.Tier2PermissionId, signers, threshold);
            var validatorModule = new OwnableValidatorSessionValidatorModule(
                _session.Deployment!.Modules.SmartSession, progress.Tier2PermissionId, signatureSlotCount: threshold);
            var account = _session.Client!.GetAccount(user.AccountAddress, signingService, validatorModule);

            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var handler = _session.Client!.Configure(payableTarget, account);
            _session.ApplySponsor(handler);
            return payableTarget;
        }

        private PayableTargetService BuildUnderQuorumPayableTarget(EnrolledUser user, TieredRolesProgress progress)
        {
            var signingService = new OwnableValidatorSessionSigningService(
                progress.Tier2PermissionId,
                hash => MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(hash, new[] { progress.MemberKeys[0] }, 1));
            var validatorModule = new OwnableValidatorSessionValidatorModule(
                _session.Deployment!.Modules.SmartSession, progress.Tier2PermissionId, signatureSlotCount: 1);
            var account = _session.Client!.GetAccount(user.AccountAddress, signingService, validatorModule);

            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var handler = _session.Client!.Configure(payableTarget, account);
            _session.ApplySponsor(handler);
            return payableTarget;
        }

        private PayableTargetService BuildTier1PayableTarget(EnrolledUser user, TieredRolesProgress progress)
        {
            var account = _session.Client!.GetAccount(
                user.AccountAddress,
                new SmartSessionKeySigningService(progress.OperatorKey, progress.Tier1PermissionId),
                new SmartSessionValidatorModule(_session.Deployment!.Modules.SmartSession, progress.Tier1PermissionId));

            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var handler = _session.Client!.Configure(payableTarget, account);
            _session.ApplySponsor(handler);
            return payableTarget;
        }

        private TieredRolesProgress RequireProgress(EnrolledUser user) => _session.RequireTieredRoles(user.UserId);

        private static byte[] ToSaltBytes(BigInteger salt) =>
            salt.ToByteArray(isUnsigned: true, isBigEndian: true).PadBytesLeft(32);

        private static byte[] RandomSalt32()
        {
            var salt = new byte[32];
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 0, 16);
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 16, 16);
            return salt;
        }
    }
}
