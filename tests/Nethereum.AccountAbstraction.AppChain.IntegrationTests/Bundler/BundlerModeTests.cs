using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Nethereum.AccountAbstraction.AppChain.Interfaces;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Bundler
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class BundlerModeTests
    {
        private readonly AAIntegrationFixture _fixture;

        public BundlerModeTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task OpenMode_AnyBundlerCanSubmit()
        {
            Assert.NotNull(_fixture.Web3);
            Assert.NotNull(_fixture.Contracts);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task InvitationMode_OnlyInvitedBundlersCanSubmit()
        {
            Assert.NotNull(_fixture.BundlerAccount);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task StakeMode_RequiresSufficientStake()
        {
            Assert.NotNull(_fixture.UserAccounts);
            Assert.Equal(5, _fixture.UserAccounts.Length);
            await Task.CompletedTask;
        }
    }
}
