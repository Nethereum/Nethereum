using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethereum.Merkle.Patricia.Nodes.Rlp;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests.SecurityAudit71.S14
{
    public class PatriciaNodeNestingS14Tests
    {
        private const int SmallStackBytes = 256 * 1024;

        private static byte[] List(byte[] payload)
        {
            if (payload.Length < 56) return new[] { (byte)(0xC0 + payload.Length) }.Concat(payload).ToArray();
            var lengthBytes = BitConverter.GetBytes(payload.Length).Reverse().SkipWhile(b => b == 0).ToArray();
            return new[] { (byte)(0xF7 + lengthBytes.Length) }.Concat(lengthBytes).Concat(payload).ToArray();
        }

        private static byte[] NestedExtensionChain(int depth)
        {
            var node = new byte[] { 0xC2, 0x20, 0x01 };
            for (var i = 0; i < depth; i++)
                node = List(new byte[] { 0x00 }.Concat(node).ToArray());
            return node;
        }

        private static (Exception thrown, long allocated) RunOnSmallStack(Action action)
        {
            Exception thrown = null;
            long allocated = 0;
            var thread = new Thread(() =>
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                thrown = Record.Exception(action);
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }, SmallStackBytes);
            thread.Start();
            thread.Join();
            return (thrown, allocated);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(20_000)]
        [Trait("Rule", "S14-7")]
        public void Given_AnInlineExtensionChainNestedDeeperThanAnyTrieCanBe_When_TheNodeIsDecoded_Then_ItIsRejectedWithBoundedAllocation(int depth)
        {
            var rlp = NestedExtensionChain(depth);

            var (thrown, allocated) = RunOnSmallStack(() =>
                NodeRlpDecoder.Decode(rlp, null, new byte[0], Sha3KeccackHashProvider.Instance));

            Assert.NotNull(thrown);
            Assert.True(allocated < 128L * rlp.Length, $"{rlp.Length} byte node allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-7")]
        public void Given_AProofWhoseRootNodeIsAnInlineExtensionChain20000Deep_When_Verified_Then_ItFailsWithoutKillingTheProcess()
        {
            var rlp = NestedExtensionChain(20_000);
            var root = Sha3KeccackHashProvider.Instance.ComputeHash(rlp);
            var key = new byte[32];
            bool verified = true;

            var (thrown, allocated) = RunOnSmallStack(() =>
                verified = PatriciaProofVerifier.TryVerify(root, key, new List<byte[]> { rlp }, out _));

            Assert.False(verified);
            Assert.True(allocated < 128L * rlp.Length, $"allocated {allocated} bytes");
        }

        [Fact]
        [Trait("Rule", "S14-7")]
        public void Given_ARangeProofWhoseNodeIsAnInlineExtensionChain20000Deep_When_Verified_Then_ItIsInvalidWithoutKillingTheProcess()
        {
            var rlp = NestedExtensionChain(20_000);
            var root = Sha3KeccackHashProvider.Instance.ComputeHash(rlp);
            var key = new byte[32];
            RangeProofResult result = new RangeProofResult(true, true);

            var (thrown, allocated) = RunOnSmallStack(() =>
                result = PatriciaRangeProofVerifier.Current.Verify(
                    root, key, new List<byte[]>(), new List<byte[]>(), new List<byte[]> { rlp }));

            Assert.False(result.Valid);
            Assert.True(allocated < 128L * rlp.Length, $"allocated {allocated} bytes");
        }
    }
}
