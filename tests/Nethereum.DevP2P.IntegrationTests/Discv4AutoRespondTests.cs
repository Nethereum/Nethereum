using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Direct loopback test of Discv4Listener auto-respond: send PING, expect
    /// PONG. Isolates the auto-PONG path from full conformance harness so we
    /// can see why the devp2p tool sees i/o timeouts.
    /// </summary>
    public class Discv4AutoRespondTests
    {
        private readonly ITestOutputHelper _output;
        public Discv4AutoRespondTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task SendPing_ListenerAutoResponds_WithPong()
        {
            var serverKey = EthECKey.GenerateKey();
            var serverRouting = new Discv4RoutingTable(serverKey.GetPubKeyNoPrefix());
            using var server = new Discv4Listener(serverKey, serverRouting);
            server.ErrorOccurred += (_, e) => _output.WriteLine($"server error [{e.Phase}]: {e.Exception.GetType().Name} {e.Exception.Message}");
            server.Start(udpPort: 0, bindAddress: IPAddress.Loopback);
            _output.WriteLine($"Listener bound to 127.0.0.1:{server.Port}");

            using var clientUdp = new UdpClient(0, AddressFamily.InterNetwork);
            var clientPort = ((IPEndPoint)clientUdp.Client.LocalEndPoint).Port;
            var clientKey = EthECKey.GenerateKey();

            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)clientPort, TcpPort = (ushort)clientPort },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)server.Port, TcpPort = 0 },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var pingData = Discv4MessageEncoder.EncodePing(ping);
            var pingPacket = Discv4Packet.Encode(clientKey, Discv4MessageType.Ping, pingData);

            var serverEp = new IPEndPoint(IPAddress.Loopback, server.Port);
            await clientUdp.SendAsync(pingPacket, pingPacket.Length, serverEp);
            _output.WriteLine($"Sent PING from 127.0.0.1:{clientPort} to 127.0.0.1:{server.Port}");

            var receiveTask = clientUdp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(receiveTask, completed);

            var result = receiveTask.Result;
            _output.WriteLine($"Got {result.Buffer.Length} bytes from {result.RemoteEndPoint}");

            var decoded = Discv4Packet.Decode(result.Buffer);
            Assert.Equal(Discv4MessageType.Pong, decoded.Type);
            Assert.True(ByteUtil.AreEqual(decoded.SenderPubKey, serverKey.GetPubKeyNoPrefix()),
                "PONG must be signed by our listener's key");

            var pong = Discv4MessageEncoder.DecodePong(decoded.Data);
            var expectedPingHash = new byte[32];
            Buffer.BlockCopy(pingPacket, 0, expectedPingHash, 0, 32);
            Assert.Equal(expectedPingHash.ToHex(), pong.PingHash.ToHex());
            _output.WriteLine($"PONG ok. ping-hash matches.");

            await server.StopAsync();
        }
    }
}
