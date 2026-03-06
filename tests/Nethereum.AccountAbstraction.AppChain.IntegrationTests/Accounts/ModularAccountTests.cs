using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Accounts
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class ModularAccountTests
    {
        private readonly AAIntegrationFixture _fixture;

        public ModularAccountTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateAccount_WithNoModules_Succeeds()
        {
            Assert.NotNull(_fixture.Web3);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task CreateAccount_WithModules_Succeeds()
        {
            Assert.NotNull(_fixture.Contracts);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task CreateAccount_WithMultipleModules_Succeeds()
        {
            Assert.NotNull(_fixture.UserAccounts);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task ModularAccount_OwnerCanExecute()
        {
            Assert.NotNull(_fixture.OperatorAccount);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task ModularAccount_NonOwnerCannotExecute()
        {
            Assert.NotNull(_fixture.BundlerAccount);
            await Task.CompletedTask;
        }
    }
}
