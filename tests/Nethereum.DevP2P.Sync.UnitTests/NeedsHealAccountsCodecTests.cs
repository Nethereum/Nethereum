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
    public class NeedsHealAccountsCodecTests
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

            var blob = NeedsHealAccountsCodec.Encode(original);
            var decoded = NeedsHealAccountsCodec.Decode(blob);

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
            Assert.Empty(NeedsHealAccountsCodec.Decode(null));
            Assert.Empty(NeedsHealAccountsCodec.Decode(System.Array.Empty<byte>()));
        }

        [Fact]
        public void Given_TheShippedDeferredCodecNames_When_Used_Then_TheyForwardToTheNeedsHealCodecs()
        {
#pragma warning disable CS0618
            var account = new SnapSyncClient.AccountNeedingHeal(new byte[32], new byte[32]);
            var codeHash = new byte[32];
            codeHash[31] = 0x07;

            Assert.Equal(NeedsHealAccountsCodec.Encode(new[] { account }), DeferredHealAccountsCodec.Encode(new[] { account }));
            Assert.Single(DeferredHealAccountsCodec.Decode(NeedsHealAccountsCodec.Encode(new[] { account })));
            Assert.Equal(NeedsHealCodeCodec.Encode(new[] { codeHash }), DeferredHealCodeCodec.Encode(new[] { codeHash }));
            Assert.Single(DeferredHealCodeCodec.Decode(NeedsHealCodeCodec.Encode(new[] { codeHash })));
#pragma warning restore CS0618
        }
    }
}
