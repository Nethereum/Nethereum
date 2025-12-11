#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Nethereum.Consensus.Ssz.Tests
{
    internal static class ConsensusSpecValueProvider
    {
        private const string BaseRelativePath = "tests/LightClientVectors/ssz/consensus-spec-tests/deneb/ssz_static";
        private const string CaseFolder = "ssz_random";

        public static BeaconBlockHeader LoadBeaconBlockHeader(string caseName) =>
            BuildDeserializer().Deserialize<BeaconBlockHeaderYaml>(ReadYaml("BeaconBlockHeader", caseName))
                .ToModel();

        public static ExecutionPayloadHeader LoadExecutionPayloadHeader(string caseName) =>
            BuildDeserializer().Deserialize<ExecutionPayloadHeaderYaml>(ReadYaml("ExecutionPayloadHeader", caseName))
                .ToModel();

        public static SyncCommittee LoadSyncCommittee(string caseName) =>
            BuildDeserializer().Deserialize<SyncCommitteeYaml>(ReadYaml("SyncCommittee", caseName))
                .ToModel();

        public static SyncAggregate LoadSyncAggregate(string caseName) =>
            BuildDeserializer().Deserialize<SyncAggregateYaml>(ReadYaml("SyncAggregate", caseName))
                .ToModel();

        public static LightClientHeader LoadLightClientHeader(string caseName) =>
            BuildDeserializer().Deserialize<LightClientHeaderYaml>(ReadYaml("LightClientHeader", caseName))
                .ToModel();

        public static LightClientBootstrap LoadLightClientBootstrap(string caseName) =>
            BuildDeserializer().Deserialize<LightClientBootstrapYaml>(ReadYaml("LightClientBootstrap", caseName))
                .ToModel();

        public static LightClientUpdate LoadLightClientUpdate(string caseName) =>
            BuildDeserializer().Deserialize<LightClientUpdateYaml>(ReadYaml("LightClientUpdate", caseName))
                .ToModel();

        public static LightClientFinalityUpdate LoadLightClientFinalityUpdate(string caseName) =>
            BuildDeserializer().Deserialize<LightClientFinalityUpdateYaml>(ReadYaml("LightClientFinalityUpdate", caseName))
                .ToModel();

        public static LightClientOptimisticUpdate LoadLightClientOptimisticUpdate(string caseName) =>
            BuildDeserializer().Deserialize<LightClientOptimisticUpdateYaml>(ReadYaml("LightClientOptimisticUpdate", caseName))
                .ToModel();

        private static string ReadYaml(string container, string caseName)
        {
            var root = RepositoryPath.Root ?? throw new InvalidOperationException("Repository root not found.");
            var basePath = Combine(root, BaseRelativePath);
            var casePath = Path.Combine(basePath, container, caseName, "value.yaml");
            return File.ReadAllText(casePath);
        }

        private static IDeserializer BuildDeserializer()
        {
            return new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new HexByteArrayConverter())
                .WithTypeConverter(new BigIntegerConverter())
                .Build();
        }

        private static string Combine(string root, string relative)
        {
            var segments = relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            foreach (var segment in segments)
            {
                current = Path.Combine(current, segment);
            }

            return current;
        }

        private sealed class HexByteArrayConverter : IYamlTypeConverter
        {
            public bool Accepts(Type type) => type == typeof(byte[]);

            public object ReadYaml(IParser parser, Type type)
            {
                var scalar = parser.Consume<Scalar>();
                var value = scalar.Value ?? string.Empty;
                return value.HexToByteArray();
            }

            public void WriteYaml(IEmitter emitter, object? value, Type type)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class BigIntegerConverter : IYamlTypeConverter
        {
            public bool Accepts(Type type) => type == typeof(BigInteger);

            public object ReadYaml(IParser parser, Type type)
            {
                var scalar = parser.Consume<Scalar>();
                if (BigInteger.TryParse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                // Some YAML emit big integers quoted; fall back to decimal parsing
                return BigInteger.Parse(scalar.Value ?? "0", CultureInfo.InvariantCulture);
            }

            public void WriteYaml(IEmitter emitter, object? value, Type type)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class BeaconBlockHeaderYaml
        {
            public ulong Slot { get; set; }
            public ulong ProposerIndex { get; set; }
            public byte[] ParentRoot { get; set; } = Array.Empty<byte>();
            public byte[] StateRoot { get; set; } = Array.Empty<byte>();
            public byte[] BodyRoot { get; set; } = Array.Empty<byte>();

            public BeaconBlockHeader ToModel()
            {
                return new BeaconBlockHeader
                {
                    Slot = Slot,
                    ProposerIndex = ProposerIndex,
                    ParentRoot = ParentRoot,
                    StateRoot = StateRoot,
                    BodyRoot = BodyRoot
                };
            }
        }

        private sealed class ExecutionPayloadHeaderYaml
        {
            public byte[] ParentHash { get; set; } = Array.Empty<byte>();
            public byte[] FeeRecipient { get; set; } = Array.Empty<byte>();
            public byte[] StateRoot { get; set; } = Array.Empty<byte>();
            public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
            public byte[] LogsBloom { get; set; } = Array.Empty<byte>();
            public byte[] PrevRandao { get; set; } = Array.Empty<byte>();
            public ulong BlockNumber { get; set; }
            public ulong GasLimit { get; set; }
            public ulong GasUsed { get; set; }
            public ulong Timestamp { get; set; }
            public byte[] ExtraData { get; set; } = Array.Empty<byte>();
            public BigInteger BaseFeePerGas { get; set; }
            public byte[] BlockHash { get; set; } = Array.Empty<byte>();
            public byte[] TransactionsRoot { get; set; } = Array.Empty<byte>();
            public byte[] WithdrawalsRoot { get; set; } = Array.Empty<byte>();
            public ulong BlobGasUsed { get; set; }
            public ulong ExcessBlobGas { get; set; }

            public ExecutionPayloadHeader ToModel()
            {
                return new ExecutionPayloadHeader
                {
                    ParentHash = ParentHash,
                    FeeRecipient = FeeRecipient,
                    StateRoot = StateRoot,
                    ReceiptsRoot = ReceiptsRoot,
                    LogsBloom = LogsBloom,
                    PrevRandao = PrevRandao,
                    BlockNumber = BlockNumber,
                    GasLimit = GasLimit,
                    GasUsed = GasUsed,
                    Timestamp = Timestamp,
                    ExtraData = ExtraData,
                    BaseFeePerGas = ToLittleEndian(BaseFeePerGas, 32),
                    BlockHash = BlockHash,
                    TransactionsRoot = TransactionsRoot,
                    WithdrawalsRoot = WithdrawalsRoot,
                    BlobGasUsed = BlobGasUsed,
                    ExcessBlobGas = ExcessBlobGas
                };
            }
        }

        private sealed class SyncCommitteeYaml
        {
            public List<byte[]> Pubkeys { get; set; } = new List<byte[]>();
            public byte[] AggregatePubkey { get; set; } = Array.Empty<byte>();

            public SyncCommittee ToModel()
            {
                return new SyncCommittee
                {
                    PubKeys = Pubkeys,
                    AggregatePubKey = AggregatePubkey
                };
            }
        }

        private sealed class SyncAggregateYaml
        {
            public byte[] SyncCommitteeBits { get; set; } = Array.Empty<byte>();
            public byte[] SyncCommitteeSignature { get; set; } = Array.Empty<byte>();

            public SyncAggregate ToModel()
            {
                return new SyncAggregate
                {
                    SyncCommitteeBits = SyncCommitteeBits,
                    SyncCommitteeSignature = SyncCommitteeSignature
                };
            }
        }

        private sealed class LightClientHeaderYaml
        {
            public BeaconBlockHeaderYaml Beacon { get; set; } = new BeaconBlockHeaderYaml();
            public ExecutionPayloadHeaderYaml Execution { get; set; } = new ExecutionPayloadHeaderYaml();
            public List<byte[]> ExecutionBranch { get; set; } = new List<byte[]>();

            public LightClientHeader ToModel()
            {
                return new LightClientHeader
                {
                    Beacon = Beacon.ToModel(),
                    Execution = Execution.ToModel(),
                    ExecutionBranch = ExecutionBranch
                };
            }
        }

        private sealed class LightClientBootstrapYaml
        {
            public LightClientHeaderYaml Header { get; set; } = new LightClientHeaderYaml();
            public SyncCommitteeYaml CurrentSyncCommittee { get; set; } = new SyncCommitteeYaml();
            public List<byte[]> CurrentSyncCommitteeBranch { get; set; } = new List<byte[]>();

            public LightClientBootstrap ToModel()
            {
                return new LightClientBootstrap
                {
                    Header = Header.ToModel(),
                    CurrentSyncCommittee = CurrentSyncCommittee.ToModel(),
                    CurrentSyncCommitteeBranch = CurrentSyncCommitteeBranch
                };
            }
        }

        private sealed class LightClientUpdateYaml
        {
            public LightClientHeaderYaml AttestedHeader { get; set; } = new LightClientHeaderYaml();
            public SyncCommitteeYaml NextSyncCommittee { get; set; } = new SyncCommitteeYaml();
            public List<byte[]> NextSyncCommitteeBranch { get; set; } = new List<byte[]>();
            public LightClientHeaderYaml FinalizedHeader { get; set; } = new LightClientHeaderYaml();
            public List<byte[]> FinalityBranch { get; set; } = new List<byte[]>();
            public SyncAggregateYaml SyncAggregate { get; set; } = new SyncAggregateYaml();
            public ulong SignatureSlot { get; set; }

            public LightClientUpdate ToModel()
            {
                return new LightClientUpdate
                {
                    AttestedHeader = AttestedHeader.ToModel(),
                    NextSyncCommittee = NextSyncCommittee.ToModel(),
                    NextSyncCommitteeBranch = NextSyncCommitteeBranch,
                    FinalizedHeader = FinalizedHeader.ToModel(),
                    FinalityBranch = FinalityBranch,
                    SyncAggregate = SyncAggregate.ToModel(),
                    SignatureSlot = SignatureSlot
                };
            }
        }

        private sealed class LightClientFinalityUpdateYaml
        {
            public LightClientHeaderYaml AttestedHeader { get; set; } = new LightClientHeaderYaml();
            public LightClientHeaderYaml FinalizedHeader { get; set; } = new LightClientHeaderYaml();
            public List<byte[]> FinalityBranch { get; set; } = new List<byte[]>();
            public SyncAggregateYaml SyncAggregate { get; set; } = new SyncAggregateYaml();
            public ulong SignatureSlot { get; set; }

            public LightClientFinalityUpdate ToModel()
            {
                return new LightClientFinalityUpdate
                {
                    AttestedHeader = AttestedHeader.ToModel(),
                    FinalizedHeader = FinalizedHeader.ToModel(),
                    FinalityBranch = FinalityBranch,
                    SyncAggregate = SyncAggregate.ToModel(),
                    SignatureSlot = SignatureSlot
                };
            }
        }

        private sealed class LightClientOptimisticUpdateYaml
        {
            public LightClientHeaderYaml AttestedHeader { get; set; } = new LightClientHeaderYaml();
            public SyncAggregateYaml SyncAggregate { get; set; } = new SyncAggregateYaml();
            public ulong SignatureSlot { get; set; }

            public LightClientOptimisticUpdate ToModel()
            {
                return new LightClientOptimisticUpdate
                {
                    AttestedHeader = AttestedHeader.ToModel(),
                    SyncAggregate = SyncAggregate.ToModel(),
                    SignatureSlot = SignatureSlot
                };
            }
        }

        private static byte[] ToLittleEndian(BigInteger value, int length)
        {
            var buffer = new byte[length];
            if (!value.TryWriteBytes(buffer, out var written, isUnsigned: true, isBigEndian: false))
            {
                throw new InvalidOperationException("Failed to write BigInteger.");
            }

            if (written > length)
            {
                throw new InvalidOperationException($"Value does not fit in {length} bytes.");
            }

            return buffer;
        }
    }
}
