using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Dns;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2DnsQueryBudgetSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SignedTreeWithThreeHundredUnparseableLeaves_When_ResolvedWithASmallLeafBudget_Then_DnsQueriesStayWithinTheBudget()
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaves = Enumerable.Range(0, 300).Select(i => tree.Add("enr:-junk" + i)).ToArray();
            tree.SetRoot(tree.AddBranch(leaves), tree.AddBranch());
            var resolver = new EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 1, CancellationToken.None);

            Assert.True(tree.Queries <= 70, $"{tree.Queries} DNS queries were issued for maxLeaves=1");
        }
    }
}
