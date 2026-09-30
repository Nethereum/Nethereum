using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry.ContractDefinition;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class CustomViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private string? _guestOfLastQuery;

        [ObservableProperty]
        private SlotAlreadyBookedError? _slotAlreadyBooked;

        public CustomViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task BookAsync(BigInteger slot) => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var registry = _session.Booking!;
            var handler = _session.Client!.Configure(registry, account);
            _session.ApplyPaymentMode(handler);
            SlotAlreadyBooked = null;

            try
            {
                var receipt = (AATransactionReceipt)await registry.BookRequestAndWaitForReceiptAsync(slot).ConfigureAwait(false);
                LastReceipt = receipt;
                StatusMessage = receipt.UserOpSuccess
                    ? $"Booked slot {slot} for {account.Address} (userOpHash {receipt.UserOpHash})"
                    : receipt.FailureDiagnostic;
            }
            catch (SmartContractCustomErrorRevertException ex)
                when (ex.ExceptionEncodedData.IsExceptionEncodedDataForError<SlotAlreadyBookedError>())
            {
                SlotAlreadyBooked = ex.ExceptionEncodedData.DecodeExceptionEncodedData<SlotAlreadyBookedError>();
                throw;
            }
        });

        [RelayCommand]
        private Task ReleaseAsync(BigInteger slot) => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var registry = _session.Booking!;
            var handler = _session.Client!.Configure(registry, account);
            _session.ApplyPaymentMode(handler);

            var receipt = (AATransactionReceipt)await registry.ReleaseRequestAndWaitForReceiptAsync(slot).ConfigureAwait(false);
            LastReceipt = receipt;
            StatusMessage = receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task GuestOfAsync(BigInteger slot) => RunAsync(async () =>
        {
            _session.RequireReady();
            GuestOfLastQuery = await _session.Booking!.GuestOfQueryAsync(slot).ConfigureAwait(false);
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");
    }
}
