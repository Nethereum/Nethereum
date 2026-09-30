using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.MainnetChain.Bootstrap;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class DurableLightClientStoreTests
    {
        private static byte[] Fill(byte b, int n) { var a = new byte[n]; for (int i = 0; i < n; i++) a[i] = b; return a; }

        private static SyncCommittee FullCommittee(byte seed)
        {
            var c = new SyncCommittee { PubKeys = new List<byte[]>() };
            for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
                c.PubKeys.Add(Fill((byte)(seed + i), SszBasicTypes.PubKeyLength));
            c.AggregatePubKey = Fill((byte)(seed + 71), SszBasicTypes.PubKeyLength);
            return c;
        }

        private static LightClientState State(DateTimeOffset updated) => new LightClientState
        {
            FinalizedHeader = new BeaconBlockHeader
            {
                Slot = 14_751_136,
                ProposerIndex = 5,
                ParentRoot = Fill(0x11, 32),
                StateRoot = Fill(0x22, 32),
                BodyRoot = Fill(0x33, 32),
            },
            CurrentSyncCommittee = FullCommittee(1),
            NextSyncCommittee = FullCommittee(100),
            FinalizedSlot = 14_751_136,
            CurrentPeriod = 460_973,
            OptimisticSlot = 14_751_201,
            OptimisticLastUpdated = updated,
            LastUpdated = updated,
        };

        private static DurableLightClientStore NewStore(InMemoryChainMetadataStore meta, DateTimeOffset now, TimeSpan? window = null)
            => new DurableLightClientStore(meta, window ?? TimeSpan.FromDays(14), now: () => now);

        [Fact]
        public async Task SaveThenLoad_RoundTripsVerifiedState_WithinWindow()
        {
            var meta = new InMemoryChainMetadataStore();
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var saved = State(now);

            await NewStore(meta, now).SaveAsync(saved);
            var loaded = await NewStore(meta, now.AddMinutes(5)).LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(saved.CurrentSyncCommittee.HashTreeRoot(), loaded.CurrentSyncCommittee.HashTreeRoot());
            Assert.Equal(saved.NextSyncCommittee.HashTreeRoot(), loaded.NextSyncCommittee.HashTreeRoot());
            Assert.Equal(saved.FinalizedHeader.HashTreeRoot(), loaded.FinalizedHeader.HashTreeRoot());
            Assert.Equal(saved.CurrentPeriod, loaded.CurrentPeriod);
            Assert.Equal(saved.OptimisticSlot, loaded.OptimisticSlot);
        }

        [Fact]
        public async Task Load_NoPersistedState_ReturnsNull()
        {
            var loaded = await NewStore(new InMemoryChainMetadataStore(), DateTimeOffset.UtcNow).LoadAsync();
            Assert.Null(loaded);
        }

        [Fact]
        public async Task Load_StaleBeyondTrustWindow_ReturnsNull_ForcingReBootstrap()
        {
            var meta = new InMemoryChainMetadataStore();
            var saved = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            await NewStore(meta, saved).SaveAsync(State(saved));

            var loaded = await NewStore(meta, saved.AddDays(15), TimeSpan.FromDays(14)).LoadAsync();
            Assert.Null(loaded);
        }

        [Fact]
        public async Task Load_JustInsideWindow_Resumes()
        {
            var meta = new InMemoryChainMetadataStore();
            var saved = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            await NewStore(meta, saved).SaveAsync(State(saved));

            var loaded = await NewStore(meta, saved.AddDays(13), TimeSpan.FromDays(14)).LoadAsync();
            Assert.NotNull(loaded);
        }

        [Fact]
        public async Task Load_CorruptBlob_ReturnsNull_NoThrow()
        {
            var meta = new InMemoryChainMetadataStore();
            meta.SaveLightClientStateBlob(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });
            var loaded = await NewStore(meta, DateTimeOffset.UtcNow).LoadAsync();
            Assert.Null(loaded);
        }
    }
}
