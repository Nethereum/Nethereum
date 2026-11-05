using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.SendTransaction;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;
using Xunit;

namespace Nethereum.Wallet.UI.Components.UnitTests
{
    public class WalletSendNativeBalanceTests
    {
        private const string Recipient = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task Given_TheSendScreenOpensWithANativeBalance_When_AnAmountBelowItIsEntered_Then_TheFormIsValidWithoutReselecting()
        {
            using var harness = await WalletDevChainHarness.CreateAsync();
            var viewModel = harness.Services.GetRequiredService<SendNativeTokenViewModel>();

            await viewModel.InitializeAsync();
            viewModel.NativeTransfer.RecipientAddress = Recipient;
            viewModel.NativeTransfer.Amount = "1";

            Assert.Equal(WalletDevChainHarness.FirstBalance, viewModel.NativeTransfer.AvailableBalance);
            Assert.True(viewModel.NativeTransfer.IsValid);
            Assert.True(viewModel.IsStep1Valid);
            Assert.False(viewModel.NativeTransfer.HasFieldErrors(nameof(TokenNativeTransferModel.Amount)));
        }

        [Fact]
        public async Task Given_ANativeAmountWasEntered_When_TheAccountOrChainChanges_Then_TheAvailableBalanceIsTheNewBalance()
        {
            using var harness = await WalletDevChainHarness.CreateAsync();
            var viewModel = harness.Services.GetRequiredService<SendNativeTokenViewModel>();
            await viewModel.InitializeAsync();
            await viewModel.SelectTokenAsync(viewModel.AvailableTokens.Single(t => t.IsNative));
            viewModel.NativeTransfer.RecipientAddress = Recipient;
            viewModel.NativeTransfer.Amount = "1";
            Assert.Equal(WalletDevChainHarness.FirstBalance, viewModel.NativeTransfer.AvailableBalance);

            viewModel.SelectedAccount = viewModel.AvailableAccounts.Single(a =>
                a.Address.Equals(harness.SecondAddress, StringComparison.OrdinalIgnoreCase));
            await WaitUntilAsync(() => viewModel.NativeTransfer.AvailableBalance == WalletDevChainHarness.SecondBalance);

            Assert.Equal(WalletDevChainHarness.SecondBalance, viewModel.NativeTransfer.AvailableBalance);
            Assert.Equal("1", viewModel.NativeTransfer.Amount);
            Assert.True(viewModel.NativeTransfer.IsValid);
        }

        [Fact]
        public async Task Given_AnAmountEnteredBeforeTheBalanceLoaded_When_TheBalanceArrives_Then_IsValidIsRaisedAndTrue()
        {
            using var harness = await WalletDevChainHarness.CreateAsync();
            var model = harness.Services.GetRequiredService<TokenNativeTransferModel>();
            model.RecipientAddress = Recipient;
            model.Amount = "1";
            Assert.False(model.IsValid);

            var raised = new List<string?>();
            model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            model.AvailableBalance = WalletDevChainHarness.FirstBalance;

            Assert.Contains(nameof(TokenNativeTransferModel.IsValid), raised);
            Assert.True(model.IsValid);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }
        }
    }
}
