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
    /// <summary>
    /// Adversarial datagram-flood tests for the discv5 inbound filter wired in
    /// <see cref="Discv5Listener"/>. Per-source-IP rate-limiting and the
    /// banned-IP LRU run BEFORE any crypto work so a single source cannot
    /// amplify cost beyond the cited bucket cap.
    /// </summary>
    public class Discv5FloodTests
    {
        private readonly ITestOutputHelper _output;
        public Discv5FloodTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task Given_OneSourceIp_When_FloodedWithGarbagePackets_Then_BucketCapDropsExcess()
        {
            // Real loopback UdpClient — no mocks of the socket layer.
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

            // Allow the listener's read loop to drain its receive queue.
            await Task.Delay(500).ConfigureAwait(false);

            var dropped = listener.DroppedInboundCount;
            _output.WriteLine($"dropped={dropped} burst={DevP2PRateLimitConstants.InboundBurstCapacity}");

            // Burst capacity is admitted; the rest are dropped before crypto.
            // Slack: the rate limiter refills continuously, so a ~500 ms drain
            // can admit a handful of refilled tokens.
            Assert.True(dropped >= flood - DevP2PRateLimitConstants.InboundBurstCapacity - 5,
                $"expected at least {flood - DevP2PRateLimitConstants.InboundBurstCapacity - 5} drops, observed {dropped}");
            // Banned-IP LRU records the offender after the first deny.
            Assert.True(listener.IsBanned(IPAddress.Loopback));
        }

        [Fact]
        public async Task Given_DistinctSourceIps_When_BurstFromEach_Then_AllAcceptedPerIp()
        {
            // Use 127.0.0.x — every byte in the last octet is a distinct
            // loopback IP from the OS's perspective.
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

            // Each IP stays inside its own burst — no banned IPs across the run.
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

            // Phase 1: induce a ban (burst+1 packets).
            for (int i = 0; i < DevP2PRateLimitConstants.InboundBurstCapacity + 2; i++)
            {
                await attacker.SendAsync(garbage, garbage.Length, listenerEndpoint).ConfigureAwait(false);
            }
            await Task.Delay(200).ConfigureAwait(false);
            Assert.True(listener.IsBanned(IPAddress.Loopback));
            var droppedAfterBan = listener.DroppedInboundCount;

            // Phase 2: more traffic from the same IP. The banned-IP fast path
            // increments the dropped counter without ever consulting the bucket.
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
            // Minimum valid discv5 packet size — anything smaller is rejected
            // by the size check upstream of the rate limiter and would not
            // exercise the bucket path. Per discv5-wire.md §"Packet Encoding".
            var buf = new byte[Discv5Packet.MinPacketSize];
            new Random(1234).NextBytes(buf);
            return buf;
        }
    }
}
