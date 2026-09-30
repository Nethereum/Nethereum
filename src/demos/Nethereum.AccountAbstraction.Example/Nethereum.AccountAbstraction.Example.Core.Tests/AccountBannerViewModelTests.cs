using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    public class AccountBannerViewModelTests
    {
        private static NethereumSmartAccount NewAccount(bool isDeployed = false) =>
            new NethereumSmartAccount(
                "0x1111111111111111111111111111111111111111",
                new AccountSigningOfflineService(EthECKey.GenerateKey()),
                new EcdsaValidatorModule("0x2222222222222222222222222222222222222222"),
                isDeployed);

        [Fact]
        [Trait("UseCase", "AccountBanner")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#account-banner")]
        public void Given_no_active_account_When_the_banner_is_created_Then_it_shows_the_empty_state()
        {
            var session = new SessionState();
            var banner = new AccountBannerViewModel(session);

            Assert.False(banner.HasAccount);
            Assert.Equal("No account yet - create one in Setup or Passkey", banner.DisplayText);
        }

        [Fact]
        [Trait("UseCase", "AccountBanner")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#account-banner")]
        public void Given_a_faucet_balance_When_an_account_becomes_active_Then_the_banner_composes_address_description_status_and_balance()
        {
            var faucet = new FakeDevChainFaucet { BalanceToReturn = Nethereum.Web3.Web3.Convert.ToWei(3m) };
            var session = new SessionState { Faucet = faucet };
            var banner = new AccountBannerViewModel(session);
            var account = NewAccount(isDeployed: false);

            session.RaiseAccountChanged(account, "Passkey · WebAuthn P-256");

            Assert.True(banner.HasAccount);
            Assert.True(faucet.BalanceQueryCount > 0);
            Assert.Equal(
                $"Account {account.Address} · Passkey · WebAuthn P-256 · Counterfactual · 3 ETH · Self-funded",
                banner.DisplayText);
        }

        [Fact]
        [Trait("UseCase", "AccountBanner")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#account-banner")]
        public void Given_an_active_account_When_its_balance_changes_Then_NotifyBalanceChanged_re_queries_the_faucet()
        {
            var faucet = new FakeDevChainFaucet { BalanceToReturn = BigInteger.Zero };
            var session = new SessionState { Faucet = faucet };
            var banner = new AccountBannerViewModel(session);
            var account = NewAccount(isDeployed: true);
            session.RaiseAccountChanged(account, "ECDSA owner (secp256k1)");
            var queryCountAfterCreate = faucet.BalanceQueryCount;
            Assert.True(queryCountAfterCreate > 0);
            Assert.Equal(BigInteger.Zero, banner.Balance);

            faucet.BalanceToReturn = Nethereum.Web3.Web3.Convert.ToWei(10m);
            session.NotifyBalanceChanged();

            Assert.True(faucet.BalanceQueryCount > queryCountAfterCreate);
            Assert.Equal(Nethereum.Web3.Web3.Convert.ToWei(10m), banner.Balance);
            Assert.Equal(
                $"Account {account.Address} · ECDSA owner (secp256k1) · Deployed · 10 ETH · Self-funded",
                banner.DisplayText);
        }

        private sealed class FakeDevChainFaucet : IDevChainFaucet
        {
            public BigInteger BalanceToReturn { get; set; }

            public int BalanceQueryCount { get; private set; }

            public Task FundAsync(string address, decimal ether = 10m) => Task.CompletedTask;

            public Task<BigInteger> GetBalanceAsync(string address)
            {
                BalanceQueryCount++;
                return Task.FromResult(BalanceToReturn);
            }
        }
    }
}
