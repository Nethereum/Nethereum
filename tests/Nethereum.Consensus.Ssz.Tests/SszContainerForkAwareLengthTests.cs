using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests
{
    /// <summary>
    /// Verifies the fork-aware container fixed-section byte layouts match the spec
    /// table in <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> (Altair–Deneb: depth 5 sync-committee
    /// branches, depth 6 finality branch) and
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
    /// specs/electra/light-client/sync-protocol.md</see> (Electra+: depth 6 sync-committee
    /// branches, depth 7 finality branch).
    /// </summary>
    public class SszContainerForkAwareLengthTests
    {
        [Theory]
        [InlineData(ConsensusFork.Altair, 5)]
        [InlineData(ConsensusFork.Bellatrix, 5)]
        [InlineData(ConsensusFork.Capella, 5)]
        [InlineData(ConsensusFork.Deneb, 5)]
        [InlineData(ConsensusFork.Electra, 6)]
        [InlineData(ConsensusFork.Fulu, 6)]
        public void LightClientForkSpec_CurrentSyncCommitteeBranchLength_MatchesSpec(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork));
            Assert.Equal(expected, LightClientForkSpec.CurrentSyncCommitteeBranchDepth(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair, 5)]
        [InlineData(ConsensusFork.Bellatrix, 5)]
        [InlineData(ConsensusFork.Capella, 5)]
        [InlineData(ConsensusFork.Deneb, 5)]
        [InlineData(ConsensusFork.Electra, 6)]
        [InlineData(ConsensusFork.Fulu, 6)]
        public void LightClientForkSpec_NextSyncCommitteeBranchLength_MatchesSpec(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.NextSyncCommitteeBranchLength(fork));
            Assert.Equal(expected, LightClientForkSpec.NextSyncCommitteeBranchDepth(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair, 6)]
        [InlineData(ConsensusFork.Bellatrix, 6)]
        [InlineData(ConsensusFork.Capella, 6)]
        [InlineData(ConsensusFork.Deneb, 6)]
        [InlineData(ConsensusFork.Electra, 7)]
        [InlineData(ConsensusFork.Fulu, 7)]
        public void LightClientForkSpec_FinalityBranchLength_MatchesSpec(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.FinalityBranchLength(fork));
            Assert.Equal(expected, LightClientForkSpec.FinalityBranchDepth(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair, 54)]
        [InlineData(ConsensusFork.Deneb, 54)]
        [InlineData(ConsensusFork.Electra, 86)]
        [InlineData(ConsensusFork.Fulu, 86)]
        public void LightClientForkSpec_CurrentSyncCommitteeGIndex_MatchesSpec(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.CurrentSyncCommitteeGIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair, 55)]
        [InlineData(ConsensusFork.Deneb, 55)]
        [InlineData(ConsensusFork.Electra, 87)]
        [InlineData(ConsensusFork.Fulu, 87)]
        public void LightClientForkSpec_NextSyncCommitteeGIndex_MatchesSpec(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.NextSyncCommitteeGIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        [InlineData(ConsensusFork.Fulu)]
        public void LightClientForkSpec_CurrentSyncCommitteeBranchIndex_Equals22(ConsensusFork fork)
        {
            Assert.Equal(22, LightClientForkSpec.CurrentSyncCommitteeBranchIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        [InlineData(ConsensusFork.Fulu)]
        public void LightClientForkSpec_NextSyncCommitteeBranchIndex_Equals23(ConsensusFork fork)
        {
            Assert.Equal(23, LightClientForkSpec.NextSyncCommitteeBranchIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Capella, 4)]
        [InlineData(ConsensusFork.Deneb, 4)]
        [InlineData(ConsensusFork.Electra, 4)]
        [InlineData(ConsensusFork.Fulu, 4)]
        public void LightClientForkSpec_ExecutionBranchDepth_CapellaThroughFulu(
            ConsensusFork fork, int expected)
        {
            Assert.Equal(expected, LightClientForkSpec.ExecutionBranchDepth(fork));
            Assert.Equal(9, LightClientForkSpec.ExecutionBranchIndex(fork));
        }

        [Theory]
        [InlineData(ConsensusFork.Phase0)]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        public void LightClientForkSpec_ExecutionBranchDepth_PreCapella_Throws(ConsensusFork fork)
        {
            Assert.Throws<InvalidOperationException>(() => LightClientForkSpec.ExecutionBranchDepth(fork));
        }

        [Fact]
        public void LightClientForkSpec_ExecutionBranchDepth_Gloas_Throws_NotSupported()
        {
            Assert.Throws<NotSupportedException>(() => LightClientForkSpec.ExecutionBranchDepth(ConsensusFork.Gloas));
        }

        [Fact]
        public void LightClientForkSpec_CurrentSyncCommitteeBranchLength_Gloas_Throws_NotSupported()
        {
            Assert.Throws<NotSupportedException>(() => LightClientForkSpec.CurrentSyncCommitteeBranchLength(ConsensusFork.Gloas));
        }

        [Fact]
        public void LightClientForkSpec_NextSyncCommitteeBranchLength_Gloas_Throws_NotSupported()
        {
            Assert.Throws<NotSupportedException>(() => LightClientForkSpec.NextSyncCommitteeBranchLength(ConsensusFork.Gloas));
        }

        [Fact]
        public void LightClientForkSpec_FinalityBranchLength_Gloas_Throws_NotSupported()
        {
            Assert.Throws<NotSupportedException>(() => LightClientForkSpec.FinalityBranchLength(ConsensusFork.Gloas));
        }

        [Theory]
        [InlineData(ConsensusFork.Capella, 24804)]
        [InlineData(ConsensusFork.Deneb, 24804)]
        [InlineData(ConsensusFork.Electra, 24836)]
        [InlineData(ConsensusFork.Fulu, 24836)]
        public void LightClientBootstrap_FixedSectionEncode_LengthMatchesFork(
            ConsensusFork fork, int expectedFixedHead)
        {
            var bootstrap = BuildBootstrap(fork);
            var encoded = bootstrap.Encode(fork);
            Assert.True(encoded.Length >= expectedFixedHead,
                $"Expected encoded length >= {expectedFixedHead} for fork {fork}, got {encoded.Length}");

            var decoded = LightClientBootstrap.Decode(encoded, fork);
            Assert.Equal(
                LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork),
                decoded.CurrentSyncCommitteeBranch.Count);
        }

        [Theory]
        [InlineData(ConsensusFork.Capella, 5, 6)]
        [InlineData(ConsensusFork.Deneb, 5, 6)]
        [InlineData(ConsensusFork.Electra, 6, 7)]
        [InlineData(ConsensusFork.Fulu, 6, 7)]
        public void LightClientUpdate_BranchLengths_MatchFork(
            ConsensusFork fork, int expectedNextBranchLen, int expectedFinalityBranchLen)
        {
            var update = BuildUpdate(fork);
            var encoded = update.Encode(fork);
            var decoded = LightClientUpdate.Decode(encoded, fork);
            Assert.Equal(expectedNextBranchLen, decoded.NextSyncCommitteeBranch.Count);
            Assert.Equal(expectedFinalityBranchLen, decoded.FinalityBranch.Count);
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        [InlineData(ConsensusFork.Fulu)]
        public void LightClientHeader_FixedSection_RoundTripsPerFork(ConsensusFork fork)
        {
            var header = BuildHeader(fork);
            var encoded = header.Encode(fork);
            var decoded = LightClientHeader.Decode(encoded, fork);
            Assert.Equal(fork, decoded.Fork);
            Assert.Equal(header.Beacon.Slot, decoded.Beacon.Slot);
            if (LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                Assert.Equal(
                    LightClientForkSpec.ExecutionBranchDepth(fork),
                    decoded.ExecutionBranch.Count);
            }
            else
            {
                Assert.Empty(decoded.ExecutionBranch);
            }
        }

        [Fact]
        public void LightClientBootstrap_Encode_ThrowsWhenHeaderForkMismatchesOuter()
        {
            var bootstrap = BuildBootstrap(ConsensusFork.Electra);
            bootstrap.Header.Fork = ConsensusFork.Deneb;
            Assert.Throws<InvalidOperationException>(() => bootstrap.Encode(ConsensusFork.Electra));
        }

        [Fact]
        public void LightClientUpdate_Encode_ThrowsWhenAttestedHeaderForkMismatchesOuter()
        {
            var update = BuildUpdate(ConsensusFork.Electra);
            update.AttestedHeader.Fork = ConsensusFork.Deneb;
            Assert.Throws<InvalidOperationException>(() => update.Encode(ConsensusFork.Electra));
        }

        [Fact]
        public void LightClientUpdate_Encode_ThrowsWhenFinalizedHeaderForkMismatchesOuter()
        {
            var update = BuildUpdate(ConsensusFork.Electra);
            update.FinalizedHeader.Fork = ConsensusFork.Deneb;
            Assert.Throws<InvalidOperationException>(() => update.Encode(ConsensusFork.Electra));
        }

        [Fact]
        public void LightClientFinalityUpdate_Encode_ThrowsWhenFinalizedHeaderForkMismatchesOuter()
        {
            var finality = BuildFinalityUpdate(ConsensusFork.Electra);
            finality.FinalizedHeader.Fork = ConsensusFork.Deneb;
            Assert.Throws<InvalidOperationException>(() => finality.Encode(ConsensusFork.Electra));
        }

        [Fact]
        public void LightClientOptimisticUpdate_Encode_ThrowsWhenAttestedHeaderForkMismatchesOuter()
        {
            var optimistic = BuildOptimisticUpdate(ConsensusFork.Electra);
            optimistic.AttestedHeader.Fork = ConsensusFork.Deneb;
            Assert.Throws<InvalidOperationException>(() => optimistic.Encode(ConsensusFork.Electra));
        }

        private static BeaconBlockHeader BuildBeacon() => new BeaconBlockHeader
        {
            Slot = 1,
            ProposerIndex = 1,
            ParentRoot = Bytes(SszBasicTypes.RootLength, 0x01),
            StateRoot = Bytes(SszBasicTypes.RootLength, 0x02),
            BodyRoot = Bytes(SszBasicTypes.RootLength, 0x03)
        };

        private static ExecutionPayloadHeader BuildExecution(ConsensusFork fork) => new ExecutionPayloadHeader
        {
            Fork = fork,
            ParentHash = Bytes(SszBasicTypes.RootLength, 0x10),
            FeeRecipient = Bytes(20, 0x11),
            StateRoot = Bytes(SszBasicTypes.RootLength, 0x12),
            ReceiptsRoot = Bytes(SszBasicTypes.RootLength, 0x13),
            LogsBloom = Bytes(SszBasicTypes.LogsBloomLength, 0x14),
            PrevRandao = Bytes(SszBasicTypes.RootLength, 0x15),
            BlockNumber = 100,
            GasLimit = 30_000_000,
            GasUsed = 1_000_000,
            Timestamp = 123456,
            ExtraData = new byte[] { 0xAA },
            BaseFeePerGas = Bytes(SszBasicTypes.RootLength, 0x16),
            BlockHash = Bytes(SszBasicTypes.RootLength, 0x17),
            TransactionsRoot = Bytes(SszBasicTypes.RootLength, 0x18),
            WithdrawalsRoot = Bytes(SszBasicTypes.RootLength, 0x19),
            BlobGasUsed = 0,
            ExcessBlobGas = 0
        };

        private static LightClientHeader BuildHeader(ConsensusFork fork)
        {
            var header = new LightClientHeader
            {
                Fork = fork,
                Beacon = BuildBeacon()
            };
            if (LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                header.Execution = BuildExecution(fork);
                header.ExecutionBranch = CreateBranch(LightClientForkSpec.ExecutionBranchDepth(fork), 0x40);
            }
            return header;
        }

        private static SyncCommittee BuildSyncCommittee()
        {
            var pubkeys = Enumerable.Range(0, SszBasicTypes.SyncCommitteeSize)
                .Select(i => Bytes(SszBasicTypes.PubKeyLength, (byte)(i % 255)))
                .ToList();
            return new SyncCommittee
            {
                PubKeys = pubkeys,
                AggregatePubKey = Bytes(SszBasicTypes.PubKeyLength, 0xAA)
            };
        }

        private static SyncAggregate BuildSyncAggregate() => new SyncAggregate
        {
            SyncCommitteeBits = Bytes(SszBasicTypes.SyncCommitteeSize / 8, 0xCC),
            SyncCommitteeSignature = Bytes(SszBasicTypes.SignatureLength, 0xDD)
        };

        private static LightClientBootstrap BuildBootstrap(ConsensusFork fork) => new LightClientBootstrap
        {
            Fork = fork,
            Header = BuildHeader(fork),
            CurrentSyncCommittee = BuildSyncCommittee(),
            CurrentSyncCommitteeBranch = CreateBranch(LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork), 0x50)
        };

        private static LightClientUpdate BuildUpdate(ConsensusFork fork) => new LightClientUpdate
        {
            Fork = fork,
            AttestedHeader = BuildHeader(fork),
            NextSyncCommittee = BuildSyncCommittee(),
            NextSyncCommitteeBranch = CreateBranch(LightClientForkSpec.NextSyncCommitteeBranchLength(fork), 0x60),
            FinalizedHeader = BuildHeader(fork),
            FinalityBranch = CreateBranch(LightClientForkSpec.FinalityBranchLength(fork), 0x70),
            SyncAggregate = BuildSyncAggregate(),
            SignatureSlot = 1234
        };

        private static LightClientFinalityUpdate BuildFinalityUpdate(ConsensusFork fork) => new LightClientFinalityUpdate
        {
            Fork = fork,
            AttestedHeader = BuildHeader(fork),
            FinalizedHeader = BuildHeader(fork),
            FinalityBranch = CreateBranch(LightClientForkSpec.FinalityBranchLength(fork), 0x80),
            SyncAggregate = BuildSyncAggregate(),
            SignatureSlot = 4321
        };

        private static LightClientOptimisticUpdate BuildOptimisticUpdate(ConsensusFork fork) => new LightClientOptimisticUpdate
        {
            Fork = fork,
            AttestedHeader = BuildHeader(fork),
            SyncAggregate = BuildSyncAggregate(),
            SignatureSlot = 99
        };

        private static List<byte[]> CreateBranch(int length, byte seed)
        {
            var branch = new List<byte[]>(length);
            for (var i = 0; i < length; i++)
                branch.Add(Bytes(SszBasicTypes.RootLength, (byte)(seed + i)));
            return branch;
        }

        private static byte[] Bytes(int length, byte seed)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++) data[i] = (byte)(seed + i);
            return data;
        }
    }
}
