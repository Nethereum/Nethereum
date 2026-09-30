using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Discv5
{
    public class Discv5FloodTests
    {
        private readonly ITestOutputHelper _output;
        public Discv5FloodTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task Given_OneSourceIp_When_FloodedWithGarbagePackets_Then_BucketCapDropsExcess()
        {
            var listenerKey = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(listenerKey);
            listener.Start(IPAddress.Loopback, port: 0);
            var listenerEndpoint = new IPEndPoint(IPAddress.Loopback, listener.Port);

            using var attacker = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            const int flood = 100;
            var garbage = MakeMinimumSizedGarbagePacket();
            for (int i = 0; i < flood; i++)
            {
                await attacker.SendAsync(garbage, garbage.Length, listenerEndpoint).ConfigureAwait(false);
            }

            await Task.Delay(500).ConfigureAwait(false);

            var dropped = listener.DroppedInboundCount;
            _output.WriteLine($"dropped={dropped} burst={DevP2PRateLimitConstants.InboundBurstCapacity}");

            Assert.True(dropped >= flood - DevP2PRateLimitConstants.InboundBurstCapacity - 5,
                $"expected at least {flood - DevP2PRateLimitConstants.InboundBurstCapacity - 5} drops, observed {dropped}");
            Assert.True(listener.IsBanned(IPAddress.Loopback));
        }

        [Fact]
        public async Task Given_DistinctSourceIps_When_BurstFromEach_Then_AllAcceptedPerIp()
        {
            var listenerKey = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(listenerKey);
            listener.Start(IPAddress.Parse("127.0.0.1"), port: 0);
            var listenerEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), listener.Port);

            const int distinctIps = 20;
            const int packetsPerIp = DevP2PRateLimitConstants.InboundBurstCapacity;

            var garbage = MakeMinimumSizedGarbagePacket();
            for (int ipIdx = 1; ipIdx <= distinctIps; ipIdx++)
            {
                var srcIp = IPAddress.Parse($"127.0.0.{ipIdx}");
                using var sender = new UdpClient(new IPEndPoint(srcIp, 0));
                for (int p = 0; p < packetsPerIp; p++)
                {
                    await sender.SendAsync(garbage, garbage.Length, listenerEndpoint).ConfigureAwait(false);
                }
            }

            await Task.Delay(500).ConfigureAwait(false);

            for (int ipIdx = 1; ipIdx <= distinctIps; ipIdx++)
            {
                var srcIp = IPAddress.Parse($"127.0.0.{ipIdx}");
                Assert.False(listener.IsBanned(srcIp),
                    $"{srcIp} should not be banned within its burst");
            }
        }

        [Fact]
        public async Task Given_AlreadyBannedIp_When_AdditionalPacketsArrive_Then_DroppedWithoutBucketRefresh()
        {
            var listenerKey = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(listenerKey);
            listener.Start(IPAddress.Loopback, port: 0);
            var listenerEndpoint = new IPEndPoint(IPAddress.Loopback, listener.Port);

            using var attacker = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var garbage = MakeMinimumSizedGarbagePacket();

            for (int i = 0; i < DevP2PRateLimitConstants.InboundBurstCapacity + 2; i++)
            {
                await attacker.SendAsync(garbage, garbage.Length, listenerEndpoint).ConfigureAwait(false);
            }
            await Task.Delay(200).ConfigureAwait(false);
            Assert.True(listener.IsBanned(IPAddress.Loopback));
            var droppedAfterBan = listener.DroppedInboundCount;

            for (int i = 0; i < 20; i++)
            {
                await attacker.SendAsync(garbage, garbage.Length, listenerEndpoint).ConfigureAwait(false);
            }
            await Task.Delay(200).ConfigureAwait(false);

            Assert.True(listener.DroppedInboundCount > droppedAfterBan,
                "post-ban packets must be counted as drops");
        }

        private static byte[] MakeMinimumSizedGarbagePacket()
        {
            var buf = new byte[Discv5Packet.MinPacketSize];
            new Random(1234).NextBytes(buf);
            return buf;
        }
    }
}
