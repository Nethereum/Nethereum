using System;
using Nethereum.DevP2P.Sync.Peering;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SyncPeerSessionDialConfigTests
    {
        [Fact]
        public void Given_ADialTimeout_When_BuildingDialConfig_Then_ReadTimeoutExceedsPingIntervalAtGethParityValues()
        {
            var timeout = TimeSpan.FromSeconds(15);

            var cfg = SyncPeerSession.BuildDialConfig(timeout);

            Assert.Equal(30_000, cfg.ReadTimeoutMs);
            Assert.Equal(15_000, cfg.PingIntervalMs);
            Assert.True(cfg.ReadTimeoutMs > cfg.PingIntervalMs);
            Assert.Equal((int)timeout.TotalMilliseconds, cfg.RequestTimeoutMs);
            Assert.Equal((int)timeout.TotalMilliseconds, cfg.ConnectTimeoutMs);
            Assert.Equal((int)timeout.TotalMilliseconds, cfg.HandshakeTimeoutMs);
        }
    }
}
