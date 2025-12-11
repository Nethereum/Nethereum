using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.Ssz.Tests
{
    public class SszContainerTests
    {
        private readonly ITestOutputHelper _output;

        public SszContainerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BeaconBlockHeader_RoundTrip()
        {
            var header = SampleData.CreateBeaconHeader();
            var encoded = header.Encode();
            var decoded = BeaconBlockHeader.Decode(encoded);

            Assert.Equal(header.Slot, decoded.Slot);
            Assert.Equal(header.ProposerIndex, decoded.ProposerIndex);
            Assert.Equal(header.ParentRoot, decoded.ParentRoot);
            Assert.Equal(header.StateRoot, decoded.StateRoot);
            Assert.Equal(header.BodyRoot, decoded.BodyRoot);
            Assert.Equal(header.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void ExecutionPayloadHeader_RoundTrip()
        {
            var header = SampleData.CreateExecutionHeader();
            var encoded = header.Encode();
            var decoded = ExecutionPayloadHeader.Decode(encoded);

            Assert.Equal(header.ParentHash, decoded.ParentHash);
            Assert.Equal(header.FeeRecipient, decoded.FeeRecipient);
            Assert.Equal(header.LogsBloom, decoded.LogsBloom);
            Assert.Equal(header.Timestamp, decoded.Timestamp);
            Assert.Equal(header.ExtraData, decoded.ExtraData);
            Assert.Equal(header.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void SyncCommittee_RoundTrip()
        {
            var committee = SampleData.CreateSyncCommittee();
            var encoded = committee.Encode();
            var decoded = SyncCommittee.Decode(encoded);

            Assert.Equal(committee.PubKeys.Count, decoded.PubKeys.Count);
            for (var i = 0; i < committee.PubKeys.Count; i++)
            {
                Assert.Equal(committee.PubKeys[i], decoded.PubKeys[i]);
            }

            Assert.Equal(committee.AggregatePubKey, decoded.AggregatePubKey);
            Assert.Equal(committee.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void SyncAggregate_RoundTrip()
        {
            var aggregate = SampleData.CreateSyncAggregate();
            var encoded = aggregate.Encode();
            var decoded = SyncAggregate.Decode(encoded);

            Assert.Equal(aggregate.SyncCommitteeBits, decoded.SyncCommitteeBits);
            Assert.Equal(aggregate.SyncCommitteeSignature, decoded.SyncCommitteeSignature);
            Assert.Equal(aggregate.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void LightClientBootstrap_RoundTrip()
        {
            var bootstrap = SampleData.CreateBootstrap();
            var encoded = bootstrap.Encode();
            var decoded = LightClientBootstrap.Decode(encoded);

            AssertLightClientHeaderEqual(bootstrap.Header, decoded.Header);
            Assert.Equal(bootstrap.CurrentSyncCommittee.HashTreeRoot(), decoded.CurrentSyncCommittee.HashTreeRoot());
            AssertBranchesEqual(bootstrap.CurrentSyncCommitteeBranch, decoded.CurrentSyncCommitteeBranch);
            Assert.Equal(bootstrap.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void LightClientHeader_RoundTrip()
        {
            var header = SampleData.CreateLightClientHeader();
            var encoded = header.Encode();
            var decoded = LightClientHeader.Decode(encoded);

            AssertLightClientHeaderEqual(header, decoded);
            Assert.Equal(header.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void LightClientFinalityUpdate_RoundTrip()
        {
            var update = SampleData.CreateFinalityUpdate();
            var encoded = update.Encode();
            var decoded = LightClientFinalityUpdate.Decode(encoded);

            AssertLightClientHeaderEqual(update.AttestedHeader, decoded.AttestedHeader);
            AssertLightClientHeaderEqual(update.FinalizedHeader, decoded.FinalizedHeader);
            AssertBranchesEqual(update.FinalityBranch, decoded.FinalityBranch);
            Assert.Equal(update.SyncAggregate.HashTreeRoot(), decoded.SyncAggregate.HashTreeRoot());
            Assert.Equal(update.SignatureSlot, decoded.SignatureSlot);
            Assert.Equal(update.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void LightClientOptimisticUpdate_RoundTrip()
        {
            var update = SampleData.CreateOptimisticUpdate();
            var encoded = update.Encode();
            var decoded = LightClientOptimisticUpdate.Decode(encoded);

            AssertLightClientHeaderEqual(update.AttestedHeader, decoded.AttestedHeader);
            Assert.Equal(update.SyncAggregate.HashTreeRoot(), decoded.SyncAggregate.HashTreeRoot());
            Assert.Equal(update.SignatureSlot, decoded.SignatureSlot);
            Assert.Equal(update.HashTreeRoot(), decoded.HashTreeRoot());

        }

        [Fact]
        public void LightClientUpdate_RoundTrip()
        {
            var update = SampleData.CreateUpdate();
            var encoded = update.Encode();
            var decoded = LightClientUpdate.Decode(encoded);

            AssertLightClientHeaderEqual(update.AttestedHeader, decoded.AttestedHeader);
            Assert.Equal(update.NextSyncCommittee.HashTreeRoot(), decoded.NextSyncCommittee.HashTreeRoot());
            AssertBranchesEqual(update.NextSyncCommitteeBranch, decoded.NextSyncCommitteeBranch);
            AssertLightClientHeaderEqual(update.FinalizedHeader, decoded.FinalizedHeader);
            AssertBranchesEqual(update.FinalityBranch, decoded.FinalityBranch);
            Assert.Equal(update.SyncAggregate.HashTreeRoot(), decoded.SyncAggregate.HashTreeRoot());
            Assert.Equal(update.SignatureSlot, decoded.SignatureSlot);
            Assert.Equal(update.HashTreeRoot(), decoded.HashTreeRoot());

        }

        private static void AssertBranchesEqual(IList<byte[]> expected, IList<byte[]> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
        [Theory]
        [MemberData(nameof(BeaconBlockHeaderSpecVectors))]
        public void BeaconBlockHeader_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var header = BeaconBlockHeader.Decode(testCase.SerializedSpan);
            Assert.Equal(testCase.Serialized, header.Encode());
            Assert.Equal(testCase.Root, header.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadBeaconBlockHeader(testCase.CaseName);
            AssertBeaconHeaderEqual(expected, header);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(ExecutionPayloadHeaderSpecVectors))]
        public void ExecutionPayloadHeader_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var header = ExecutionPayloadHeader.Decode(testCase.SerializedSpan);
            Assert.Equal(testCase.Serialized, header.Encode());
            Assert.Equal(testCase.Root, header.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadExecutionPayloadHeader(testCase.CaseName);
            AssertExecutionHeaderEqual(expected, header);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(SyncCommitteeSpecVectors))]
        public void SyncCommittee_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var committee = SyncCommittee.Decode(testCase.SerializedSpan);
            Assert.Equal(testCase.Serialized, committee.Encode());
            Assert.Equal(testCase.Root, committee.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadSyncCommittee(testCase.CaseName);
            AssertSyncCommitteeEqual(expected, committee);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(SyncAggregateSpecVectors))]
        public void SyncAggregate_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var aggregate = SyncAggregate.Decode(testCase.SerializedSpan);
            Assert.Equal(testCase.Serialized, aggregate.Encode());
            Assert.Equal(testCase.Root, aggregate.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadSyncAggregate(testCase.CaseName);
            AssertSyncAggregateEqual(expected, aggregate);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(LightClientBootstrapSpecVectors))]
        public void LightClientBootstrap_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var bootstrap = LightClientBootstrap.Decode(testCase.Serialized);
            var encoded = bootstrap.Encode();
            Assert.Equal(testCase.Serialized, encoded);
            Assert.Equal(testCase.Root, bootstrap.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadLightClientBootstrap(testCase.CaseName);
            AssertLightClientHeaderEqual(expected.Header, bootstrap.Header);
            AssertSyncCommitteeEqual(expected.CurrentSyncCommittee, bootstrap.CurrentSyncCommittee);
            AssertBranchesEqual(expected.CurrentSyncCommitteeBranch, bootstrap.CurrentSyncCommitteeBranch);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(LightClientHeaderSpecVectors))]
        public void LightClientHeader_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var header = LightClientHeader.Decode(testCase.Serialized);
            var encoded = header.Encode();
            Assert.Equal(testCase.Serialized, encoded);
            Assert.Equal(testCase.Root, header.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadLightClientHeader(testCase.CaseName);
            AssertLightClientHeaderEqual(expected, header);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(LightClientFinalityUpdateSpecVectors))]
        public void LightClientFinalityUpdate_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var update = LightClientFinalityUpdate.Decode(testCase.Serialized);
            Assert.Equal(testCase.Serialized, update.Encode());
            Assert.Equal(testCase.Root, update.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadLightClientFinalityUpdate(testCase.CaseName);
            AssertLightClientHeaderEqual(expected.AttestedHeader, update.AttestedHeader);
            AssertLightClientHeaderEqual(expected.FinalizedHeader, update.FinalizedHeader);
            AssertBranchesEqual(expected.FinalityBranch, update.FinalityBranch);
            AssertSyncAggregateEqual(expected.SyncAggregate, update.SyncAggregate);
            Assert.Equal(expected.SignatureSlot, update.SignatureSlot);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(LightClientOptimisticUpdateSpecVectors))]
        public void LightClientOptimisticUpdate_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            var update = LightClientOptimisticUpdate.Decode(testCase.Serialized);
            Assert.Equal(testCase.Serialized, update.Encode());
            Assert.Equal(testCase.Root, update.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadLightClientOptimisticUpdate(testCase.CaseName);
            AssertLightClientHeaderEqual(expected.AttestedHeader, update.AttestedHeader);
            AssertSyncAggregateEqual(expected.SyncAggregate, update.SyncAggregate);
            Assert.Equal(expected.SignatureSlot, update.SignatureSlot);
            LogConsensusCase(testCase);
        }

        [Theory]
        [MemberData(nameof(LightClientUpdateSpecVectors))]
        public void LightClientUpdate_ConsensusSpecVectors(ConsensusSpecTestCase testCase)
        {
            if (testCase.CaseName.EndsWith("case_0", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine($"expected start {HexPrefix(testCase.Serialized, 8)}");
            }
            var update = LightClientUpdate.Decode(testCase.Serialized);
            Assert.Equal(testCase.Serialized, update.Encode());
            Assert.Equal(testCase.Root, update.HashTreeRoot());
            var expected = ConsensusSpecValueProvider.LoadLightClientUpdate(testCase.CaseName);
            AssertLightClientHeaderEqual(expected.AttestedHeader, update.AttestedHeader);
            AssertSyncCommitteeEqual(expected.NextSyncCommittee, update.NextSyncCommittee);
            AssertBranchesEqual(expected.NextSyncCommitteeBranch, update.NextSyncCommitteeBranch);
            AssertLightClientHeaderEqual(expected.FinalizedHeader, update.FinalizedHeader);
            AssertBranchesEqual(expected.FinalityBranch, update.FinalityBranch);
            AssertSyncAggregateEqual(expected.SyncAggregate, update.SyncAggregate);
            Assert.Equal(expected.SignatureSlot, update.SignatureSlot);
            LogConsensusCase(testCase);
        }

        public static IEnumerable<object[]> BeaconBlockHeaderSpecVectors() => LoadConsensusCases("BeaconBlockHeader");
        public static IEnumerable<object[]> ExecutionPayloadHeaderSpecVectors() => LoadConsensusCases("ExecutionPayloadHeader");
        public static IEnumerable<object[]> SyncCommitteeSpecVectors() => LoadConsensusCases("SyncCommittee");
        public static IEnumerable<object[]> SyncAggregateSpecVectors() => LoadConsensusCases("SyncAggregate");
        public static IEnumerable<object[]> LightClientBootstrapSpecVectors() => LoadConsensusCases("LightClientBootstrap");
        public static IEnumerable<object[]> LightClientHeaderSpecVectors() => LoadConsensusCases("LightClientHeader");
        public static IEnumerable<object[]> LightClientFinalityUpdateSpecVectors() => LoadConsensusCases("LightClientFinalityUpdate");
        public static IEnumerable<object[]> LightClientOptimisticUpdateSpecVectors() => LoadConsensusCases("LightClientOptimisticUpdate");
        public static IEnumerable<object[]> LightClientUpdateSpecVectors() => LoadConsensusCases("LightClientUpdate");

        private static IEnumerable<object[]> LoadConsensusCases(string container)
        {
            foreach (var testCase in ConsensusSpecTestCaseProvider.Load(container))
            {
                yield return new object[] { testCase };
            }
        }

        private void LogConsensusCase(ConsensusSpecTestCase testCase)
        {
            _output.WriteLine($"consensus-spec vector processed: {testCase.DisplayName}");
        }

        [Fact]
        public void ExecutionPayloadHeader_YamlEncodeMatchesVector_FirstCase()
        {
            var cases = ConsensusSpecTestCaseProvider.Load("ExecutionPayloadHeader").ToList();
            if (cases.Count == 0)
            {
                _output.WriteLine("No consensus spec cases found for ExecutionPayloadHeader.");
                return;
            }

            var first = cases[0];
            var model = ConsensusSpecValueProvider.LoadExecutionPayloadHeader(first.CaseName);
            var encoded = model.Encode();

            _output.WriteLine($"Case: {first.DisplayName}");
            _output.WriteLine($"Vector length: {first.Serialized.Length}");
            _output.WriteLine($"Encoded length: {encoded.Length}");
            var diff = FirstDiffIndex(first.Serialized, encoded);
            _output.WriteLine($"First differing index: {diff}");
            _output.WriteLine($"Vector head: {HexPrefix(first.Serialized, 64)}");
            _output.WriteLine($"Encoded head: {HexPrefix(encoded, 64)}");

            Assert.Equal(first.Serialized, encoded);
        }

        [Fact]
        public void SyncCommittee_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("SyncCommittee", name => ConsensusSpecValueProvider.LoadSyncCommittee(name).Encode());

        [Fact]
        public void SyncAggregate_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("SyncAggregate", name => ConsensusSpecValueProvider.LoadSyncAggregate(name).Encode());

        [Fact]
        public void LightClientHeader_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("LightClientHeader", name => ConsensusSpecValueProvider.LoadLightClientHeader(name).Encode());

        [Fact]
        public void LightClientBootstrap_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("LightClientBootstrap", name => ConsensusSpecValueProvider.LoadLightClientBootstrap(name).Encode());

        [Fact]
        public void LightClientUpdate_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("LightClientUpdate", name => ConsensusSpecValueProvider.LoadLightClientUpdate(name).Encode());

        [Fact]
        public void LightClientFinalityUpdate_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("LightClientFinalityUpdate", name => ConsensusSpecValueProvider.LoadLightClientFinalityUpdate(name).Encode());

        [Fact]
        public void LightClientOptimisticUpdate_YamlEncodeMatchesVector_FirstCase() =>
            AssertFirstCaseEncodingMatches("LightClientOptimisticUpdate", name => ConsensusSpecValueProvider.LoadLightClientOptimisticUpdate(name).Encode());

        private void AssertFirstCaseEncodingMatches(string containerName, Func<string, byte[]> encodeFromYaml)
        {
            var cases = ConsensusSpecTestCaseProvider.Load(containerName).ToList();
            if (cases.Count == 0)
            {
                _output.WriteLine($"No consensus spec cases found for {containerName}.");
                return;
            }

            var first = cases[0];
            var encoded = encodeFromYaml(first.CaseName);
            Assert.Equal(first.Serialized, encoded);
        }

        private static string HexPrefix(byte[] data, int count)
        {
            var length = Math.Min(count, data.Length);
            return BitConverter.ToString(data, 0, length);
        }

        private static void AssertBeaconHeaderEqual(BeaconBlockHeader expected, BeaconBlockHeader actual)
        {
            Assert.Equal(expected.Slot, actual.Slot);
            Assert.Equal(expected.ProposerIndex, actual.ProposerIndex);
            Assert.Equal(expected.ParentRoot, actual.ParentRoot);
            Assert.Equal(expected.StateRoot, actual.StateRoot);
            Assert.Equal(expected.BodyRoot, actual.BodyRoot);
        }

        private static void AssertExecutionHeaderEqual(ExecutionPayloadHeader expected, ExecutionPayloadHeader actual)
        {
            Assert.Equal(expected.ParentHash, actual.ParentHash);
            Assert.Equal(expected.FeeRecipient, actual.FeeRecipient);
            Assert.Equal(expected.StateRoot, actual.StateRoot);
            Assert.Equal(expected.ReceiptsRoot, actual.ReceiptsRoot);
            Assert.Equal(expected.LogsBloom, actual.LogsBloom);
            Assert.Equal(expected.PrevRandao, actual.PrevRandao);
            Assert.Equal(expected.BlockNumber, actual.BlockNumber);
            Assert.Equal(expected.GasLimit, actual.GasLimit);
            Assert.Equal(expected.GasUsed, actual.GasUsed);
            Assert.Equal(expected.Timestamp, actual.Timestamp);
            Assert.Equal(expected.ExtraData, actual.ExtraData);
            Assert.Equal(expected.BaseFeePerGas, actual.BaseFeePerGas);
            Assert.Equal(expected.BlockHash, actual.BlockHash);
            Assert.Equal(expected.TransactionsRoot, actual.TransactionsRoot);
            Assert.Equal(expected.WithdrawalsRoot, actual.WithdrawalsRoot);
            Assert.Equal(expected.BlobGasUsed, actual.BlobGasUsed);
            Assert.Equal(expected.ExcessBlobGas, actual.ExcessBlobGas);
        }

        private static void AssertLightClientHeaderEqual(LightClientHeader expected, LightClientHeader actual)
        {
            AssertBeaconHeaderEqual(expected.Beacon, actual.Beacon);
            AssertExecutionHeaderEqual(expected.Execution, actual.Execution);
            AssertBranchesEqual(expected.ExecutionBranch, actual.ExecutionBranch);
        }

        private static void AssertSyncCommitteeEqual(SyncCommittee expected, SyncCommittee actual)
        {
            Assert.Equal(expected.PubKeys.Count, actual.PubKeys.Count);
            for (var i = 0; i < expected.PubKeys.Count; i++)
            {
                Assert.Equal(expected.PubKeys[i], actual.PubKeys[i]);
            }
            Assert.Equal(expected.AggregatePubKey, actual.AggregatePubKey);
        }

        private static void AssertSyncAggregateEqual(SyncAggregate expected, SyncAggregate actual)
        {
            Assert.Equal(expected.SyncCommitteeBits, actual.SyncCommitteeBits);
            Assert.Equal(expected.SyncCommitteeSignature, actual.SyncCommitteeSignature);
        }

        private static int FirstDiffIndex(byte[] expected, byte[] actual)
        {
            var length = Math.Min(expected.Length, actual.Length);
            for (var i = 0; i < length; i++)
            {
                if (expected[i] != actual[i]) return i;
            }

            return expected.Length == actual.Length ? -1 : length;
        }

    }

    internal static class SampleData
    {
        public static BeaconBlockHeader CreateBeaconHeader()
        {
            return new BeaconBlockHeader
            {
                Slot = 1234,
                ProposerIndex = 42,
                ParentRoot = Bytes(32, 0x10),
                StateRoot = Bytes(32, 0x20),
                BodyRoot = Bytes(32, 0x30)
            };
        }

        public static ExecutionPayloadHeader CreateExecutionHeader()
        {
            return new ExecutionPayloadHeader
            {
                ParentHash = Bytes(32, 0x01),
                FeeRecipient = Bytes(20, 0x02),
                StateRoot = Bytes(32, 0x03),
                ReceiptsRoot = Bytes(32, 0x04),
                LogsBloom = Bytes(SszBasicTypes.LogsBloomLength, 0x05),
                PrevRandao = Bytes(32, 0x06),
                BlockNumber = 555,
                GasLimit = 16_000_000,
                GasUsed = 15_000_000,
                Timestamp = 1_694_000_123,
                ExtraData = Bytes(12, 0x07),
                BaseFeePerGas = Bytes(32, 0x08),
                BlockHash = Bytes(32, 0x09),
                TransactionsRoot = Bytes(32, 0x0A),
                WithdrawalsRoot = Bytes(32, 0x0B),
                BlobGasUsed = 1024,
                ExcessBlobGas = 2048
            };
        }

        public static SyncCommittee CreateSyncCommittee()
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

        public static SyncAggregate CreateSyncAggregate()
        {
            return new SyncAggregate
            {
                SyncCommitteeBits = Bytes(SszBasicTypes.SyncCommitteeSize / 8, 0xCC),
                SyncCommitteeSignature = Bytes(SszBasicTypes.SignatureLength, 0xDD)
            };
        }

        public static LightClientBootstrap CreateBootstrap()
        {
            return new LightClientBootstrap
            {
                Header = CreateLightClientHeader(0x60),
                CurrentSyncCommittee = CreateSyncCommittee(),
                CurrentSyncCommitteeBranch = CreateBranch(SszBasicTypes.CurrentSyncCommitteeBranchLength, 0x50)
            };
        }

        public static LightClientUpdate CreateUpdate()
        {
            return new LightClientUpdate
            {
                AttestedHeader = CreateLightClientHeader(0x70),
                NextSyncCommittee = CreateSyncCommittee(),
                NextSyncCommitteeBranch = CreateBranch(SszBasicTypes.CurrentSyncCommitteeBranchLength, 0x70),
                FinalizedHeader = CreateLightClientHeader(0x80),
                FinalityBranch = CreateBranch(SszBasicTypes.FinalityBranchLength, 0x80),
                SyncAggregate = CreateSyncAggregate(),
                SignatureSlot = 987654
            };
        }

        public static LightClientHeader CreateLightClientHeader(byte branchSeed = 0x90)
        {
            return new LightClientHeader
            {
                Beacon = CreateBeaconHeader(),
                Execution = CreateExecutionHeader(),
                ExecutionBranch = CreateBranch(SszBasicTypes.ExecutionBranchLength, branchSeed)
            };
        }

        public static LightClientFinalityUpdate CreateFinalityUpdate()
        {
            return new LightClientFinalityUpdate
            {
                AttestedHeader = CreateLightClientHeader(0xA0),
                FinalizedHeader = CreateLightClientHeader(0xB0),
                FinalityBranch = CreateBranch(SszBasicTypes.FinalityBranchLength, 0xA0),
                SyncAggregate = CreateSyncAggregate(),
                SignatureSlot = 456789
            };
        }

        public static LightClientOptimisticUpdate CreateOptimisticUpdate()
        {
            return new LightClientOptimisticUpdate
            {
                AttestedHeader = CreateLightClientHeader(0xC0),
                SyncAggregate = CreateSyncAggregate(),
                SignatureSlot = 222333
            };
        }

        private static List<byte[]> CreateBranch(int length, byte seed)
        {
            var branch = new List<byte[]>(length);
            for (var i = 0; i < length; i++)
            {
                branch.Add(Bytes(SszBasicTypes.RootLength, (byte)(seed + i)));
            }

            return branch;
        }

        private static byte[] Bytes(int length, byte seed)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++)
            {
                data[i] = (byte)(seed + i);
            }

            return data;
        }
    }
}
