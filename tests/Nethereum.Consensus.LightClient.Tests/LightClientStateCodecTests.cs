using System;
using System.Linq;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class LightClientStateCodecTests
    {
        private static byte[] Fill(byte b, int n) { var a = new byte[n]; for (int i = 0; i < n; i++) a[i] = b; return a; }

        private static SyncCommittee FullCommittee(byte seed)
        {
            var c = new SyncCommittee { PubKeys = new System.Collections.Generic.List<byte[]>() };
            for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
                c.PubKeys.Add(Fill((byte)(seed + i), SszBasicTypes.PubKeyLength));
            c.AggregatePubKey = Fill((byte)(seed + 99), SszBasicTypes.PubKeyLength);
            return c;
        }

        private static BeaconBlockHeader Beacon(ulong slot) => new BeaconBlockHeader
        {
            Slot = slot,
            ProposerIndex = slot + 7,
            ParentRoot = Fill(0x11, 32),
            StateRoot = Fill(0x22, 32),
            BodyRoot = Fill(0x33, 32),
        };

        private static ExecutionPayloadHeader Execution(ConsensusFork fork, ulong block) => new ExecutionPayloadHeader
        {
            Fork = fork,
            ParentHash = Fill(0xA1, 32),
            FeeRecipient = Fill(0xA2, 20),
            StateRoot = Fill(0xA3, 32),
            ReceiptsRoot = Fill(0xA4, 32),
            LogsBloom = Fill(0xA5, SszBasicTypes.LogsBloomLength),
            PrevRandao = Fill(0xA6, 32),
            BlockNumber = block,
            GasLimit = 30_000_000,
            GasUsed = 15_000_000,
            Timestamp = 1_700_000_000,
            ExtraData = new byte[] { 0xDE, 0xAD },
            BaseFeePerGas = Fill(0xA7, 32),
            BlockHash = Fill(0xA8, 32),
            TransactionsRoot = Fill(0xA9, 32),
            WithdrawalsRoot = Fill(0xAA, 32),
            BlobGasUsed = 131072,
            ExcessBlobGas = 262144,
        };

        private static LightClientState PopulatedState()
        {
            var s = new LightClientState
            {
                FinalizedHeader = Beacon(14_751_136),
                FinalizedExecutionPayload = Execution(ConsensusFork.Deneb, 21_000_000),
                CurrentSyncCommittee = FullCommittee(1),
                NextSyncCommittee = FullCommittee(100),
                FinalizedSlot = 14_751_136,
                CurrentPeriod = 460_973,
                LastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_123_456),
                OptimisticHeader = Beacon(14_751_201),
                OptimisticExecutionPayload = Execution(ConsensusFork.Deneb, 21_000_064),
                OptimisticSlot = 14_751_201,
                OptimisticLastUpdated = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_222_222),
            };
            for (ulong b = 21_000_000; b < 21_000_050; b++)
                s.SetBlockHash(b, Fill((byte)(b & 0xFF), 32),
                    b % 2 == 0 ? BlockHashFinality.Finalized : BlockHashFinality.Optimistic);
            return s;
        }

        [Fact]
        public void RoundTrip_PreservesEveryField()
        {
            var s = PopulatedState();
            Assert.True(LightClientStateCodec.TryDecode(LightClientStateCodec.Encode(s), out var d));

            Assert.Equal(s.CurrentSyncCommittee.HashTreeRoot(), d.CurrentSyncCommittee.HashTreeRoot());
            Assert.Equal(s.NextSyncCommittee.HashTreeRoot(), d.NextSyncCommittee.HashTreeRoot());
            Assert.Equal(s.FinalizedHeader.HashTreeRoot(), d.FinalizedHeader.HashTreeRoot());
            Assert.Equal(s.OptimisticHeader.HashTreeRoot(), d.OptimisticHeader.HashTreeRoot());
            Assert.Equal(s.FinalizedExecutionPayload.HashTreeRoot(), d.FinalizedExecutionPayload.HashTreeRoot());
            Assert.Equal(ConsensusFork.Deneb, d.FinalizedExecutionPayload.Fork);
            Assert.Equal(s.OptimisticExecutionPayload.HashTreeRoot(), d.OptimisticExecutionPayload.HashTreeRoot());
            Assert.Equal(s.FinalizedSlot, d.FinalizedSlot);
            Assert.Equal(s.CurrentPeriod, d.CurrentPeriod);
            Assert.Equal(s.OptimisticSlot, d.OptimisticSlot);
            Assert.Equal(s.LastUpdated, d.LastUpdated);
            Assert.Equal(s.OptimisticLastUpdated, d.OptimisticLastUpdated);

            Assert.Equal(s.BlockHashHistory.Count, d.BlockHashHistory.Count);
            foreach (var kv in s.BlockHashHistory)
            {
                Assert.True(d.BlockHashHistory.TryGetValue(kv.Key, out var got));
                Assert.Equal(kv.Value.Finality, got.Finality);
                Assert.Equal(kv.Value.BlockHash, got.BlockHash);
            }
        }

        [Fact]
        public void RoundTrip_JustBootstrapShape_NextCommitteeEmpty()
        {
            var s = new LightClientState
            {
                FinalizedHeader = Beacon(14_751_136),
                CurrentSyncCommittee = FullCommittee(5),
                NextSyncCommittee = new SyncCommittee(),
                FinalizedSlot = 14_751_136,
                CurrentPeriod = 460_973,
            };
            Assert.True(LightClientStateCodec.TryDecode(LightClientStateCodec.Encode(s), out var d));
            Assert.Equal(s.CurrentSyncCommittee.HashTreeRoot(), d.CurrentSyncCommittee.HashTreeRoot());
            Assert.NotNull(d.NextSyncCommittee);
            Assert.Empty(d.NextSyncCommittee.PubKeys);
            Assert.Null(d.FinalizedExecutionPayload);
            Assert.Null(d.OptimisticHeader);
        }

        [Fact]
        public void Corruption_IsRejected_NotThrown()
        {
            var good = LightClientStateCodec.Encode(PopulatedState());

            Assert.False(LightClientStateCodec.TryDecode(null, out _));
            Assert.False(LightClientStateCodec.TryDecode(Array.Empty<byte>(), out _));
            Assert.False(LightClientStateCodec.TryDecode(good.Take(good.Length - 1).ToArray(), out _));

            var badMagic = (byte[])good.Clone(); badMagic[0] ^= 0xFF;
            Assert.False(LightClientStateCodec.TryDecode(badMagic, out _));

            var badVersion = (byte[])good.Clone(); badVersion[4] = 99;
            Assert.False(LightClientStateCodec.TryDecode(badVersion, out _));

            var flipped = (byte[])good.Clone(); flipped[good.Length / 2] ^= 0x01;
            Assert.False(LightClientStateCodec.TryDecode(flipped, out _));

            Assert.True(LightClientStateCodec.TryDecode(good, out _));
        }
    }
}
