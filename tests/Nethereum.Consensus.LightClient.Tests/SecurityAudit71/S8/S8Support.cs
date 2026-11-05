using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Beaconchain.LightClient.Responses;
using Nethereum.Consensus.Ssz;
using Nethereum.Signer.Bls;

namespace Nethereum.Consensus.LightClient.Tests.SecurityAudit71.S8
{
    internal static class S8Support
    {
        public const ulong PeriodStart = 11_649_024UL;

        public static SyncAggregate Aggregate(int participants)
        {
            var bits = new byte[SszBasicTypes.SyncCommitteeSize / 8];
            for (var i = 0; i < participants; i++)
            {
                bits[i / 8] |= (byte)(1 << (i % 8));
            }

            return new SyncAggregate
            {
                SyncCommitteeBits = bits,
                SyncCommitteeSignature = Enumerable.Repeat((byte)0x33, SszBasicTypes.SignatureLength).ToArray()
            };
        }

        public static SyncCommittee Committee(byte fill)
        {
            return new SyncCommittee
            {
                PubKeys = Enumerable.Range(0, SszBasicTypes.SyncCommitteeSize)
                    .Select(_ => Enumerable.Repeat(fill, SszBasicTypes.PubKeyLength).ToArray())
                    .ToList<byte[]>(),
                AggregatePubKey = Enumerable.Repeat((byte)(fill + 1), SszBasicTypes.PubKeyLength).ToArray()
            };
        }

        public static (byte[] StateRoot, List<byte[]> FinalityBranch, List<byte[]> NextCommitteeBranch) BuildState(
            byte[] finalizedRoot, byte[] nextCommitteeRoot)
        {
            var fork = ConsensusFork.Electra;
            var finalityGIndex = LightClientForkSpec.FinalizedRootGIndex(fork);
            var nextGIndex = LightClientForkSpec.NextSyncCommitteeGIndex(fork);
            var leaves = new Dictionary<int, byte[]>
            {
                [finalityGIndex] = finalizedRoot,
                [nextGIndex] = nextCommitteeRoot
            };

            byte[] Node(int g)
            {
                if (leaves.TryGetValue(g, out var leaf)) return leaf;
                var hasDescendant = leaves.Keys.Any(l =>
                {
                    var x = l;
                    while (x > g) x >>= 1;
                    return x == g;
                });
                if (!hasDescendant)
                {
                    using var sha = SHA256.Create();
                    return sha.ComputeHash(BitConverter.GetBytes(g));
                }

                return LightClientServiceTests.TestDataFactory.HashPair(Node(2 * g), Node(2 * g + 1));
            }

            List<byte[]> Branch(int leafGIndex)
            {
                var branch = new List<byte[]>();
                var g = leafGIndex;
                while (g > 1)
                {
                    branch.Add(Node(g ^ 1));
                    g >>= 1;
                }

                return branch;
            }

            return (Node(1), Branch(finalityGIndex), Branch(nextGIndex));
        }

        public static LightClientUpdate PlainUpdate(ulong attestedSlot, ulong blockNumber, int participants)
        {
            var stateRoot = Enumerable.Repeat((byte)0x02, 32).ToArray();
            var attested = LightClientServiceTests.TestDataFactory.CreateHeader(
                attestedSlot, blockNumber, stateRoot, ConsensusFork.Electra);
            var finalized = LightClientServiceTests.TestDataFactory.CreateHeader(
                attestedSlot, blockNumber, stateRoot, ConsensusFork.Electra);

            return new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                AttestedHeader = attested,
                FinalizedHeader = finalized,
                SyncAggregate = Aggregate(participants),
                NextSyncCommittee = new SyncCommittee(),
                FinalityBranch = LightClientServiceTests.TestDataFactory.CreateZeroBranch(
                    LightClientForkSpec.FinalityBranchLength(ConsensusFork.Electra)),
                NextSyncCommitteeBranch = LightClientServiceTests.TestDataFactory.CreateZeroBranch(
                    LightClientForkSpec.NextSyncCommitteeBranchLength(ConsensusFork.Electra)),
                SignatureSlot = attestedSlot + 1
            };
        }

        public static LightClientUpdate CommitteeUpdate(
            ulong attestedSlot, ulong attestedBlock, ulong finalizedSlot, ulong finalizedBlock,
            SyncCommittee next, int participants, bool withFinality)
        {
            var finalized = LightClientServiceTests.TestDataFactory.CreateHeader(
                finalizedSlot, finalizedBlock, Enumerable.Repeat((byte)0x02, 32).ToArray(), ConsensusFork.Electra);
            var state = BuildState(finalized.Beacon.HashTreeRoot(), next.HashTreeRoot());
            var attested = LightClientServiceTests.TestDataFactory.CreateHeader(
                attestedSlot, attestedBlock, state.StateRoot, ConsensusFork.Electra);

            return new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                AttestedHeader = attested,
                FinalizedHeader = finalized,
                SyncAggregate = Aggregate(participants),
                NextSyncCommittee = next,
                FinalityBranch = withFinality
                    ? state.FinalityBranch
                    : LightClientServiceTests.TestDataFactory.CreateZeroBranch(state.FinalityBranch.Count),
                NextSyncCommitteeBranch = state.NextCommitteeBranch,
                SignatureSlot = attestedSlot + 1
            };
        }

        public static async Task<(LightClientService Service, ScriptedApi Api)> StartAsync(
            LightClientBootstrap bootstrap, params LightClientUpdate[] updates)
        {
            var api = new ScriptedApi(bootstrap, updates);
            var service = new LightClientService(
                api, new AcceptingBls(), LightClientServiceTests.TestDataFactory.CreateConfig(bootstrap), new InMemoryLightClientStore());
            await service.InitializeAsync();
            return (service, api);
        }

        internal sealed class ScriptedApi : ILightClientApi
        {
            private readonly LightClientBootstrap _bootstrap;
            public List<LightClientUpdate> Updates { get; }

            public ScriptedApi(LightClientBootstrap bootstrap, IEnumerable<LightClientUpdate> updates)
            {
                _bootstrap = bootstrap;
                Updates = updates.ToList();
            }

            public Task<LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot) =>
                Task.FromResult(LightClientServiceTests.TestDataFactory.CreateBootstrapResponse(_bootstrap));

            public Task<IReadOnlyList<LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count) =>
                Task.FromResult<IReadOnlyList<LightClientUpdateResponse>>(
                    Updates.Select(LightClientServiceTests.TestDataFactory.CreateUpdateResponse).ToList());

            public Task<LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                Task.FromResult<LightClientFinalityUpdateResponse>(null);

            public Task<LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                Task.FromResult<LightClientOptimisticUpdateResponse>(null);
        }

        internal sealed class AcceptingBls : IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();
            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();
            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) => throw new NotSupportedException();
        }
    }
}
