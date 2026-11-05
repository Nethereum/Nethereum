using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Dns;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class DnsEnrTreeResolverDepthSecurityTests
    {
        private static Func<string, TimeSpan, CancellationToken, Task<List<string>>> SynchronousResolver(SignedEnrTree tree)
            => (name, _, _) =>
            {
                var text = tree.Lookup(name);
                if (text == null) throw new InvalidOperationException("NXDOMAIN " + name);
                return Task.FromResult(new List<string> { text });
            };

        private static string ChainOfBranches(SignedEnrTree tree, string bottom, int length)
        {
            var label = bottom;
            for (var i = 0; i < length; i++)
                label = tree.AddBranch(label);
            return label;
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SignedTreeNestedTwentyThousandBranchesDeep_When_ResolvedWithASynchronousResolver_Then_ResolutionEndsWithoutOverflowingTheStack()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            tree.SetRoot(ChainOfBranches(tree, tree.AddBranch(leaf), 20000), tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SynchronousResolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 100, CancellationToken.None);

            Assert.Empty(enrs);
            Assert.True(tree.Queries <= EnrTreeResolver.MaxTreeDepth + 4,
                $"{tree.Queries} DNS queries were issued for a chain deeper than the {EnrTreeResolver.MaxTreeDepth}-level bound");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SignedTreeWithALeafTenBranchesDeep_When_Resolved_Then_TheLeafIsReturned()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            tree.SetRoot(ChainOfBranches(tree, tree.AddBranch(leaf), 10), tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SynchronousResolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 100, CancellationToken.None);

            Assert.Single(enrs);
        }
    }
}
