using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Dns;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class DnsEnrTreeResolverSecurityTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SignedTreeWithOneEnr_When_Resolved_Then_TheEnrIsReturned()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            tree.SetRoot(tree.AddBranch(leaf), tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, Timeout, 100, CancellationToken.None);

            Assert.Single(enrs);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_LinkedTreeThatLinksBack_When_Resolved_Then_ResolutionTerminatesWithBothTreesLeaves()
        {
            var a = new SignedEnrTree("a.example.org");
            var b = new SignedEnrTree("b.example.org");
            var leafA = a.AddEnr(EthECKey.GenerateKey(), "8.8.4.4");
            var leafB = b.AddEnr(EthECKey.GenerateKey(), "1.1.1.1");
            a.SetRoot(a.AddBranch(leafA), a.AddBranch(a.Add("enrtree://" + SignedEnrTree.Base32(b.Key.GetPubKey(true)) + "@" + b.Domain)));
            b.SetRoot(b.AddBranch(leafB), b.AddBranch(b.Add("enrtree://" + SignedEnrTree.Base32(a.Key.GetPubKey(true)) + "@" + a.Domain)));
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(a, b));

            var enrs = await resolver.ResolveEnrsAsync(a.Url, Timeout, 100, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(2, enrs.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_LinkEntryWithoutAtSign_When_ResolverWalksTheTree_Then_TheValidEnrsAreStillReturned()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var malformedLink = tree.Add("enrtree://AKA3AM6LPBYEUDMVNU3BSVQJ5AD45Y7YPOHJLEF6W26QOE4VTUDPE");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            tree.SetRoot(tree.AddBranch(malformedLink, leaf), tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            List<Nethereum.Model.Enr.EnrRecord> enrs = null;
            Exception failure = null;
            try { enrs = await resolver.ResolveEnrsAsync(tree.Url, Timeout, 100, CancellationToken.None); }
            catch (Exception ex) { failure = ex; }

            Assert.True(failure == null && enrs.Count == 1,
                $"one malformed link entry aborted the whole resolution: {failure?.GetType().Name}: {failure?.Message}");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_RootWithUnknownVersion_When_Resolved_Then_ItIsRejected()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            tree.SetRoot(tree.AddBranch(leaf), tree.AddBranch(), version: "v2");
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, Timeout, 100, CancellationToken.None);

            Assert.Empty(enrs);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_EntryReferencedByAOneByteHashPrefix_When_Resolved_Then_ItIsRejected()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var shortLabel = tree.Add("enrtree-branch:" + tree.AddEnr(EthECKey.GenerateKey()), hashBytes: 1);
            tree.SetRoot(shortLabel, tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, Timeout, 100, CancellationToken.None);

            Assert.True(enrs.Count == 0,
                "a 1-byte hash prefix authenticated a subtree (geth dnsdisc/tree.go:151 minHashLength=12 bytes; a 1-byte label can be forged with about 256 tries)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SignedTreeWithAVeryLongChainOfEmptyBranches_When_ResolvedWithSmallLeafBudget_Then_DnsWorkIsBounded()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            var label = tree.AddBranch(leaf);
            for (var i = 0; i < 2000; i++)
                label = tree.AddBranch(label, tree.Add("enrtree-branch:" + i.ToString("x")).ToString());
            tree.SetRoot(label, tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            await resolver.ResolveEnrsAsync(tree.Url, Timeout, 1, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.True(tree.Queries <= 500,
                $"{tree.Queries} sequential DNS queries were issued for one signed tree with maxLeaves=1; only ENR leaves count against the budget, branch entries are unbounded");
        }
    }
}
