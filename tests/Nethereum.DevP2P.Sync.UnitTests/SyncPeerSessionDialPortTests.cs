using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SyncPeerSessionDialPortTests
    {
        [Fact]
        public async Task Given_AnEnodeWithADiscportSuffix_When_Dialled_Then_ThePortUsedIsTheTcpPort()
        {
            var (accepted, failure) = await DialLoopbackAsync(tcpPort => $"127.0.0.1:{tcpPort}?discport=30301");

            Assert.True(accepted,
                $"no TCP connection reached the listener at the enode TCP port; the dial raised {failure?.GetType().Name}: {failure?.Message}");
        }

        [Fact]
        public async Task Given_AnEnodeWithoutADiscportSuffix_When_Dialled_Then_ThePortUsedIsTheTcpPort()
        {
            var (accepted, failure) = await DialLoopbackAsync(tcpPort => $"127.0.0.1:{tcpPort}");

            Assert.True(accepted,
                $"no TCP connection reached the listener at the enode TCP port; the dial raised {failure?.GetType().Name}: {failure?.Message}");
        }

        private static async Task<(bool Accepted, Exception Failure)> DialLoopbackAsync(Func<int, string> hostAndPort)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var tcpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                var nodeId = EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex();
                var enode = $"enode://{nodeId}@{hostAndPort(tcpPort)}";
                var accept = listener.AcceptTcpClientAsync();
                var genesis = new byte[32];
                genesis[0] = 0x01;

                Exception failure = null;
                try
                {
                    using var session = await SyncPeerSession.ConnectAsync(
                        enode, TimeSpan.FromSeconds(3), CancellationToken.None, genesis, expectedNetworkId: 1);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                var winner = await Task.WhenAny(accept, Task.Delay(TimeSpan.FromSeconds(5)));
                if (winner != accept) return (false, failure);
                accept.Result.Dispose();
                return (true, failure);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
