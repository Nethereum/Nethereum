using System.Collections.Generic;
using System.Linq;
using Nethereum.DevP2P;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.UnitTests
{
    public class CapabilityNegotiatorTests
    {
        // The slot count for the eth sub-protocol changes with the version:
        // geth eth/protocols/eth/protocol.go declares
        //     protocolLengths = { ETH68: 17, ETH69: 18 }
        // Any later sub-protocol (snap, les, ...) starts at 0x10 + length(eth).
        // If we negotiate a different eth version than the peer expected and
        // hand a fixed slot count, the snap base offset drifts one slot and
        // our GetAccountRange lands on the peer's AccountRange response slot
        // — which is the exact symptom of the 5/5 peer-disconnect snap bug.

        private static List<P2PCapability> EthAndSnap(params int[] ethVersions)
        {
            var caps = ethVersions
                .Select(v => new P2PCapability { Name = "eth", Version = v })
                .ToList();
            caps.Add(new P2PCapability { Name = "snap", Version = 1 });
            return caps;
        }

        [Fact]
        public void Negotiate_Eth69_PutsSnapAt0x22()
        {
            var local = EthAndSnap(68, 69);
            var remote = EthAndSnap(68, 69);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var eth = shared.Single(c => c.Name == "eth");
            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(69, eth.Version);
            Assert.Equal(18, eth.Length);
            Assert.Equal(0x10, eth.Offset);
            Assert.Equal(0x22, snap.Offset);
        }

        // Regression: this was the production snap-sync blocker.
        // When the peer only advertised eth/68 we used to allocate 18 slots
        // (the eth/69 length), putting snap at 0x22; the peer expected 0x21,
        // so our GetAccountRange wire ID landed on its AccountRange response
        // slot, peer's RLP decoder failed, peer disconnected. All 5/5 snap
        // peers dropped us in production until this was fixed.
        [Fact]
        public void Negotiate_Eth68Only_PutsSnapAt0x21()
        {
            var local = EthAndSnap(68, 69);
            var remote = EthAndSnap(68);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var eth = shared.Single(c => c.Name == "eth");
            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(68, eth.Version);
            Assert.Equal(17, eth.Length);
            Assert.Equal(0x10, eth.Offset);
            Assert.Equal(0x21, snap.Offset);
        }

        [Fact]
        public void Negotiate_Snap_OnlyEightSlots()
        {
            var local = EthAndSnap(69);
            var remote = EthAndSnap(69);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(8, snap.Length);
        }
    }
}
