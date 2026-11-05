using System.Linq;
using System.Text;
using Nethereum.CoreChain.Rpc;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S7.Round3
{
    public class S7R3RpcBatchGuardTests
    {
        private static byte[] Batch(int members, string member = "{\"id\":7,\"method\":\"eth_chainId\"}") =>
            Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat(member, members)) + "]");

        [Fact]
        [Trait("Rule", "S7-R3-11")]
        [Trait("Erc", "JSON-RPC batch")]
        public void RejectOversizedBatch_WhenTheBatchHasMoreMembersThanTheLimit_ReturnsOneBatchTooLargeErrorWithTheFirstId()
        {
            var rejection = RpcBatchGuard.RejectOversizedBatch(Batch(1001), 1000);

            Assert.NotNull(rejection);
            Assert.Single(rejection!);
            Assert.Equal(-32600, rejection[0].Error.Code);
            Assert.Equal("batch too large", rejection[0].Error.Message);
            Assert.Equal(7L, rejection[0].Id);
        }

        [Fact]
        [Trait("Rule", "S7-R3-11")]
        [Trait("Erc", "JSON-RPC batch")]
        public void RejectOversizedBatch_WhenTheBatchIsAtTheLimitOrNotABatchOrMalformed_ReturnsNull()
        {
            Assert.Null(RpcBatchGuard.RejectOversizedBatch(Batch(1000), 1000));
            Assert.Null(RpcBatchGuard.RejectOversizedBatch(Encoding.UTF8.GetBytes("{\"id\":1}"), 1000));
            Assert.Null(RpcBatchGuard.RejectOversizedBatch(Encoding.UTF8.GetBytes("[{\"id\":1},"), 1000));
        }

        [Fact]
        [Trait("Rule", "S7-R3-11")]
        [Trait("Erc", "JSON-RPC batch")]
        public void RejectOversizedBatch_WhenMembersAreNestedAndNonObjects_CountsOnlyTopLevelMembers()
        {
            var nested = "{\"id\":\"a\",\"params\":[[1,2,3],{\"x\":[4,5]}]}";
            var body = Encoding.UTF8.GetBytes("[" + nested + ",[1,2,3],5,null,\"s\"]");

            Assert.Null(RpcBatchGuard.RejectOversizedBatch(body, 5));
            var rejection = RpcBatchGuard.RejectOversizedBatch(body, 4);

            Assert.Equal("a", rejection![0].Id);
        }
    }
}
