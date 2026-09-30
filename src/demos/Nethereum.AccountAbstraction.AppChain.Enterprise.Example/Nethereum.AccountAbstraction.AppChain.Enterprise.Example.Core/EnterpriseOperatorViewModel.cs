using System.ComponentModel;
using System.Numerics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public partial class EnterpriseOperatorViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private BigInteger _cap = 100;

        [ObservableProperty]
        private BigInteger _withinCapAmount = 60;

        [ObservableProperty]
        private BigInteger _overCapAmount = 150;

        [ObservableProperty]
        private BigInteger _newCap = 500;

        [ObservableProperty]
        private bool _hasCappedRole;

        [ObservableProperty]
        private string? _sessionKeyAddress;

        [ObservableProperty]
        private string? _permissionIdHex;

        [ObservableProperty]
        private BigInteger _targetBalance;

        [ObservableProperty]
        private bool? _overCapRejected;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        public EnterpriseOperatorViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.PropertyChanged += OnSessionPropertyChanged;
            PropertyChanged += OnOwnPropertyChanged;
        }

        public string? ActiveUserId => _session.ActiveUserId;

        public bool CanOperate => !IsBusy && ActiveUserId != null;

        public bool CanEditRole => !IsBusy && ActiveUserId != null && HasCappedRole;

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SessionState.ActiveUserId)) return;

            HasCappedRole = false;
            SessionKeyAddress = null;
            PermissionIdHex = null;
            TargetBalance = 0;
            OverCapRejected = null;
            LastReceipt = null;
            StatusMessage = null;
            ErrorMessage = null;
            OnPropertyChanged(nameof(ActiveUserId));
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanEditRole));
        }

        private void OnOwnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsBusy))
                OnPropertyChanged(nameof(CanOperate));
            if (e.PropertyName == nameof(IsBusy) || e.PropertyName == nameof(HasCappedRole))
                OnPropertyChanged(nameof(CanEditRole));
        }

        [RelayCommand]
        private Task InstallCappedRoleAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();

            var ownerAccount = await _session.Client!.CreateAccountAsync(user.OwnerKey, ToSaltBytes(user.Salt)).ConfigureAwait(false);
            var operatorService = new EnterpriseAccountOperatorService(_session.Web3!, _session.Client!, _session.Deployment!, ownerAccount, _session.Sponsor);

            var sessionKey = EthECKey.GenerateKey();
            var depositSelector = new DepositFunction().GetCallData();
            var spec = new CappedRoleSpec(
                sessionKey.GetPublicAddress(),
                _session.PayableTargetAddress!,
                depositSelector,
                _session.ValueCapCombinatorAddress!,
                _session.CapRuleId!,
                Cap,
                RandomSessionSalt());

            var result = await operatorService.InstallCappedRoleAsync(spec).ConfigureAwait(false);
            LastReceipt = result.Receipt;

            if (!result.Receipt.UserOpSuccess)
            {
                StatusMessage = IsModuleAlreadyInstalledRejection(result.Receipt)
                    ? $"'{user.UserId}' already has a capability installed by another tab in this session - " +
                      "the SmartSession module installs once per account. Use a different user for this tab."
                    : result.Receipt.FailureDiagnostic;
                return;
            }

            _session.RecordCappedRole(user.UserId, new EnrolledCappedRole(user.UserId, sessionKey, result.PermissionId, Cap));
            SessionKeyAddress = sessionKey.GetPublicAddress();
            PermissionIdHex = result.PermissionId.ToHex(true);
            HasCappedRole = true;
            OverCapRejected = null;

            StatusMessage = $"Capped role installed for '{user.UserId}' - session key {SessionKeyAddress} may call " +
                             $"PayableTarget.deposit up to a cap of {Cap} wei (userOpHash {result.Receipt.UserOpHash}).";
        });

        [RelayCommand]
        private Task PayWithinCapAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var payableTarget = BuildOperatorPayableTarget();

            var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapAmount }).ConfigureAwait(false);
            LastReceipt = receipt;

            if (!receipt.UserOpSuccess)
            {
                StatusMessage = receipt.FailureDiagnostic;
                return;
            }

            var balance = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balance.Value;
            OverCapRejected = null;
            StatusMessage = $"Deposited {WithinCapAmount} wei (within the cap of {Cap}) - PayableTarget's balance is now {TargetBalance} wei.";
        });

        [RelayCommand]
        private Task TryOverCapAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var payableTarget = BuildOperatorPayableTarget();

            try
            {
                var receipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = OverCapAmount }).ConfigureAwait(false);
                LastReceipt = receipt;
                OverCapRejected = false;
                StatusMessage = $"UNEXPECTED: a deposit of {OverCapAmount} wei above the cap of {Cap} succeeded - the policy did not hold.";
            }
            catch (Exception ex)
            {
                if (!IsCapPolicyRejection(ex))
                    throw;

                OverCapRejected = true;
                StatusMessage = $"Deposit of {OverCapAmount} wei was REJECTED by the cap policy as expected " +
                                $"(cap {Cap}, PolicyViolation {PolicyViolationSelector}).";
            }
        });

        [RelayCommand]
        private Task EditCapAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            if (!HasCappedRole)
                throw new InvalidOperationException($"'{user.UserId}' has no installed capped role to edit - install one first.");
            var previousRole = _session.RequireCappedRole(user.UserId);

            var ownerAccount = await _session.Client!.CreateAccountAsync(user.OwnerKey, ToSaltBytes(user.Salt)).ConfigureAwait(false);

            var accountService = new NethereumAccountService(_session.Web3!, user.AccountAddress);
            var uninstallHandler = _session.Client!.Configure(accountService, ownerAccount);
            _session.ApplySponsor(uninstallHandler);

            var uninstallReceipt = (AATransactionReceipt)await accountService.UninstallModuleRequestAndWaitForReceiptAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _session.Deployment!.Modules.SmartSession, Array.Empty<byte>()).ConfigureAwait(false);
            if (!uninstallReceipt.UserOpSuccess)
            {
                StatusMessage = uninstallReceipt.FailureDiagnostic;
                return;
            }

            var operatorService = new EnterpriseAccountOperatorService(_session.Web3!, _session.Client!, _session.Deployment!, ownerAccount, _session.Sponsor);
            var sessionKey = EthECKey.GenerateKey();
            var depositSelector = new DepositFunction().GetCallData();
            var spec = new CappedRoleSpec(
                sessionKey.GetPublicAddress(),
                _session.PayableTargetAddress!,
                depositSelector,
                _session.ValueCapCombinatorAddress!,
                _session.CapRuleId!,
                NewCap,
                RandomSessionSalt());

            var installResult = await operatorService.InstallCappedRoleAsync(spec).ConfigureAwait(false);
            if (!installResult.Receipt.UserOpSuccess)
            {
                StatusMessage = installResult.Receipt.FailureDiagnostic;
                return;
            }

            _session.RecordCappedRole(user.UserId, new EnrolledCappedRole(user.UserId, sessionKey, installResult.PermissionId, NewCap));
            var previousSessionKeyAddress = SessionKeyAddress;
            SessionKeyAddress = sessionKey.GetPublicAddress();
            PermissionIdHex = installResult.PermissionId.ToHex(true);
            Cap = NewCap;
            OverCapRejected = null;
            LastReceipt = installResult.Receipt;

            StatusMessage = $"Cap edited for '{user.UserId}' - the role was RE-ISSUED at a new cap of {NewCap} wei " +
                             $"(was {previousRole.Cap} wei): the old session key {previousSessionKeyAddress} is dead, a new " +
                             $"session key {SessionKeyAddress} now holds permissionId {PermissionIdHex}.";
        });

        [RelayCommand]
        private Task RevokeRoleAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            if (!HasCappedRole)
                throw new InvalidOperationException($"'{user.UserId}' has no installed capped role to revoke.");
            var role = _session.RequireCappedRole(user.UserId);

            var ownerAccount = await _session.Client!.CreateAccountAsync(user.OwnerKey, ToSaltBytes(user.Salt)).ConfigureAwait(false);

            var smartSessionService = new SmartSessionService(_session.Web3!, _session.Deployment!.Modules.SmartSession);
            var revokeHandler = _session.Client!.Configure(smartSessionService, ownerAccount);
            _session.ApplySponsor(revokeHandler);

            var revokeReceipt = (AATransactionReceipt)await smartSessionService.RemoveSessionRequestAndWaitForReceiptAsync(
                role.PermissionId).ConfigureAwait(false);
            if (!revokeReceipt.UserOpSuccess)
            {
                StatusMessage = revokeReceipt.FailureDiagnostic;
                return;
            }

            var operatorAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new SmartSessionKeySigningService(role.SessionKey, role.PermissionId),
                new SmartSessionValidatorModule(_session.Deployment!.Modules.SmartSession, role.PermissionId));
            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var revokedSessionHandler = _session.Client!.Configure(payableTarget, operatorAccount);
            _session.ApplySponsor(revokedSessionHandler);

            var balanceBefore = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            try
            {
                await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = WithinCapAmount }).ConfigureAwait(false);
                StatusMessage = "UNEXPECTED: the revoked session key still executed a deposit - the role was not severed.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsRevokedSessionRejection(ex))
                    throw;
            }

            var balanceAfterRevoke = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);

            var accountService = new NethereumAccountService(_session.Web3!, user.AccountAddress);
            var uninstallHandler = _session.Client!.Configure(accountService, ownerAccount);
            _session.ApplySponsor(uninstallHandler);

            var uninstallReceipt = (AATransactionReceipt)await accountService.UninstallModuleRequestAndWaitForReceiptAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _session.Deployment!.Modules.SmartSession, Array.Empty<byte>()).ConfigureAwait(false);
            if (!uninstallReceipt.UserOpSuccess)
            {
                StatusMessage = uninstallReceipt.FailureDiagnostic;
                return;
            }

            LastReceipt = uninstallReceipt;
            OverCapRejected = null;
            HasCappedRole = false;

            StatusMessage = $"Role revoked for '{user.UserId}' - the session key {SessionKeyAddress} that was live is now " +
                             $"REJECTED (InvalidPermissionId {InvalidPermissionIdSelector}); the SmartSession module is now " +
                             $"uninstalled; PayableTarget's balance stayed at {balanceAfterRevoke.Value} wei " +
                             $"({(balanceBefore.Value == balanceAfterRevoke.Value ? "unchanged" : "CHANGED")}).";
        });

        private static readonly string PolicyViolationSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);

        private static readonly string InvalidPermissionIdSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("InvalidPermissionId(bytes32)"))[..4].ToHex(true);

        private static readonly string ModuleAlreadyInstalledSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("ModuleAlreadyInstalled(address)"))[..4].ToHex(true);

        private static bool IsCapPolicyRejection(Exception ex)
        {
            var text = FlattenMessages(ex);
            return text.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(PolicyViolationSelector, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRevokedSessionRejection(Exception ex)
        {
            var text = FlattenMessages(ex);
            return text.IndexOf("AA23", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf(InvalidPermissionIdSelector, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private PayableTargetService BuildOperatorPayableTarget()
        {
            var user = _session.RequireActiveUser();
            var role = _session.RequireCappedRole(user.UserId);

            var operatorAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new SmartSessionKeySigningService(role.SessionKey, role.PermissionId),
                new SmartSessionValidatorModule(_session.Deployment!.Modules.SmartSession, role.PermissionId));

            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var handler = _session.Client!.Configure(payableTarget, operatorAccount);
            _session.ApplySponsor(handler);
            return payableTarget;
        }

        private static byte[] ToSaltBytes(BigInteger salt) =>
            salt.ToByteArray(isUnsigned: true, isBigEndian: true).PadBytesLeft(32);

        private static byte[] RandomSessionSalt()
        {
            var salt = new byte[32];
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 0, 16);
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 16, 16);
            return salt;
        }
    }
}
