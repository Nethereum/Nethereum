using System.Collections.Generic;
using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class DeferredHealAccountsCodecTests
    {
        [Fact]
        public void Given_ANonEmptyDeferredAccountsList_When_EncodedThenDecoded_Then_TheListRoundTripsExactly()
        {
            var accountHash1 = new byte[32];
            var storageRoot1 = new byte[32];
            for (int i = 0; i < 32; i++) { accountHash1[i] = (byte)i; storageRoot1[i] = (byte)(255 - i); }
            var accountHash2 = new byte[32];
            var storageRoot2 = new byte[32];
            for (int i = 0; i < 32; i++) { accountHash2[i] = (byte)(i * 2); storageRoot2[i] = (byte)(i + 1); }

            var original = new List<SnapSyncClient.AccountNeedingHeal>
            {
                new(accountHash1, storageRoot1),
                new(accountHash2, storageRoot2),
            };

            var blob = DeferredHealAccountsCodec.Encode(original);
            var decoded = DeferredHealAccountsCodec.Decode(blob);

            Assert.Equal(original.Count, decoded.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].AccountHash, decoded[i].AccountHash);
                Assert.Equal(original[i].ExpectedStorageRoot, decoded[i].ExpectedStorageRoot);
            }
        }

        [Fact]
        public void Given_ANullOrEmptyBlob_When_Decoded_Then_ReturnsAnEmptyList()
        {
            Assert.Empty(DeferredHealAccountsCodec.Decode(null));
            Assert.Empty(DeferredHealAccountsCodec.Decode(System.Array.Empty<byte>()));
        }
    }
}
