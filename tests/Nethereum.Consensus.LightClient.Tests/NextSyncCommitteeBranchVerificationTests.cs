using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class NextSyncCommitteeBranchVerificationTests
    {
        [Theory]
        [InlineData(ConsensusFork.Altair, 5, 23)]
        [InlineData(ConsensusFork.Bellatrix, 5, 23)]
        [InlineData(ConsensusFork.Capella, 5, 23)]
        [InlineData(ConsensusFork.Deneb, 5, 23)]
        [InlineData(ConsensusFork.Electra, 6, 23)]
        public void Given_Fork_When_QueryingForkSpec_Then_DepthAndIndexMatchSpec(ConsensusFork fork, int expectedDepth, int expectedIndex)
        {
            Assert.Equal(expectedDepth, LightClientForkSpec.NextSyncCommitteeBranchDepth(fork));
            Assert.Equal(expectedIndex, LightClientForkSpec.NextSyncCommitteeBranchIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_ValidNextCommitteeBranch_When_Verifying_Then_ReturnsTrue(ConsensusFork fork)
        {
            var committee = CreateCommittee(seed: 0x10);
            var branch = BuildValidNextSyncCommitteeBranch(committee, fork, out var stateRoot);
            var attestedHeader = CreateAttestedHeader(stateRoot, fork);

            var result = InvokeVerifyNextSyncCommitteeBranch(attestedHeader, committee, branch);

            Assert.True(result);
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_TamperedCommitteeLeaf_When_Verifying_Then_ReturnsFalse(ConsensusFork fork)
        {
            var committee = CreateCommittee(seed: 0x10);
            var branch = BuildValidNextSyncCommitteeBranch(committee, fork, out var stateRoot);
            var attestedHeader = CreateAttestedHeader(stateRoot, fork);

            var tampered = CreateCommittee(seed: 0x20);

            var result = InvokeVerifyNextSyncCommitteeBranch(attestedHeader, tampered, branch);

            Assert.False(result);
        }

        [Fact]
        public void Given_WrongLengthBranch_When_VerifyingAtElectra_Then_ReturnsFalse()
        {
            var fork = ConsensusFork.Electra;
            var committee = CreateCommittee(seed: 0x10);
            var validBranch = BuildValidNextSyncCommitteeBranch(committee, fork, out var stateRoot);
            var shortBranch = validBranch.Take(LightClientForkSpec.NextSyncCommitteeBranchDepth(ConsensusFork.Deneb)).ToList();
            var attestedHeader = CreateAttestedHeader(stateRoot, fork);

            var result = InvokeVerifyNextSyncCommitteeBranch(attestedHeader, committee, shortBranch);

            Assert.False(result);
        }

        [Fact]
        public void Given_NullInputs_When_Verifying_Then_ReturnsFalse()
        {
            var committee = CreateCommittee(seed: 0x10);
            var branch = BuildValidNextSyncCommitteeBranch(committee, ConsensusFork.Electra, out var stateRoot);
            var attestedHeader = CreateAttestedHeader(stateRoot, ConsensusFork.Electra);

            Assert.False(InvokeVerifyNextSyncCommitteeBranch(null, committee, branch));
            Assert.False(InvokeVerifyNextSyncCommitteeBranch(attestedHeader, null, branch));
            Assert.False(InvokeVerifyNextSyncCommitteeBranch(attestedHeader, committee, null));
        }

        [Fact]
        public void Given_MalformedCommittee_When_Verifying_Then_ReturnsFalseInsteadOfThrowing()
        {
            var fork = ConsensusFork.Electra;
            var validCommittee = CreateCommittee(seed: 0x10);
            var branch = BuildValidNextSyncCommitteeBranch(validCommittee, fork, out var stateRoot);
            var attestedHeader = CreateAttestedHeader(stateRoot, fork);

            var malformed = new SyncCommittee
            {
                PubKeys = new List<byte[]>(),
                AggregatePubKey = new byte[SszBasicTypes.PubKeyLength]
            };

            var result = InvokeVerifyNextSyncCommitteeBranch(attestedHeader, malformed, branch);

            Assert.False(result);
        }

        private static bool InvokeVerifyNextSyncCommitteeBranch(LightClientHeader attestedHeader, SyncCommittee committee, IList<byte[]> branch)
        {
            var method = typeof(LightClientService).GetMethod(
                "VerifyNextSyncCommitteeBranch",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (bool)method!.Invoke(null, new object[] { attestedHeader, committee, branch });
        }

        internal static SyncCommittee CreateCommittee(byte seed)
        {
            var pubkeys = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
            for (var i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
            {
                var key = new byte[SszBasicTypes.PubKeyLength];
                for (var j = 0; j < key.Length; j++)
                {
                    key[j] = (byte)((seed + i + j) & 0xFF);
                }
                pubkeys.Add(key);
            }

            return new SyncCommittee
            {
                PubKeys = pubkeys,
                AggregatePubKey = Enumerable.Repeat(seed, SszBasicTypes.PubKeyLength).ToArray()
            };
        }

        internal static List<byte[]> BuildValidNextSyncCommitteeBranch(SyncCommittee committee, ConsensusFork fork, out byte[] stateRoot)
        {
            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(fork);
            var index = LightClientForkSpec.NextSyncCommitteeBranchIndex(fork);

            var branch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                var sibling = new byte[SszBasicTypes.RootLength];
                for (var j = 0; j < sibling.Length; j++)
                {
                    sibling[j] = (byte)((0xA0 + i + j) & 0xFF);
                }
                branch.Add(sibling);
            }

            var current = committee.HashTreeRoot();
            for (var i = 0; i < depth; i++)
            {
                if ((index & (1 << i)) != 0)
                {
                    current = HashPair(branch[i], current);
                }
                else
                {
                    current = HashPair(current, branch[i]);
                }
            }

            stateRoot = current;
            return branch;
        }

        internal static LightClientHeader CreateAttestedHeader(byte[] stateRoot, ConsensusFork fork)
        {
            return new LightClientHeader
            {
                Fork = fork,
                Beacon = new BeaconBlockHeader
                {
                    Slot = 12_000_000UL,
                    ProposerIndex = 1UL,
                    ParentRoot = Enumerable.Repeat((byte)0x01, SszBasicTypes.RootLength).ToArray(),
                    StateRoot = stateRoot,
                    BodyRoot = Enumerable.Repeat((byte)0x03, SszBasicTypes.RootLength).ToArray()
                }
            };
        }

        private static byte[] HashPair(byte[] left, byte[] right)
        {
            using var sha = SHA256.Create();
            var combined = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, combined, 0, left.Length);
            Buffer.BlockCopy(right, 0, combined, left.Length, right.Length);
            return sha.ComputeHash(combined);
        }
    }
}
