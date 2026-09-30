using System.Net;
using Nethereum.DevP2P.Rlpx;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxInboundAdmissionTests
    {
        private static RlpxInboundAdmission Admission(int perIp, int perSubnet)
            => new RlpxInboundAdmission(new DevP2PConfig
            {
                MaxInboundPerIP = perIp,
                MaxInboundPerSubnet = perSubnet
            });

        [Fact]
        public void Given_PerIpCapReached_When_TryReserve_Then_RefusesWithPerIpCap()
        {
            var admission = Admission(perIp: 2, perSubnet: 0);
            var ip = IPAddress.Parse("203.0.113.5");

            Assert.True(admission.TryReserve(ip).Admitted);
            Assert.True(admission.TryReserve(ip).Admitted);

            var rejected = admission.TryReserve(ip);
            Assert.False(rejected.Admitted);
            Assert.Equal(InboundRejectReason.PerIpCap, rejected.Reason);
            Assert.Equal(2, admission.CountInboundForIp(ip));
        }

        [Fact]
        public void Given_PerSubnetCapReached_When_TryReserve_Then_RefusesAndRollsBackPerIp()
        {
            var admission = Admission(perIp: 10, perSubnet: 1);
            var first = IPAddress.Parse("203.0.113.1");
            var second = IPAddress.Parse("203.0.113.2");

            Assert.True(admission.TryReserve(first).Admitted);

            var rejected = admission.TryReserve(second);
            Assert.False(rejected.Admitted);
            Assert.Equal(InboundRejectReason.PerSubnetCap, rejected.Reason);
            Assert.Equal(0, admission.CountInboundForIp(second));
            Assert.Equal(1, admission.CountInboundForIp(first));
        }

        [Fact]
        public void Given_SubnetSlotReleased_When_TryReserve_Then_ReAdmits()
        {
            var admission = Admission(perIp: 10, perSubnet: 1);
            var first = IPAddress.Parse("203.0.113.1");
            var second = IPAddress.Parse("203.0.113.2");

            var reservation = admission.TryReserve(first);
            Assert.True(reservation.Admitted);
            Assert.False(admission.TryReserve(second).Admitted);

            admission.Release(first, reservation.SubnetKey);

            Assert.True(admission.TryReserve(second).Admitted);
        }

        [Fact]
        public void Given_MappedIPv4PeersInDifferentSubnets_When_TryReserve_Then_DoNotShareSubnetSlot()
        {
            var admission = Admission(perIp: 10, perSubnet: 1);
            var mappedA = IPAddress.Parse("::ffff:203.0.113.7");
            var mappedB = IPAddress.Parse("::ffff:198.51.100.7");

            Assert.True(admission.TryReserve(mappedA).Admitted);
            Assert.True(admission.TryReserve(mappedB).Admitted);
        }

        [Fact]
        public void Given_MappedIPv4PeersInSameSubnet_When_TryReserve_Then_ShareSubnetSlot()
        {
            var admission = Admission(perIp: 10, perSubnet: 1);
            var mappedA = IPAddress.Parse("::ffff:203.0.113.7");
            var mappedB = IPAddress.Parse("::ffff:203.0.113.8");

            Assert.True(admission.TryReserve(mappedA).Admitted);

            var rejected = admission.TryReserve(mappedB);
            Assert.False(rejected.Admitted);
            Assert.Equal(InboundRejectReason.PerSubnetCap, rejected.Reason);
        }

        [Fact]
        public void Given_NativeLoopbackV4_When_TryReserve_Then_StillCountedBySubnet()
        {
            var admission = Admission(perIp: 10, perSubnet: 1);

            Assert.True(admission.TryReserve(IPAddress.Parse("127.0.0.1")).Admitted);

            var rejected = admission.TryReserve(IPAddress.Parse("127.0.0.2"));
            Assert.False(rejected.Admitted);
            Assert.Equal(InboundRejectReason.PerSubnetCap, rejected.Reason);
        }

        [Fact]
        public void Given_PerIpCapZero_When_TryReserve_Then_RefusesEvenFirst()
        {
            var admission = Admission(perIp: 0, perSubnet: 0);

            var rejected = admission.TryReserve(IPAddress.Parse("203.0.113.5"));
            Assert.False(rejected.Admitted);
            Assert.Equal(InboundRejectReason.PerIpCap, rejected.Reason);
        }

        [Fact]
        public void Given_PerSubnetDisabled_When_TryReserve_Then_OnlyPerIpApplies()
        {
            var admission = Admission(perIp: 1, perSubnet: 0);

            for (int host = 1; host <= 20; host++)
                Assert.True(admission.TryReserve(IPAddress.Parse($"203.0.113.{host}")).Admitted);
        }
    }
}
