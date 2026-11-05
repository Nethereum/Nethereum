using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5InboundBanExpirySecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_BannedSourceIp_When_TheBanDurationHasElapsed_Then_TheIpIsNoLongerBanned()
        {
            await using var listener = new Discv5Listener(EthECKey.GenerateKey())
            {
                EnableInboundPacketFilter = true,
                BanDuration = TimeSpan.FromSeconds(5)
            };
            listener.Start(IPAddress.Loopback, 0);
            using var attacker = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var garbage = new byte[Discv5Packet.MinPacketSize];
            new Random(5).NextBytes(garbage);
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            for (var i = 0; i < 50; i++)
                await attacker.SendAsync(garbage, garbage.Length, target);
            Assert.True(await WaitUntilAsync(() => listener.IsBanned(IPAddress.Loopback), TimeSpan.FromSeconds(10)),
                "the flood never triggered a ban");

            Assert.True(await WaitUntilAsync(() => !listener.IsBanned(IPAddress.Loopback), TimeSpan.FromSeconds(20)),
                "a spoofed-source flood bans an address for the process lifetime; bans must expire");
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(20);
            }
            return condition();
        }
    }
}
