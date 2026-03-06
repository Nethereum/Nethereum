using System.Threading.Tasks;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.E2E.Fixtures
{
    public class SponsoredGasAAFixture : IAsyncLifetime
    {
        protected object? EntryPointService { get; set; }
        protected object? SimpleAccountFactory { get; set; }
        protected object? Paymaster { get; set; }

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
