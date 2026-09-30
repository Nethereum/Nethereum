using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public partial class OffboardViewModel : TabViewModel
    {
        private const int GuardianCount = 3;
        private const int GuardianThreshold = 2;

        private readonly SessionState _session;

        [ObservableProperty]
        private BigInteger _cap = 100;

        [ObservableProperty]
        private BigInteger _withinCapAmount = 60;

        [ObservableProperty]
        private bool _isSetUp;

        [ObservableProperty]
        private string? _sessionKeyAddress;

        [ObservableProperty]
        private string? _permissionIdHex;

        public ObservableCollection<string> GuardianAddresses { get; } = new();

        [ObservableProperty]
        private bool _isRotated;

        [ObservableProperty]
        private string? _newOwnerAddress;

        [ObservableProperty]
        private bool? _oldOwnerRejected;

        [ObservableProperty]
        private bool _isSessionRevoked;

        [ObservableProperty]
        private bool? _revokedSessionRejected;

        [ObservableProperty]
        private bool _isModuleUninstalled;

        [ObservableProperty]
        private bool _isSwept;

        [ObservableProperty]
        private string? _treasuryAddress;

        [ObservableProperty]
        private BigInteger _sweptAmount;

        [ObservableProperty]
        private bool _isBanned;

        [ObservableProperty]
        private BigInteger _targetBalance;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        public OffboardViewModel(SessionState session)
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

            IsSetUp = false;
            SessionKeyAddress = null;
            PermissionIdHex = null;
            GuardianAddresses.Clear();
            IsRotated = false;
            NewOwnerAddress = null;
            OldOwnerRejected = null;
            IsSessionRevoked = false;
            RevokedSessionRejected = null;
            IsModuleUninstalled = false;
            IsSwept = false;
            TreasuryAddress = null;
            SweptAmount = 0;
            IsBanned = false;
            TargetBalance = 0;
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
        private Task SetUpAccountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            if (_session.TryGetOffboardProgress(user.UserId, out _))
                throw new InvalidOperationException($"Offboard is already set up for '{user.UserId}' - each userId offboards once per session.");

            var ownerAccount = await _session.Client!.CreateAccountAsync(user.OwnerKey, ToSaltBytes(user.Salt)).ConfigureAwait(false);
            var operatorService = new EnterpriseAccountOperatorService(_session.Web3!, _session.Client!, _session.Deployment!, ownerAccount, _session.Sponsor);

            var sessionKey = EthECKey.GenerateKey();
            var depositSelector = new DepositFunction().GetCallData();
            var cappedRoleSpec = new CappedRoleSpec(
                sessionKey.GetPublicAddress(),
                _session.PayableTargetAddress!,
                depositSelector,
                _session.ValueCapCombinatorAddress!,
                _session.CapRuleId!,
                Cap,
                RandomSalt32());

            var installResult = await operatorService.InstallCappedRoleAsync(cappedRoleSpec).ConfigureAwait(false);
            if (!installResult.Receipt.UserOpSuccess)
            {
                StatusMessage = IsModuleAlreadyInstalledRejection(installResult.Receipt)
                    ? $"'{user.UserId}' already has a capability installed by another tab in this session - " +
                      "the SmartSession module installs once per account. Use a different user for this tab."
                    : installResult.Receipt.FailureDiagnostic;
                return;
            }

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var guardianReceipt = (AATransactionReceipt)await operatorService.InstallGuardiansAsync(
                _session.Deployment!.Modules.SocialRecovery,
                GuardianThreshold,
                guardianKeys.Select(k => k.GetPublicAddress()).ToList()).ConfigureAwait(false);
            if (!guardianReceipt.UserOpSuccess)
            {
                StatusMessage = guardianReceipt.FailureDiagnostic;
                return;
            }

            var progress = new OffboardProgress(user.UserId, sessionKey, installResult.PermissionId, Cap, guardianKeys, GuardianThreshold);
            _session.RecordOffboardSetup(user.UserId, progress);

            var operatorAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new SmartSessionKeySigningService(sessionKey, installResult.PermissionId),
                new SmartSessionValidatorModule(_session.Deployment!.Modules.SmartSession, installResult.PermissionId));
            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var baselineHandler = _session.Client!.Configure(payableTarget, operatorAccount);
            _session.ApplySponsor(baselineHandler);

            var baselineReceipt = (AATransactionReceipt)await payableTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = WithinCapAmount }).ConfigureAwait(false);
            if (!baselineReceipt.UserOpSuccess)
            {
                StatusMessage = baselineReceipt.FailureDiagnostic;
                return;
            }

            var balance = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balance.Value;
            LastReceipt = baselineReceipt;
            SessionKeyAddress = sessionKey.GetPublicAddress();
            PermissionIdHex = installResult.PermissionId.ToHex(true);

            GuardianAddresses.Clear();
            foreach (var address in MultiGuardianSignatureBlobBuilder.SortAddressesAscending(guardianKeys.Select(k => k.GetPublicAddress())))
                GuardianAddresses.Add(address);

            IsSetUp = true;
            StatusMessage = $"Offboard set up for '{user.UserId}' - a tier-1 capped session ({SessionKeyAddress}, cap {Cap}) and a " +
                             $"{GuardianThreshold}-of-{GuardianCount} guardian quorum are live; the baseline deposit of {WithinCapAmount} wei " +
                             $"succeeded (PayableTarget balance {TargetBalance} wei), proving the session is LIVE before offboarding begins.";
        });

        [RelayCommand]
        private Task RotateOwnerAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = _session.RequireOffboardProgress(user.UserId);
            if (progress.IsRotated)
                throw new InvalidOperationException($"'{user.UserId}' has already had its owner rotated.");

            var newOwner = EthECKey.GenerateKey();
            var recoveryAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new MultiGuardianSigningService(progress.GuardianKeys, progress.GuardianThreshold),
                new SocialRecoveryValidatorModule(_session.Deployment!.Modules.SocialRecovery, progress.GuardianThreshold));
            var recoveryEcdsa = new ECDSAValidatorService(_session.Web3!, _session.Deployment!.Modules.EcdsaValidator);
            var recoveryHandler = _session.Client!.Configure(recoveryEcdsa, recoveryAccount);
            _session.ApplySponsor(recoveryHandler);

            var rotateReceipt = (AATransactionReceipt)await recoveryEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(
                newOwner.GetPublicAddress()).ConfigureAwait(false);
            if (!rotateReceipt.UserOpSuccess)
            {
                StatusMessage = rotateReceipt.FailureDiagnostic;
                return;
            }

            var oldOwnerAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new AccountSigningOfflineService(user.OwnerKey),
                new EcdsaValidatorModule(_session.Deployment!.Modules.EcdsaValidator));
            var oldOwnerEcdsa = new ECDSAValidatorService(_session.Web3!, _session.Deployment!.Modules.EcdsaValidator);
            var oldOwnerHandler = _session.Client!.Configure(oldOwnerEcdsa, oldOwnerAccount);
            _session.ApplySponsor(oldOwnerHandler);

            try
            {
                await oldOwnerEcdsa.TransferOwnershipRequestAndWaitForReceiptAsync(
                    EthECKey.GenerateKey().GetPublicAddress()).ConfigureAwait(false);
                OldOwnerRejected = false;
                StatusMessage = "UNEXPECTED: the OLD owner key was still able to act after rotation - it was not severed.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsOldOwnerDeadRejection(ex))
                    throw;
                OldOwnerRejected = true;
            }

            progress.RecordRotated(newOwner);
            NewOwnerAddress = newOwner.GetPublicAddress();
            LastReceipt = rotateReceipt;
            IsRotated = true;

            StatusMessage = $"Owner rotated for '{user.UserId}' to {NewOwnerAddress} via a {progress.GuardianThreshold}-of-{progress.GuardianKeys.Count} " +
                             $"guardian quorum - the OLD owner ({user.OwnerAddress}) was confirmed dead (AA24) on the account it used to control.";
        });

        [RelayCommand]
        private Task RevokeSessionAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = _session.RequireOffboardProgress(user.UserId);
            if (!progress.IsRotated)
                throw new InvalidOperationException($"Rotate the owner for '{user.UserId}' before revoking the session.");
            if (progress.IsSessionRevoked)
                throw new InvalidOperationException($"The session for '{user.UserId}' has already been revoked.");

            var newOwnerAccount = BuildNewOwnerAccount(user, progress);
            var smartSessionService = new SmartSessionService(_session.Web3!, _session.Deployment!.Modules.SmartSession);
            var revokeHandler = _session.Client!.Configure(smartSessionService, newOwnerAccount);
            _session.ApplySponsor(revokeHandler);

            var revokeReceipt = (AATransactionReceipt)await smartSessionService.RemoveSessionRequestAndWaitForReceiptAsync(
                progress.PermissionId).ConfigureAwait(false);
            if (!revokeReceipt.UserOpSuccess)
            {
                StatusMessage = revokeReceipt.FailureDiagnostic;
                return;
            }

            var operatorAccount = _session.Client!.GetAccount(
                user.AccountAddress,
                new SmartSessionKeySigningService(progress.SessionKey, progress.PermissionId),
                new SmartSessionValidatorModule(_session.Deployment!.Modules.SmartSession, progress.PermissionId));
            var payableTarget = new PayableTargetService(_session.Web3!, _session.PayableTargetAddress!);
            var revokedSessionHandler = _session.Client!.Configure(payableTarget, operatorAccount);
            _session.ApplySponsor(revokedSessionHandler);

            var balanceBefore = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            try
            {
                await payableTarget.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = WithinCapAmount }).ConfigureAwait(false);
                RevokedSessionRejected = false;
                StatusMessage = "UNEXPECTED: the revoked session key still executed a deposit - the session was not severed.";
                return;
            }
            catch (Exception ex)
            {
                if (!IsRevokedSessionRejection(ex))
                    throw;
                RevokedSessionRejected = true;
            }

            var balanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(_session.PayableTargetAddress!).ConfigureAwait(false);
            TargetBalance = balanceAfter.Value;
            LastReceipt = revokeReceipt;
            progress.RecordSessionRevoked();
            IsSessionRevoked = true;

            StatusMessage = $"Session revoked for '{user.UserId}' - the session key {progress.SessionKeyAddress} that was live at Setup is " +
                             $"now REJECTED (InvalidPermissionId {InvalidPermissionIdSelector}); PayableTarget's balance stayed at {TargetBalance} wei " +
                             $"({(balanceBefore.Value == balanceAfter.Value ? "unchanged" : "CHANGED")}).";
        });

        [RelayCommand]
        private Task UninstallModuleAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = _session.RequireOffboardProgress(user.UserId);
            if (!progress.IsSessionRevoked)
                throw new InvalidOperationException($"Revoke the session for '{user.UserId}' before uninstalling the module.");
            if (progress.IsModuleUninstalled)
                throw new InvalidOperationException($"The SmartSession module for '{user.UserId}' has already been uninstalled.");

            var newOwnerAccount = BuildNewOwnerAccount(user, progress);
            var accountService = new NethereumAccountService(_session.Web3!, user.AccountAddress);
            var uninstallHandler = _session.Client!.Configure(accountService, newOwnerAccount);
            _session.ApplySponsor(uninstallHandler);

            var uninstallReceipt = (AATransactionReceipt)await accountService.UninstallModuleRequestAndWaitForReceiptAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _session.Deployment!.Modules.SmartSession, Array.Empty<byte>()).ConfigureAwait(false);
            if (!uninstallReceipt.UserOpSuccess)
            {
                StatusMessage = uninstallReceipt.FailureDiagnostic;
                return;
            }

            var queryService = new NethereumAccountService(_session.Web3!, user.AccountAddress);
            var stillInstalled = await queryService.IsModuleInstalledQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR, _session.Deployment!.Modules.SmartSession, Array.Empty<byte>()).ConfigureAwait(false);
            if (stillInstalled)
            {
                StatusMessage = "UNEXPECTED: the SmartSession module still reports installed after uninstall.";
                return;
            }

            LastReceipt = uninstallReceipt;
            progress.RecordModuleUninstalled();
            IsModuleUninstalled = true;
            StatusMessage = $"SmartSession module uninstalled for '{user.UserId}' - IsModuleInstalled is now false.";
        });

        [RelayCommand]
        private Task SweepAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = _session.RequireOffboardProgress(user.UserId);
            if (!progress.IsModuleUninstalled)
                throw new InvalidOperationException($"Uninstall the SmartSession module for '{user.UserId}' before sweeping.");
            if (progress.IsSwept)
                throw new InvalidOperationException($"'{user.UserId}' has already been swept.");

            var newOwnerAccount = BuildNewOwnerAccount(user, progress);
            var accountService = new NethereumAccountService(_session.Web3!, user.AccountAddress);
            var sweepHandler = _session.Client!.Configure(accountService, newOwnerAccount);
            _session.ApplySponsor(sweepHandler);

            var treasury = EthECKey.GenerateKey().GetPublicAddress();
            var treasuryBalanceBefore = await _session.Web3!.Eth.GetBalance.SendRequestAsync(treasury).ConfigureAwait(false);

            var gasReserve = Nethereum.Web3.Web3.Convert.ToWei(0.1m);
            var accountBalance = await _session.Web3!.Eth.GetBalance.SendRequestAsync(user.AccountAddress).ConfigureAwait(false);
            if (accountBalance.Value <= gasReserve)
                throw new InvalidOperationException(
                    $"'{user.UserId}' account balance ({accountBalance.Value} wei) is too low to sweep past the {gasReserve} wei gas reserve.");
            var sweepAmount = accountBalance.Value - gasReserve;

            var sweepReceipt = (AATransactionReceipt)await accountService.ExecuteAsync(
                new Nethereum.AccountAbstraction.Structs.Call { Target = treasury, Value = sweepAmount, Data = Array.Empty<byte>() }).ConfigureAwait(false);
            if (!sweepReceipt.UserOpSuccess)
            {
                StatusMessage = sweepReceipt.FailureDiagnostic;
                return;
            }

            var treasuryBalanceAfter = await _session.Web3!.Eth.GetBalance.SendRequestAsync(treasury).ConfigureAwait(false);
            LastReceipt = sweepReceipt;
            TreasuryAddress = treasury;
            SweptAmount = treasuryBalanceAfter.Value - treasuryBalanceBefore.Value;
            progress.RecordSwept();
            IsSwept = true;

            StatusMessage = $"Swept {SweptAmount} wei from '{user.UserId}' to fresh treasury {TreasuryAddress} - treasury balance moved " +
                             $"from {treasuryBalanceBefore.Value} to {treasuryBalanceAfter.Value} wei.";
        });

        [RelayCommand]
        private Task BanAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var user = _session.RequireActiveUser();
            var progress = _session.RequireOffboardProgress(user.UserId);
            if (!progress.IsSwept)
                throw new InvalidOperationException($"Sweep '{user.UserId}''s balance before banning the account.");
            if (progress.IsBanned)
                throw new InvalidOperationException($"'{user.UserId}' is already banned.");

            await _session.AdminService!.BanUserAsync(user.AccountAddress, "offboarded").ConfigureAwait(false);
            var isActive = await _session.AdminService!.IsActiveAsync(user.AccountAddress).ConfigureAwait(false);
            if (isActive)
            {
                StatusMessage = "UNEXPECTED: the account is still Active on the registry after the ban.";
                return;
            }

            progress.RecordBanned();
            IsBanned = true;
            StatusMessage = $"'{user.UserId}' banned on the registry - IsActive is now false. Offboard complete.";
        });

        private NethereumSmartAccount BuildNewOwnerAccount(EnrolledUser user, OffboardProgress progress) =>
            _session.Client!.GetAccount(
                user.AccountAddress,
                new AccountSigningOfflineService(progress.RequireNewOwnerKey()),
                new EcdsaValidatorModule(_session.Deployment!.Modules.EcdsaValidator));

        private static byte[] ToSaltBytes(BigInteger salt) =>
            salt.ToByteArray(isUnsigned: true, isBigEndian: true).PadBytesLeft(32);

        private static byte[] RandomSalt32()
        {
            var salt = new byte[32];
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 0, 16);
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, salt, 16, 16);
            return salt;
        }

        private static readonly string InvalidPermissionIdSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("InvalidPermissionId(bytes32)"))[..4].ToHex(true);

        private static readonly string ModuleAlreadyInstalledSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("ModuleAlreadyInstalled(address)"))[..4].ToHex(true);

        private static bool IsOldOwnerDeadRejection(Exception ex) =>
            FlattenMessages(ex).IndexOf("AA24", StringComparison.OrdinalIgnoreCase) >= 0;

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
    }
}
