using System.Collections.Generic;
using System.Linq;
using Nethereum.DevP2P;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.UnitTests
{
    public class CapabilityNegotiatorTests
    {

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

        [Fact]
        public void Negotiate_SharedUnknownCapability_Throws()
        {
            var local = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 68 },
                new P2PCapability { Name = "mystery", Version = 1 },
            };
            var remote = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 68 },
                new P2PCapability { Name = "mystery", Version = 1 },
            };

            Assert.Throws<System.NotSupportedException>(
                () => CapabilityNegotiator.Negotiate(local, remote));
        }

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
        public void Negotiate_Eth71_Grants20Slots()
        {
            var local = EthAndSnap(68, 69, 71);
            var remote = EthAndSnap(68, 69, 71);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var eth = shared.Single(c => c.Name == "eth");
            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(71, eth.Version);
            Assert.Equal(20, eth.Length);
            Assert.Equal(0x10, eth.Offset);
            Assert.Equal(0x24, snap.Offset);
        }

        [Fact]
        public void Negotiate_Eth71_Twin_Eth69StaysAt18AndEth68StaysAt17()
        {
            var eth69Shared = CapabilityNegotiator.Negotiate(EthAndSnap(68, 69), EthAndSnap(68, 69));
            Assert.Equal(18, eth69Shared.Single(c => c.Name == "eth").Length);

            var eth68Shared = CapabilityNegotiator.Negotiate(EthAndSnap(68), EthAndSnap(68));
            Assert.Equal(17, eth68Shared.Single(c => c.Name == "eth").Length);
        }

        [Fact]
        public void CapabilityNegotiator_Eth70Grants18()
        {
            var local = EthAndSnap(68, 69, 70);
            var remote = EthAndSnap(68, 69, 70);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var eth = shared.Single(c => c.Name == "eth");
            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(70, eth.Version);
            Assert.Equal(18, eth.Length);
            Assert.Equal(0x10, eth.Offset);
            Assert.Equal(0x22, snap.Offset);
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

        [Fact]
        public void CapabilityNegotiator_Snap2Grants10()
        {
            var local = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 69 },
                new P2PCapability { Name = "snap", Version = 1 },
                new P2PCapability { Name = "snap", Version = 2 },
            };
            var remote = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 69 },
                new P2PCapability { Name = "snap", Version = 1 },
                new P2PCapability { Name = "snap", Version = 2 },
            };

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(2, snap.Version);
            Assert.Equal(10, snap.Length);
        }

        [Fact]
        public void CapabilityNegotiator_Snap2Grants10_Twin_Snap1StaysAt8()
        {
            var local = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 69 },
                new P2PCapability { Name = "snap", Version = 1 },
                new P2PCapability { Name = "snap", Version = 2 },
            };
            var remote = EthAndSnap(69);

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            var snap = shared.Single(c => c.Name == "snap");
            Assert.Equal(1, snap.Version);
            Assert.Equal(8, snap.Length);
        }
    }
}
