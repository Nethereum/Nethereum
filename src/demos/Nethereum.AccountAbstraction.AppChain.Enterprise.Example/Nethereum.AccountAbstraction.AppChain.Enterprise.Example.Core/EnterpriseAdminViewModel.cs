using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public partial class EnterpriseAdminViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private string? _userId;

        [ObservableProperty]
        private string? _resolvedAddress;

        [ObservableProperty]
        private bool? _isActive;

        [ObservableProperty]
        private decimal _initialFundingEth = 1m;

        public ObservableCollection<EnrolledUserSummary> Directory { get; } = new();

        public string? ActiveUserId => _session.ActiveUserId;

        public EnterpriseAdminViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.PropertyChanged += OnSessionPropertyChanged;
        }

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SessionState.ActiveUserId)) return;
            OnPropertyChanged(nameof(ActiveUserId));
            RefreshDirectory();
        }

        [RelayCommand]
        private Task EnrollAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            if (string.IsNullOrWhiteSpace(UserId))
                throw new InvalidOperationException("Enter a userId to create.");
            var userId = UserId!;
            if (_session.TryGetEnrolledUser(userId, out _))
                throw new InvalidOperationException($"userId '{userId}' is already enrolled.");

            var ownerKey = EthECKey.GenerateKey();
            var salt = GenerateSalt();
            _session.RegisterOwnerKey(userId, ownerKey);

            var directoryAdmin = new EnterpriseDirectoryAdminService(_session.AdminService!, _session.ResolveOwnerAddress);
            var address = await directoryAdmin.EnrollAsync(userId, salt).ConfigureAwait(false);
            var isActive = await directoryAdmin.IsActiveAsync(userId).ConfigureAwait(false);

            var enrolled = _session.RecordEnrolled(userId, address, salt, isActive);
            ResolvedAddress = enrolled.AccountAddress;
            IsActive = enrolled.IsActive;
            RefreshDirectory();

            if (InitialFundingEth > 0)
                await _session.Web3!.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync(enrolled.AccountAddress, InitialFundingEth).ConfigureAwait(false);

            _session.SelectActiveUser(userId);

            StatusMessage = $"Created '{userId}' - account {ResolvedAddress} owned by {ownerKey.GetPublicAddress()} is " +
                             $"{(IsActive == true ? "Active" : "not yet Active")} on the registry, funded with {InitialFundingEth} ETH of " +
                             $"spendable balance (gas is org-sponsored by the paymaster). '{userId}' is now the active user.";

            UserId = null;
        });

        [RelayCommand]
        private Task SelectUserAsync(string userId) => RunAsync(() =>
        {
            _session.SelectActiveUser(userId);
            StatusMessage = $"'{userId}' is now the active user for the User / Tiered approval / Offboard tabs.";
            return Task.CompletedTask;
        });

        private void RefreshDirectory()
        {
            Directory.Clear();
            foreach (var user in _session.EnrolledUsers.Values)
                Directory.Add(new EnrolledUserSummary(
                    user.UserId, user.OwnerAddress, user.AccountAddress, user.IsActive, user.UserId == _session.ActiveUserId));
        }

        private static BigInteger GenerateSalt()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }
    }
}
