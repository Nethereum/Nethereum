using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Merkle;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SecurityAudit71.S12
{
    [Struct("Pair")]
    public class Bytes32Pair
    {
        [Parameter("bytes32", "a", 1)]
        public byte[] A { get; set; }

        [Parameter("bytes32", "b", 2)]
        public byte[] B { get; set; }
    }

    public class MerkleTreeSoundnessTests
    {
        private static readonly System.Text.Encoding Latin1 = System.Text.Encoding.GetEncoding("iso-8859-1");

        private static byte[] Word(byte seed) => Enumerable.Repeat(seed, 32).ToArray();

        private static byte[] Concat(byte[] x, byte[] y) => x.Concat(y).ToArray();

        [Fact]
        [Trait("AcceptedRisk", "S12-3")]
        [Trait("Rule", "S12-30")]
        public void Given_a_tree_of_64_byte_leaves_when_an_internal_node_preimage_is_presented_as_a_leaf_then_the_single_hash_tree_accepts_it_and_the_openzeppelin_tree_rejects_it()
        {
            var items = Enumerable.Range(1, 4).Select(i => new Bytes32Pair { A = Word((byte)i), B = Word((byte)(i + 10)) }).ToList();
            var tree = new AbiStructMerkleTree<Bytes32Pair>();
            tree.BuildTree(items);

            var h0 = tree.Leaves[0].Hash;
            var h1 = tree.Leaves[1].Hash;
            var ordered = new[] { h0, h1 }.OrderBy(x => x, ByteArrayComparer.Current).ToArray();
            var forged = new Bytes32Pair { A = ordered[0], B = ordered[1] };
            var uncle = tree.Layers[1][1].Hash;

            Assert.True(tree.VerifyProof(new List<byte[]> { uncle }, forged));
            AssertOpenZeppelinRejectsForgery(items);
        }

        [Fact]
        [Trait("AcceptedRisk", "S12-3")]
        [Trait("Rule", "S12-30")]
        public void Given_a_generic_tree_when_an_internal_node_preimage_is_presented_as_a_leaf_then_the_single_hash_tree_accepts_it_and_the_openzeppelin_tree_rejects_it()
        {
            var items = Enumerable.Range(1, 4).Select(i => Latin1.GetString(Word((byte)i))).ToList();
            var tree = new MerkleTree<string>(Sha3KeccackHashProvider.Instance, new Latin1Convertor());
            tree.BuildTree(items);

            var ordered = new[] { tree.Leaves[0].Hash, tree.Leaves[1].Hash }.OrderBy(x => x, ByteArrayComparer.Current).ToArray();
            var forged = Latin1.GetString(Concat(ordered[0], ordered[1]));
            var uncle = tree.Layers[1][1].Hash;

            Assert.True(tree.VerifyProof(new List<byte[]> { uncle }, forged));
            AssertOpenZeppelinRejectsForgery(Enumerable.Range(1, 4).Select(i => new Bytes32Pair { A = Word((byte)i), B = Word((byte)(i + 10)) }).ToList());
        }

        private static void AssertOpenZeppelinRejectsForgery(List<Bytes32Pair> items)
        {
            var oz = new OpenZeppelinStandardMerkleTree<Bytes32Pair>();
            oz.BuildTree(items);
            var ordered = new[] { oz.Leaves[0].Hash, oz.Leaves[1].Hash }.OrderBy(x => x, ByteArrayComparer.Current).ToArray();
            var forged = new Bytes32Pair { A = ordered[0], B = ordered[1] };
            Assert.False(oz.VerifyProof(new List<byte[]> { oz.Layers[1][1].Hash }, forged));
        }

        [Fact]
        [Trait("Rule", "S12-31")]
        public void Given_an_openzeppelin_standard_tree_when_an_internal_node_preimage_is_presented_as_a_leaf_then_it_is_rejected()
        {
            var items = Enumerable.Range(1, 4).Select(i => new Bytes32Pair { A = Word((byte)i), B = Word((byte)(i + 10)) }).ToList();
            var tree = new OpenZeppelinStandardMerkleTree<Bytes32Pair>();
            tree.BuildTree(items);

            var ordered = new[] { tree.Leaves[0].Hash, tree.Leaves[1].Hash }.OrderBy(x => x, ByteArrayComparer.Current).ToArray();
            var forged = new Bytes32Pair { A = ordered[0], B = ordered[1] };
            var uncle = tree.Layers[1][1].Hash;

            Assert.False(tree.VerifyProof(new List<byte[]> { uncle }, forged));
        }

        [Fact]
        [Trait("Rule", "S12-32")]
        public void Given_a_tree_when_the_last_leaf_is_duplicated_then_the_root_differs()
        {
            var three = new MerkleTree<string>(Sha3KeccackHashProvider.Instance, new Latin1Convertor());
            three.BuildTree(new List<string> { "a", "b", "c" });
            var four = new MerkleTree<string>(Sha3KeccackHashProvider.Instance, new Latin1Convertor());
            four.BuildTree(new List<string> { "a", "b", "c", "c" });

            Assert.NotEqual(three.Root.Hash, four.Root.Hash);
        }

        [Fact]
        [Trait("Rule", "S12-33")]
        public void Given_a_proof_when_nodes_are_truncated_or_extended_then_it_is_rejected()
        {
            var tree = new MerkleTree<string>(Sha3KeccackHashProvider.Instance, new Latin1Convertor());
            tree.BuildTree(Enumerable.Range(1, 8).Select(i => "leaf" + i).ToList());
            var proof = tree.GetProof("leaf3");
            Assert.True(tree.VerifyProof(proof, "leaf3"));

            Assert.False(tree.VerifyProof(proof.Take(proof.Count - 1).ToList(), "leaf3"));
            Assert.False(tree.VerifyProof(proof.Concat(new[] { Word(0) }).ToList(), "leaf3"));
            Assert.False(tree.VerifyProof(proof.AsEnumerable().Reverse().ToList(), "leaf3"));
        }

        [Fact]
        [Trait("Rule", "S12-33")]
        public void Given_a_proof_with_a_null_node_when_verified_then_it_returns_false_without_throwing()
        {
            var tree = new MerkleTree<string>(Sha3KeccackHashProvider.Instance, new Latin1Convertor());
            tree.BuildTree(Enumerable.Range(1, 4).Select(i => "leaf" + i).ToList());
            var proof = tree.GetProof("leaf1");
            proof[0] = null;

            bool result = true;
            var ex = Record.Exception(() => result = tree.VerifyProof(proof, "leaf1"));
            Assert.Null(ex);
            Assert.False(result);
        }
    }

    public class Latin1Convertor : IByteArrayConvertor<string>
    {
        public byte[] ConvertToByteArray(string data) => System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(data);
        public string ConvertFromByteArray(byte[] data) => System.Text.Encoding.GetEncoding("iso-8859-1").GetString(data);
    }
}
