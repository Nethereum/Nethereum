using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Dns;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Dns
{
    public class EnrTreeResolverTests
    {
        [Fact]
        public async Task ResolveEnrsAsync_NonExistentDomain_ReturnsEmptyList()
        {
            var resolver = new EnrTreeResolver(_ => { });
            const string url =
                "enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE@invalid.test.example.invalid";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await resolver.ResolveEnrsAsync(
                url, TimeSpan.FromMilliseconds(500), maxLeaves: 10, cts.Token);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ResolveEnrsAsync_Cancelled_ReturnsEmptyListWithoutThrowing()
        {
            var resolver = new EnrTreeResolver(_ => { });
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await resolver.ResolveEnrsAsync(
                EnrTreeResolver.MainnetEnrTree, TimeSpan.FromMilliseconds(100),
                maxLeaves: 10, cts.Token);

            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
