using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Nethereum.Ssz;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.Ssz.Tests
{
    /// <summary>
    /// Cross-fork consensus-spec-tests vector validation: roundtrip + HashTreeRoot
    /// checks the LightClient SSZ stack against the official
    /// <c>tests/LightClientVectors/ssz/consensus-spec-tests/{fork}/ssz_static/{container}/ssz_random/case_N</c>
    /// vectors for Altair, Bellatrix, Capella, Deneb, and Electra. The existing Deneb-only
    /// theories in <see cref="SszContainerTests"/> are reused via fork-aware loader overloads.
    /// </summary>
    public class SpecVectorsCrossForkTests
    {
        private readonly ITestOutputHelper _output;

        public SpecVectorsCrossForkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly ConsensusFork[] AllForks = new[]
        {
            ConsensusFork.Altair,
            ConsensusFork.Bellatrix,
            ConsensusFork.Capella,
            ConsensusFork.Deneb,
            ConsensusFork.Electra
        };

        private static readonly ConsensusFork[] ForksWithExecutionPayload = new[]
        {
            ConsensusFork.Bellatrix,
            ConsensusFork.Capella,
            ConsensusFork.Deneb,
            ConsensusFork.Electra
        };

        [Theory]
        [MemberData(nameof(BeaconBlockHeaderVectors))]
        public void BeaconBlockHeader_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = BeaconBlockHeader.Decode(testCase.SerializedSpan);
            var encoded = decoded.Encode();
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot());
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(ExecutionPayloadHeaderVectors))]
        public void ExecutionPayloadHeader_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = ExecutionPayloadHeader.Decode(testCase.SerializedSpan, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(SyncCommitteeVectors))]
        public void SyncCommittee_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = SyncCommittee.Decode(testCase.SerializedSpan);
            var encoded = decoded.Encode();
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot());
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(SyncAggregateVectors))]
        public void SyncAggregate_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = SyncAggregate.Decode(testCase.SerializedSpan);
            var encoded = decoded.Encode();
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot());
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(LightClientHeaderVectors))]
        public void LightClientHeader_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = LightClientHeader.Decode(testCase.Serialized, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(LightClientBootstrapVectors))]
        public void LightClientBootstrap_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = LightClientBootstrap.Decode(testCase.Serialized, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(LightClientUpdateVectors))]
        public void LightClientUpdate_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = LightClientUpdate.Decode(testCase.Serialized, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(LightClientFinalityUpdateVectors))]
        public void LightClientFinalityUpdate_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = LightClientFinalityUpdate.Decode(testCase.Serialized, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        [Theory]
        [MemberData(nameof(LightClientOptimisticUpdateVectors))]
        public void LightClientOptimisticUpdate_CrossFork_RoundtripAndRoot(ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            var decoded = LightClientOptimisticUpdate.Decode(testCase.Serialized, fork);
            var encoded = decoded.Encode(fork);
            AssertByteEqual(testCase.Serialized, encoded, fork, testCase);
            Assert.Equal(testCase.Root, decoded.HashTreeRoot(fork));
            _output.WriteLine($"OK {fork}/{testCase.DisplayName}");
        }

        // gindex/depth internal consistency: subtree_index = gindex - 2^depth and depth = floor(log2(gindex)).
        // No single_merkle_proof vectors are present on disk under tests/LightClientVectors;
        // these checks cross-validate that the constants we ENCODE (LightClientForkSpec)
        // satisfy the canonical merkle-tree identity used by is_valid_merkle_branch per
        // https://github.com/ethereum/consensus-specs/blob/master/ssz/merkle-proofs.md
        // and the per-fork gindex/depth tables in altair + electra light-client/sync-protocol.md.
        [Theory]
        [InlineData(ConsensusFork.Altair, "current_sync_committee", 54, 5)]
        [InlineData(ConsensusFork.Bellatrix, "current_sync_committee", 54, 5)]
        [InlineData(ConsensusFork.Capella, "current_sync_committee", 54, 5)]
        [InlineData(ConsensusFork.Deneb, "current_sync_committee", 54, 5)]
        [InlineData(ConsensusFork.Electra, "current_sync_committee", 86, 6)]
        [InlineData(ConsensusFork.Altair, "next_sync_committee", 55, 5)]
        [InlineData(ConsensusFork.Bellatrix, "next_sync_committee", 55, 5)]
        [InlineData(ConsensusFork.Capella, "next_sync_committee", 55, 5)]
        [InlineData(ConsensusFork.Deneb, "next_sync_committee", 55, 5)]
        [InlineData(ConsensusFork.Electra, "next_sync_committee", 87, 6)]
        [InlineData(ConsensusFork.Altair, "finalized_root", 105, 6)]
        [InlineData(ConsensusFork.Bellatrix, "finalized_root", 105, 6)]
        [InlineData(ConsensusFork.Capella, "finalized_root", 105, 6)]
        [InlineData(ConsensusFork.Deneb, "finalized_root", 105, 6)]
        [InlineData(ConsensusFork.Electra, "finalized_root", 169, 7)]
        [InlineData(ConsensusFork.Capella, "execution_payload", 25, 4)]
        [InlineData(ConsensusFork.Deneb, "execution_payload", 25, 4)]
        [InlineData(ConsensusFork.Electra, "execution_payload", 25, 4)]
        public void ForkSpec_GindexAndDepth_MatchSpec(ConsensusFork fork, string field, int expectedGindex, int expectedDepth)
        {
            int actualGindex;
            int actualDepth;
            int actualSubtree;
            switch (field)
            {
                case "current_sync_committee":
                    actualGindex = LightClientForkSpec.CurrentSyncCommitteeGIndex(fork);
                    actualDepth = LightClientForkSpec.CurrentSyncCommitteeBranchDepth(fork);
                    actualSubtree = LightClientForkSpec.CurrentSyncCommitteeBranchIndex(fork);
                    break;
                case "next_sync_committee":
                    actualGindex = LightClientForkSpec.NextSyncCommitteeGIndex(fork);
                    actualDepth = LightClientForkSpec.NextSyncCommitteeBranchDepth(fork);
                    actualSubtree = LightClientForkSpec.NextSyncCommitteeBranchIndex(fork);
                    break;
                case "finalized_root":
                    actualGindex = LightClientForkSpec.FinalizedRootGIndex(fork);
                    actualDepth = LightClientForkSpec.FinalityBranchDepth(fork);
                    actualSubtree = LightClientForkSpec.FinalityBranchIndex(fork);
                    break;
                case "execution_payload":
                    actualGindex = LightClientForkSpec.ExecutionPayloadGIndex;
                    actualDepth = LightClientForkSpec.ExecutionBranchDepth(fork);
                    actualSubtree = LightClientForkSpec.ExecutionBranchIndex(fork);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown field {field}");
            }

            Assert.Equal(expectedGindex, actualGindex);
            Assert.Equal(expectedDepth, actualDepth);
            // floor(log2(gindex)) == depth (canonical is_valid_merkle_branch invariant)
            Assert.Equal(expectedDepth, (int)Math.Floor(Math.Log2(expectedGindex)));
            // subtree_index = gindex - 2^depth
            var expectedSubtree = expectedGindex - (1 << expectedDepth);
            Assert.Equal(expectedSubtree, actualSubtree);
        }

        // VerifyProof self-test against a synthetic 2^depth merkle tree.
        // No on-disk single_merkle_proof vectors are available, so we construct
        // a known tree, prove a specific leaf, and assert VerifyProof accepts
        // the genuine proof and rejects a corrupted one.
        [Theory]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void VerifyProof_AcceptsValidBranchAndRejectsCorrupted(int depth)
        {
            var leafCount = 1 << depth;
            var leaves = new List<byte[]>(leafCount);
            for (var i = 0; i < leafCount; i++)
            {
                var leaf = new byte[32];
                leaf[0] = (byte)(i & 0xFF);
                leaf[1] = (byte)((i >> 8) & 0xFF);
                leaves.Add(leaf);
            }

            var root = SszMerkleizer.Merkleize(leaves);
            const int provedIndex = 3;
            var branch = BuildBranch(leaves, provedIndex, depth);

            Assert.True(SszMerkleizer.VerifyProof(leaves[provedIndex], branch, depth, provedIndex, root));

            var corrupted = new List<byte[]>(branch);
            var sibling = (byte[])corrupted[0].Clone();
            sibling[0] ^= 0xFF;
            corrupted[0] = sibling;
            Assert.False(SszMerkleizer.VerifyProof(leaves[provedIndex], corrupted, depth, provedIndex, root));
        }

        private static List<byte[]> BuildBranch(IList<byte[]> leaves, int leafIndex, int depth)
        {
            var working = new List<byte[]>(leaves);
            var branch = new List<byte[]>(depth);
            var currentIndex = leafIndex;
            for (var level = 0; level < depth; level++)
            {
                var siblingIndex = currentIndex ^ 1;
                branch.Add(working[siblingIndex]);
                var next = new List<byte[]>(working.Count / 2);
                for (var i = 0; i < working.Count; i += 2)
                {
                    next.Add(HashPair(working[i], working[i + 1]));
                }
                working = next;
                currentIndex >>= 1;
            }
            return branch;
        }

        private static byte[] HashPair(byte[] left, byte[] right)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var combined = new byte[64];
            Buffer.BlockCopy(left, 0, combined, 0, 32);
            Buffer.BlockCopy(right, 0, combined, 32, 32);
            return sha.ComputeHash(combined);
        }

        public static IEnumerable<object[]> BeaconBlockHeaderVectors() => LoadAcross(AllForks, "BeaconBlockHeader");
        public static IEnumerable<object[]> ExecutionPayloadHeaderVectors() => LoadAcross(ForksWithExecutionPayload, "ExecutionPayloadHeader");
        public static IEnumerable<object[]> SyncCommitteeVectors() => LoadAcross(AllForks, "SyncCommittee");
        public static IEnumerable<object[]> SyncAggregateVectors() => LoadAcross(AllForks, "SyncAggregate");
        public static IEnumerable<object[]> LightClientHeaderVectors() => LoadAcross(AllForks, "LightClientHeader");
        public static IEnumerable<object[]> LightClientBootstrapVectors() => LoadAcross(AllForks, "LightClientBootstrap");
        public static IEnumerable<object[]> LightClientUpdateVectors() => LoadAcross(AllForks, "LightClientUpdate");
        public static IEnumerable<object[]> LightClientFinalityUpdateVectors() => LoadAcross(AllForks, "LightClientFinalityUpdate");
        public static IEnumerable<object[]> LightClientOptimisticUpdateVectors() => LoadAcross(AllForks, "LightClientOptimisticUpdate");

        private static IEnumerable<object[]> LoadAcross(ConsensusFork[] forks, string container)
        {
            foreach (var fork in forks)
            {
                foreach (var testCase in ConsensusSpecTestCaseProvider.Load(fork, container))
                {
                    yield return new object[] { fork, testCase };
                }
            }
        }

        private void AssertByteEqual(byte[] expected, byte[] actual, ConsensusFork fork, ConsensusSpecTestCase testCase)
        {
            if (expected.Length != actual.Length || !expected.AsSpan().SequenceEqual(actual))
            {
                var diffIndex = FirstDiff(expected, actual);
                var expectedPrefix = HexPrefix(expected, Math.Min(100, expected.Length));
                var actualPrefix = HexPrefix(actual, Math.Min(100, actual.Length));
                _output.WriteLine($"FAIL roundtrip {fork}/{testCase.DisplayName}: expectedLen={expected.Length} actualLen={actual.Length} firstDiff={diffIndex}");
                _output.WriteLine($"  expected[0..100] = {expectedPrefix}");
                _output.WriteLine($"  actual  [0..100] = {actualPrefix}");
                Assert.Fail($"Byte roundtrip mismatch for {fork}/{testCase.DisplayName}: firstDiffIndex={diffIndex} expectedLen={expected.Length} actualLen={actual.Length}");
            }
        }

        private static int FirstDiff(byte[] a, byte[] b)
        {
            var min = Math.Min(a.Length, b.Length);
            for (var i = 0; i < min; i++)
            {
                if (a[i] != b[i]) return i;
            }
            return a.Length == b.Length ? -1 : min;
        }

        private static string HexPrefix(byte[] data, int count)
        {
            var len = Math.Min(count, data.Length);
            return BitConverter.ToString(data, 0, len);
        }
    }
}
