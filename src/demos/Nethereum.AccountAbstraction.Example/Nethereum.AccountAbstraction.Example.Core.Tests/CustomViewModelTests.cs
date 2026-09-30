using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class CustomViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public CustomViewModelTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private SessionState NewSession() => _fixture.NewReadySession();

        private async Task<SessionState> NewFundedSessionAsync()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            await _fixture.FundAsync(session.Account!.Address);
            return session;
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#custom-contract-book")]
        public async Task Given_a_funded_account_When_it_books_a_free_slot_Then_the_booking_succeeds_and_guestOf_reports_the_account()
        {
            var session = await NewFundedSessionAsync();
            var customViewModel = new CustomViewModel(session);
            var slot = new BigInteger(1);

            await customViewModel.BookCommand.ExecuteAsync(slot);

            Assert.Null(customViewModel.ErrorMessage);
            Assert.NotNull(customViewModel.LastReceipt);
            Assert.True(customViewModel.LastReceipt!.UserOpSuccess, customViewModel.LastReceipt.FailureDiagnostic);
            Assert.Null(customViewModel.SlotAlreadyBooked);

            await customViewModel.GuestOfCommand.ExecuteAsync(slot);
            Assert.Equal(session.Account!.Address, customViewModel.GuestOfLastQuery, ignoreCase: true);

            var guestOfSlot = await _fixture.BookingRegistry.GuestOfQueryAsync(slot);
            Assert.Equal(session.Account.Address, guestOfSlot, ignoreCase: true);
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#custom-contract-custom-error")]
        public async Task Given_a_slot_already_booked_by_another_guest_When_a_second_guest_books_it_Then_the_typed_SlotAlreadyBooked_error_is_decoded()
        {
            var slot = new BigInteger(2);

            var firstGuestSession = await NewFundedSessionAsync();
            var firstGuestViewModel = new CustomViewModel(firstGuestSession);
            await firstGuestViewModel.BookCommand.ExecuteAsync(slot);
            Assert.True(firstGuestViewModel.LastReceipt!.UserOpSuccess, firstGuestViewModel.LastReceipt.FailureDiagnostic);

            var secondGuestSession = await NewFundedSessionAsync();
            var secondGuestViewModel = new CustomViewModel(secondGuestSession);

            await secondGuestViewModel.BookCommand.ExecuteAsync(slot);

            Assert.False(secondGuestViewModel.IsBusy);
            Assert.Null(secondGuestViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(secondGuestViewModel.ErrorMessage));

            Assert.NotNull(secondGuestViewModel.SlotAlreadyBooked);
            Assert.Equal(slot, secondGuestViewModel.SlotAlreadyBooked!.Slot);
            Assert.Equal(firstGuestSession.Account!.Address, secondGuestViewModel.SlotAlreadyBooked.Guest, ignoreCase: true);

            var guestOfSlot = await _fixture.BookingRegistry.GuestOfQueryAsync(slot);
            Assert.Equal(firstGuestSession.Account.Address, guestOfSlot, ignoreCase: true);
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#custom-contract-require-string")]
        public async Task Given_an_account_that_is_not_the_owner_When_it_releases_a_booked_slot_Then_the_require_reason_surfaces_and_the_slot_stays_booked()
        {
            var slot = new BigInteger(3);
            var session = await NewFundedSessionAsync();

            var bookingViewModel = new CustomViewModel(session);
            await bookingViewModel.BookCommand.ExecuteAsync(slot);
            Assert.True(bookingViewModel.LastReceipt!.UserOpSuccess, bookingViewModel.LastReceipt.FailureDiagnostic);

            var releaseViewModel = new CustomViewModel(session);
            await releaseViewModel.ReleaseCommand.ExecuteAsync(slot);

            Assert.False(releaseViewModel.IsBusy);
            Assert.Null(releaseViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(releaseViewModel.ErrorMessage));
            Assert.Contains("only owner can release", releaseViewModel.ErrorMessage);

            var guestOfSlot = await _fixture.BookingRegistry.GuestOfQueryAsync(slot);
            Assert.Equal(session.Account!.Address, guestOfSlot, ignoreCase: true);
        }
    }
}
