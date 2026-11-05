using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    internal sealed class Discv4RawPeer : IDisposable
    {
        public EthECKey Key { get; }
        public UdpClient Udp { get; }
        public IPEndPoint Endpoint => (IPEndPoint)Udp.Client.LocalEndPoint;
        public byte[] NodeId => Key.GetPubKeyNoPrefix();

        public Discv4RawPeer(EthECKey key = null, int port = 0)
        {
            Key = key ?? EthECKey.GenerateKey();
            Udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        }

        public static long Future() => DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

        public static byte[] BuildPacket(EthECKey key, Discv4MessageType type, byte[] data)
        {
            var keccak = new Sha3Keccack();
            var typeAndData = new byte[1 + data.Length];
            typeAndData[0] = (byte)type;
            Buffer.BlockCopy(data, 0, typeAndData, 1, data.Length);
            var sig = key.SignAndCalculateV(keccak.CalculateHash(typeAndData));
            var sigBytes = new byte[65];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, sigBytes, 0, 32);
            Buffer.BlockCopy(sig.S.PadBytes(32), 0, sigBytes, 32, 32);
            sigBytes[64] = (byte)(sig.V[0] - 27);
            var hashInput = new byte[65 + typeAndData.Length];
            Buffer.BlockCopy(sigBytes, 0, hashInput, 0, 65);
            Buffer.BlockCopy(typeAndData, 0, hashInput, 65, typeAndData.Length);
            var packet = new byte[32 + hashInput.Length];
            Buffer.BlockCopy(keccak.CalculateHash(hashInput), 0, packet, 0, 32);
            Buffer.BlockCopy(hashInput, 0, packet, 32, hashInput.Length);
            return packet;
        }

        public Task SendAsync(byte[] packet, IPEndPoint to) => Udp.SendAsync(packet, packet.Length, to);

        public Discv4PingMessage NewPing(IPEndPoint to) => new Discv4PingMessage
        {
            Version = 4,
            From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)Endpoint.Port, TcpPort = (ushort)Endpoint.Port },
            To = new Discv4Endpoint { IP = to.Address, UdpPort = (ushort)to.Port, TcpPort = 0 },
            Expiration = Future()
        };

        public Task SendPingAsync(IPEndPoint to, EthECKey signWith = null)
        {
            var packet = BuildPacket(signWith ?? Key, Discv4MessageType.Ping, Discv4MessageEncoder.EncodePing(NewPing(to)));
            return SendAsync(packet, to);
        }

        public Task SendPongAsync(IPEndPoint to, byte[] pingHash)
        {
            var pong = new Discv4PongMessage
            {
                To = new Discv4Endpoint { IP = to.Address, UdpPort = (ushort)to.Port, TcpPort = 0 },
                PingHash = pingHash,
                Expiration = Future()
            };
            return SendAsync(BuildPacket(Key, Discv4MessageType.Pong, Discv4MessageEncoder.EncodePong(pong)), to);
        }

        public async Task<(Discv4Packet.DecodedPacket Packet, int Length)?> ReceiveAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                while (true)
                {
                    var result = await Udp.ReceiveAsync(cts.Token);
                    try { return (Discv4Packet.Decode(result.Buffer), result.Buffer.Length); }
                    catch (Exception) { }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<List<(Discv4Packet.DecodedPacket Packet, int Length)>> CollectAsync(TimeSpan window)
        {
            var list = new List<(Discv4Packet.DecodedPacket, int)>();
            var deadline = DateTime.UtcNow + window;
            while (true)
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) break;
                var got = await ReceiveAsync(left);
                if (got == null) break;
                list.Add(got.Value);
            }
            return list;
        }

        public async Task<bool> BondAsync(IPEndPoint target)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (await TryBondAsync(target)) return true;
            }
            return false;
        }

        private async Task<bool> TryBondAsync(IPEndPoint target)
        {
            await SendPingAsync(target);
            var gotBackPing = false;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (!gotBackPing && DateTime.UtcNow < deadline)
            {
                var got = await ReceiveAsync(deadline - DateTime.UtcNow);
                if (got == null) break;
                if (got.Value.Packet.Type == Discv4MessageType.Ping)
                {
                    await SendPongAsync(target, got.Value.Packet.Hash);
                    gotBackPing = true;
                }
            }
            await Task.Delay(150);
            return gotBackPing;
        }

        public void Dispose() => Udp.Dispose();
    }
}
