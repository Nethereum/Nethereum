using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class ProofGeneratorUnresolvedHashNodeTests
    {
        private static readonly byte[] Key = new byte[32];

        private static HashNode Unresolved() => new HashNode { Hash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 } };

        [Fact]
        public void Given_AHashNodeWithNoStore_When_AProofIsGenerated_Then_NoExceptionAndNoProofNodesForTheUnresolvedBranch()
        {
            Assert.Null(ProofGenerator.GenerateProof(Unresolved(), Key.ConvertToNibbles(), null, new System.Collections.Generic.List<byte[]>()));
            Assert.Null(ProofGenerator.GeneratePathProof(Unresolved(), Key.ConvertToNibbles(), null));
            Assert.Empty(PatriciaPathWalker.WalkPath(Unresolved(), null, Key.ConvertToNibbles()));
            Assert.Empty(PatriciaRangeProofGenerator.GenerateProof(Unresolved(), null, Key));
        }
    }
}
