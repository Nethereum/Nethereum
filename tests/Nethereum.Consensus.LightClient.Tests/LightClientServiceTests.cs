using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Signer.Bls;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class LightClientServiceTests
    {
        [Fact]
        public async Task InitializeAsync_PopulatesStateAndPersists()
        {
            var bootstrap = TestDataFactory.CreateBootstrap(slot: 1, blockNumber: 100);
            var beaconClient = new StubLightClientApi(bootstrap, Array.Empty<LightClientUpdate>());
            var config = TestDataFactory.CreateConfig();
            var store = new InMemoryLightClientStore();
            var bls = new StubBls();

            var service = new LightClientService(beaconClient, bls, config, store);

            await service.InitializeAsync();

            var state = service.GetState();
            Assert.NotNull(state);
            Assert.Equal(bootstrap.Header.Beacon.Slot, state.FinalizedSlot);
            Assert.Equal(bootstrap.Header.Execution.BlockNumber, state.FinalizedExecutionPayload?.BlockNumber);
        }

        [Fact]
        public async Task UpdateAsync_VerifiesAggregateAndUpdatesState()
        {
            var bootstrap = TestDataFactory.CreateBootstrap(slot: 1, blockNumber: 100);
            var update = TestDataFactory.CreateUpdate(slot: 2, blockNumber: 101);
            var beaconClient = new StubLightClientApi(bootstrap, new[] { update });
            var config = TestDataFactory.CreateConfig();
            var store = new InMemoryLightClientStore();
            var bls = new StubBls { VerificationResult = true };

            var service = new LightClientService(beaconClient, bls, config, store);

            await service.InitializeAsync();
            var updated = await service.UpdateAsync();

            Assert.True(updated);
            var state = service.GetState();
            Assert.Equal(update.FinalizedHeader.Beacon.Slot, state.FinalizedSlot);
            Assert.Equal(update.FinalizedHeader.Execution.BlockNumber, state.FinalizedExecutionPayload?.BlockNumber);
            Assert.NotNull(bls.LastDomain);
            Assert.Single(bls.LastMessages);
            Assert.Equal(TestDataFactory.ExpectedDomain(config), bls.LastDomain);
            Assert.Equal(TestDataFactory.ExpectedSigningRoot(update.AttestedHeader.Beacon, TestDataFactory.ExpectedDomain(config)), bls.LastMessages[0]);
            Assert.Equal(2, bls.LastPublicKeys.Length);
        }

        private sealed class StubLightClientApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;
            private readonly IReadOnlyList<LightClientUpdate> _updates;

            public StubLightClientApi(LightClientBootstrap bootstrap, IReadOnlyList<LightClientUpdate> updates)
            {
                _bootstrap = bootstrap;
                _updates = updates;
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            {
                var response = TestDataFactory.CreateBootstrapResponse(_bootstrap);
                return Task.FromResult(response);
            }

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count)
            {
                var responses = _updates.Select(TestDataFactory.CreateUpdateResponse).ToList();
                return Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(responses);
            }

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);
        }

        private sealed class StubBls : IBls
        {
            public bool VerificationResult { get; set; } = true;
            public byte[] LastAggregateSignature { get; private set; }
            public byte[][] LastPublicKeys { get; private set; } = Array.Empty<byte[]>();
            public byte[][] LastMessages { get; private set; } = Array.Empty<byte[]>();
            public byte[] LastDomain { get; private set; } = Array.Empty<byte>();

            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain)
            {
                LastAggregateSignature = aggregateSignature;
                LastPublicKeys = publicKeys;
                LastMessages = messages;
                LastDomain = domain;
                return VerificationResult;
            }
        }

        private static class TestDataFactory
        {
            public static LightClientConfig CreateConfig()
            {
                return new LightClientConfig
                {
                    GenesisValidatorsRoot = Enumerable.Repeat((byte)0xAA, SszBasicTypes.RootLength).ToArray(),
                    CurrentForkVersion = new byte[] { 0x11, 0x22, 0x33, 0x44 },
                    SlotsPerEpoch = 32,
                    SecondsPerSlot = 12,
                    WeakSubjectivityRoot = Enumerable.Repeat((byte)0xBB, SszBasicTypes.RootLength).ToArray(),
                    WeakSubjectivityPeriod = 256 * 32UL
                };
            }

            public static LightClientBootstrap CreateBootstrap(ulong slot, ulong blockNumber)
            {
                return new LightClientBootstrap
                {
                    Header = CreateHeader(slot, blockNumber),
                    CurrentSyncCommittee = CreateSyncCommittee(),
                    CurrentSyncCommitteeBranch = CreateBranch(SszBasicTypes.CurrentSyncCommitteeBranchLength)
                };
            }

            public static LightClientUpdate CreateUpdate(ulong slot, ulong blockNumber)
            {
                return new LightClientUpdate
                {
                    AttestedHeader = CreateHeader(slot, blockNumber),
                    FinalizedHeader = CreateHeader(slot, blockNumber),
                    SyncAggregate = CreateSyncAggregate(),
                    NextSyncCommittee = CreateSyncCommittee(),
                    FinalityBranch = CreateBranch(SszBasicTypes.FinalityBranchLength),
                    NextSyncCommitteeBranch = CreateBranch(SszBasicTypes.CurrentSyncCommitteeBranchLength),
                    SignatureSlot = slot + 1
                };
            }

            public static LightClientBootstrapResponse CreateBootstrapResponse(LightClientBootstrap bootstrap)
            {
                return new LightClientBootstrapResponse
                {
                    Version = "deneb",
                    Data = new LightClientBootstrapData
                    {
                        Header = CreateHeaderDto(bootstrap.Header),
                        CurrentSyncCommittee = CreateSyncCommitteeDto(bootstrap.CurrentSyncCommittee),
                        CurrentSyncCommitteeBranch = bootstrap.CurrentSyncCommitteeBranch.Select(ToHex).ToList()
                    }
                };
            }

            public static LightClientUpdateResponse CreateUpdateResponse(LightClientUpdate update)
            {
                return new LightClientUpdateResponse
                {
                    Version = "deneb",
                    Data = new LightClientUpdateData
                    {
                        AttestedHeader = CreateHeaderDto(update.AttestedHeader),
                        FinalizedHeader = CreateHeaderDto(update.FinalizedHeader),
                        SyncAggregate = CreateSyncAggregateDto(update.SyncAggregate),
                        NextSyncCommittee = CreateSyncCommitteeDto(update.NextSyncCommittee),
                        FinalityBranch = update.FinalityBranch.Select(ToHex).ToList(),
                        NextSyncCommitteeBranch = update.NextSyncCommitteeBranch.Select(ToHex).ToList(),
                        SignatureSlot = update.SignatureSlot.ToString()
                    }
                };
            }

            private static LightClientHeaderDto CreateHeaderDto(LightClientHeader header)
            {
                return new LightClientHeaderDto
                {
                    Beacon = new BeaconBlockHeaderDto
                    {
                        Slot = header.Beacon.Slot.ToString(),
                        ProposerIndex = header.Beacon.ProposerIndex.ToString(),
                        ParentRoot = ToHex(header.Beacon.ParentRoot),
                        StateRoot = ToHex(header.Beacon.StateRoot),
                        BodyRoot = ToHex(header.Beacon.BodyRoot)
                    },
                    Execution = new ExecutionPayloadHeaderDto
                    {
                        ParentHash = ToHex(header.Execution.ParentHash),
                        FeeRecipient = ToHex(header.Execution.FeeRecipient),
                        StateRoot = ToHex(header.Execution.StateRoot),
                        ReceiptsRoot = ToHex(header.Execution.ReceiptsRoot),
                        LogsBloom = ToHex(header.Execution.LogsBloom),
                        PrevRandao = ToHex(header.Execution.PrevRandao),
                        BlockNumber = header.Execution.BlockNumber.ToString(),
                        GasLimit = header.Execution.GasLimit.ToString(),
                        GasUsed = header.Execution.GasUsed.ToString(),
                        Timestamp = header.Execution.Timestamp.ToString(),
                        ExtraData = ToHex(header.Execution.ExtraData),
                        BaseFeePerGas = "0",
                        BlockHash = ToHex(header.Execution.BlockHash),
                        TransactionsRoot = ToHex(header.Execution.TransactionsRoot),
                        WithdrawalsRoot = ToHex(header.Execution.WithdrawalsRoot),
                        BlobGasUsed = header.Execution.BlobGasUsed.ToString(),
                        ExcessBlobGas = header.Execution.ExcessBlobGas.ToString()
                    },
                    ExecutionBranch = header.ExecutionBranch.Select(ToHex).ToList()
                };
            }

            private static SyncCommitteeDto CreateSyncCommitteeDto(SyncCommittee committee)
            {
                return new SyncCommitteeDto
                {
                    PubKeys = committee.PubKeys.Select(ToHex).ToList(),
                    AggregatePubKey = ToHex(committee.AggregatePubKey)
                };
            }

            private static SyncAggregateDto CreateSyncAggregateDto(SyncAggregate aggregate)
            {
                return new SyncAggregateDto
                {
                    SyncCommitteeBits = ToHex(aggregate.SyncCommitteeBits),
                    SyncCommitteeSignature = ToHex(aggregate.SyncCommitteeSignature)
                };
            }

            private static string ToHex(byte[] bytes) => "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            public static byte[] ExpectedDomain(LightClientConfig config)
            {
                var domain = new byte[32];
                var type = new byte[] { 0x07, 0x00, 0x00, 0x00 };
                Buffer.BlockCopy(type, 0, domain, 0, type.Length);
                var forkDataRoot = ComputeForkDataRoot(config.CurrentForkVersion, config.GenesisValidatorsRoot);
                Buffer.BlockCopy(forkDataRoot, 0, domain, type.Length, Math.Min(28, forkDataRoot.Length));
                return domain;
            }

            public static byte[] ExpectedSigningRoot(BeaconBlockHeader header, byte[] domain)
            {
                var fieldRoots = new[]
                {
                    SszBasicTypes.HashTreeRootFixedBytes(header.HashTreeRoot(), SszBasicTypes.RootLength),
                    SszBasicTypes.HashTreeRootFixedBytes(domain, domain.Length)
                };
                return SszMerkleizer.Merkleize(fieldRoots);
            }

            private static LightClientHeader CreateHeader(ulong slot, ulong blockNumber)
            {
                return new LightClientHeader
                {
                    Beacon = new BeaconBlockHeader
                    {
                        Slot = slot,
                        ProposerIndex = 1,
                        ParentRoot = Enumerable.Repeat((byte)0x01, SszBasicTypes.RootLength).ToArray(),
                        StateRoot = Enumerable.Repeat((byte)0x02, SszBasicTypes.RootLength).ToArray(),
                        BodyRoot = Enumerable.Repeat((byte)0x03, SszBasicTypes.RootLength).ToArray()
                    },
                    Execution = new ExecutionPayloadHeader
                    {
                        ParentHash = Enumerable.Repeat((byte)0x04, SszBasicTypes.RootLength).ToArray(),
                        FeeRecipient = Enumerable.Repeat((byte)0x05, 20).ToArray(),
                        StateRoot = Enumerable.Repeat((byte)0x06, SszBasicTypes.RootLength).ToArray(),
                        ReceiptsRoot = Enumerable.Repeat((byte)0x07, SszBasicTypes.RootLength).ToArray(),
                        LogsBloom = Enumerable.Repeat((byte)0x08, SszBasicTypes.LogsBloomLength).ToArray(),
                        PrevRandao = Enumerable.Repeat((byte)0x09, SszBasicTypes.RootLength).ToArray(),
                        BlockNumber = blockNumber,
                        GasLimit = 30_000_000,
                        GasUsed = 1_000_000,
                        Timestamp = 123456,
                        ExtraData = new byte[] { 0xAA, 0xBB },
                        BaseFeePerGas = Enumerable.Repeat((byte)0x10, SszBasicTypes.RootLength).ToArray(),
                        BlockHash = Enumerable.Repeat((byte)0x11, SszBasicTypes.RootLength).ToArray(),
                        TransactionsRoot = Enumerable.Repeat((byte)0x12, SszBasicTypes.RootLength).ToArray(),
                        WithdrawalsRoot = Enumerable.Repeat((byte)0x13, SszBasicTypes.RootLength).ToArray(),
                        BlobGasUsed = 0,
                        ExcessBlobGas = 0
                    },
                    ExecutionBranch = CreateBranch(SszBasicTypes.ExecutionBranchLength)
                };
            }

            private static SyncCommittee CreateSyncCommittee()
            {
                var pubkeys = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
                for (var i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
                {
                    var key = new byte[SszBasicTypes.PubKeyLength];
                    for (var j = 0; j < key.Length; j++)
                    {
                        key[j] = (byte)(i % 256);
                    }
                    pubkeys.Add(key);
                }

                return new SyncCommittee
                {
                    PubKeys = pubkeys,
                    AggregatePubKey = Enumerable.Repeat((byte)0x42, SszBasicTypes.PubKeyLength).ToArray()
                };
            }

            private static SyncAggregate CreateSyncAggregate()
            {
                var bits = new byte[SszBasicTypes.SyncCommitteeSize / 8];
                bits[0] = 0b00000011;

                return new SyncAggregate
                {
                    SyncCommitteeBits = bits,
                    SyncCommitteeSignature = Enumerable.Repeat((byte)0x33, SszBasicTypes.SignatureLength).ToArray()
                };
            }

            private static List<byte[]> CreateBranch(int length)
            {
                var branch = new List<byte[]>(length);
                for (var i = 0; i < length; i++)
                {
                    var root = new byte[SszBasicTypes.RootLength];
                    for (var j = 0; j < root.Length; j++)
                    {
                        root[j] = (byte)(i + 1);
                    }
                    branch.Add(root);
                }
                return branch;
            }

            private static byte[] ComputeForkDataRoot(byte[] version, byte[] genesisRoot)
            {
                var forkVersion = new byte[4];
                if (version != null)
                {
                    Buffer.BlockCopy(version, 0, forkVersion, 0, Math.Min(4, version.Length));
                }

                var genesis = new byte[SszBasicTypes.RootLength];
                if (genesisRoot != null)
                {
                    Buffer.BlockCopy(genesisRoot, 0, genesis, 0, Math.Min(SszBasicTypes.RootLength, genesisRoot.Length));
                }

                var fieldRoots = new[]
                {
                    SszBasicTypes.HashTreeRootFixedBytes(forkVersion, forkVersion.Length),
                    SszBasicTypes.HashTreeRootFixedBytes(genesis, genesis.Length)
                };

                return SszMerkleizer.Merkleize(fieldRoots);
            }
        }
    }
}
