using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class Eip7702AccountViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public Eip7702AccountViewModelTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private Eip7702AccountViewModel NewViewModel(out SessionState session)
        {
            session = _fixture.NewReadySession();
            return new Eip7702AccountViewModel(session);
        }

        [Fact]
        [Trait("UseCase", "Eip7702")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#eip7702-generate-eoa")]
        public async Task Given_the_tab_When_the_user_generates_an_eoa_Then_a_plain_codeless_address_is_shown()
        {
            var vm = NewViewModel(out _);

            await vm.GenerateEoaCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(vm.EoaAddress));
            Assert.False(vm.IsUpgraded);

            var code = await _fixture.Bootstrap.Node.GetCodeAsync(vm.EoaAddress!);
            Assert.True(code == null || code.Length == 0);
        }

        [Fact]
        [Trait("UseCase", "Eip7702")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#eip7702-fund-eoa")]
        public async Task Given_a_generated_eoa_When_the_user_funds_it_Then_the_faucet_reports_a_positive_balance()
        {
            var vm = NewViewModel(out _);
            await vm.GenerateEoaCommand.ExecuteAsync(null);

            var balanceBefore = await _fixture.Faucet.GetBalanceAsync(vm.EoaAddress!);
            Assert.Equal(BigInteger.Zero, balanceBefore);

            await vm.FundEoaCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            var balanceAfter = await _fixture.Faucet.GetBalanceAsync(vm.EoaAddress!);
            Assert.True(balanceAfter > BigInteger.Zero);
        }

        [Fact]
        [Trait("UseCase", "Eip7702")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#eip7702-upgrade-and-send")]
        public async Task Given_a_funded_eoa_When_the_user_upgrades_and_sends_Then_it_delegates_in_place_and_the_call_succeeds()
        {
            var vm = NewViewModel(out var session);
            await vm.GenerateEoaCommand.ExecuteAsync(null);
            var eoaAddress = vm.EoaAddress!;
            await vm.FundEoaCommand.ExecuteAsync(null);

            var codeBefore = await _fixture.Bootstrap.Node.GetCodeAsync(eoaAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "must start as a plain, code-less EOA");

            await vm.UpgradeAndSendCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsUpgraded);
            Assert.Equal(BigInteger.One, vm.Count);

            var codeAfter = await _fixture.Bootstrap.Node.GetCodeAsync(eoaAddress);
            Assert.NotNull(codeAfter);
            Assert.True(codeAfter!.Length > 0, "the EOA should carry EIP-7702 delegation code after the upgrade");
            Assert.Equal(eoaAddress.ToLowerInvariant(), vm.LastReceipt.Sender?.ToLowerInvariant());

            Assert.NotNull(session.Account);
            Assert.Equal(eoaAddress, session.Account!.Address);
            Assert.True(session.Account.IsDeployed);
            Assert.Equal("EOA · 7702-upgraded (secp256k1)", session.AccountDescription);
        }
    }
}
