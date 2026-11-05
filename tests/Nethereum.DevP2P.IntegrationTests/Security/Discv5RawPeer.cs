using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    internal sealed class Discv5RawPeer : IDisposable
    {
        public sealed class Challenge
        {
            public byte[] Nonce;
            public byte[] ChallengeData;
        }

        public EthECKey Key { get; }
        public byte[] NodeId { get; }
        public UdpClient Udp { get; }
        public IPEndPoint Endpoint => (IPEndPoint)Udp.Client.LocalEndPoint;
        public byte[] SignedEnr { get; }

        public IPEndPoint Target { get; }
        public byte[] TargetNodeId { get; }
        public byte[] TargetStaticPubKey { get; }

        private byte[] _initiatorKey;
        private byte[] _recipientKey;

        public Discv5RawPeer(IPEndPoint target, EthECKey targetKey, EthECKey key = null)
        {
            Key = key ?? EthECKey.GenerateKey();
            NodeId = Discv5Crypto.ComputeNodeId(Key.GetPubKeyNoPrefix());
            Udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            Target = target;
            TargetNodeId = Discv5Crypto.ComputeNodeId(targetKey.GetPubKeyNoPrefix());
            TargetStaticPubKey = targetKey.GetPubKey(true);
            SignedEnr = SignEnr(Key, IPAddress.Loopback, Endpoint.Port, 1);
        }

        public static byte[] SignEnr(EthECKey key, IPAddress ip, int udpPort, ulong sequence, int? tcpPort = null)
        {
            var enr = new EnrRecord { Sequence = sequence };
            enr.Pairs["ip"] = ip.GetAddressBytes();
            enr.Pairs["udp"] = new[] { (byte)(udpPort >> 8), (byte)udpPort };
            if (tcpPort.HasValue) enr.Pairs["tcp"] = new[] { (byte)(tcpPort.Value >> 8), (byte)tcpPort.Value };
            EnrRecordSigner.Sign(enr, key);
            return EnrRecordEncoder.EncodeRecord(enr);
        }

        public static byte[] RawHeader(Discv5Packet.PacketFlag flag, byte[] nonce, byte[] authData)
        {
            var raw = new byte[Discv5Packet.HeaderStaticLength + authData.Length];
            Buffer.BlockCopy(Discv5Packet.ProtocolId, 0, raw, 0, 6);
            raw[6] = 0;
            raw[7] = 1;
            raw[8] = (byte)flag;
            Buffer.BlockCopy(nonce, 0, raw, 9, 12);
            raw[21] = (byte)(authData.Length >> 8);
            raw[22] = (byte)authData.Length;
            Buffer.BlockCopy(authData, 0, raw, 23, authData.Length);
            return raw;
        }

        private static byte[] Aad(byte[] iv, byte[] rawHeader)
        {
            var aad = new byte[iv.Length + rawHeader.Length];
            Buffer.BlockCopy(iv, 0, aad, 0, iv.Length);
            Buffer.BlockCopy(rawHeader, 0, aad, iv.Length, rawHeader.Length);
            return aad;
        }

        private static byte[] Random(int length)
        {
            var b = new byte[length];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        public byte[] BuildRandomOrdinaryPacket(byte[] nonce)
        {
            var header = new Discv5Packet.Header { Flag = Discv5Packet.PacketFlag.Ordinary, Nonce = nonce, AuthData = NodeId };
            return Discv5Packet.EncodePacket(Random(16), header, TargetNodeId, Random(44));
        }

        public async Task<Challenge> RequestChallengeAsync(IPEndPoint to = null)
        {
            var nonce = Random(12);
            var packet = BuildRandomOrdinaryPacket(nonce);
            await Udp.SendAsync(packet, packet.Length, to ?? Target);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                while (true)
                {
                    var result = await Udp.ReceiveAsync(cts.Token);
                    try
                    {
                        var (iv, header, _, rawHeader) = Discv5Packet.DecodePacket(result.Buffer, NodeId);
                        if (header.Flag == Discv5Packet.PacketFlag.WhoAreYou)
                            return new Challenge { Nonce = header.Nonce, ChallengeData = Aad(iv, rawHeader) };
                    }
                    catch (Exception) { }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public byte[] BuildHandshake(Challenge challenge, byte[] firstMessage, EthECKey idSigner = null, bool keepKeys = true, bool flipS = false)
        {
            var eph = EthECKey.GenerateKey();
            var ephPub = eph.GetPubKey(true);
            var sigInput = Discv5KeyDerivation.ComputeIdSignatureInput(challenge.ChallengeData, ephPub, TargetNodeId);
            var sig = Discv5Crypto.SignIdSignature(idSigner ?? Key, sigInput);
            if (flipS) sig = FlipS(sig);
            var secret = Discv5Crypto.EcdhCompressed(eph, TargetStaticPubKey);
            var (initKey, recpKey) = Discv5KeyDerivation.DeriveSessionKeys(secret, challenge.ChallengeData, NodeId, TargetNodeId);
            if (keepKeys)
            {
                _initiatorKey = initKey;
                _recipientKey = recpKey;
            }
            var auth = new Discv5HandshakePackets.HandshakeAuth
            {
                SrcId = NodeId,
                IdSignature = sig,
                EphemeralPubKey = ephPub,
                Record = SignedEnr
            }.Encode();
            return EncodeWith(Discv5Packet.PacketFlag.Handshake, Random(12), auth, initKey, firstMessage);
        }

        private static byte[] FlipS(byte[] sig)
        {
            var n = System.Numerics.BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", System.Globalization.NumberStyles.HexNumber);
            var s = new System.Numerics.BigInteger(sig.Skip(32).Reverse().Concat(new byte[] { 0 }).ToArray());
            var flipped = (n - s).ToByteArray().Reverse().SkipWhile(b => b == 0).ToArray();
            var result = (byte[])sig.Clone();
            Array.Clear(result, 32, 32);
            Buffer.BlockCopy(flipped, 0, result, 64 - flipped.Length, flipped.Length);
            return result;
        }

        private byte[] EncodeWith(Discv5Packet.PacketFlag flag, byte[] nonce, byte[] authData, byte[] key, byte[] plaintext)
        {
            var iv = Random(16);
            var header = new Discv5Packet.Header { Flag = flag, Nonce = nonce, AuthData = authData };
            var enc = Discv5Packet.EncryptMessage(key, nonce, Aad(iv, RawHeader(flag, nonce, authData)), plaintext);
            return Discv5Packet.EncodePacket(iv, header, TargetNodeId, enc);
        }

        public byte[] BuildOrdinary(byte[] plaintext)
            => EncodeWith(Discv5Packet.PacketFlag.Ordinary, Random(12), NodeId, _initiatorKey, plaintext);

        public Task SendRawAsync(byte[] packet, IPEndPoint to = null) => Udp.SendAsync(packet, packet.Length, to ?? Target);

        public async Task<bool> HandshakeAsync(byte[] firstMessage)
        {
            var challenge = await RequestChallengeAsync();
            if (challenge == null) return false;
            await SendRawAsync(BuildHandshake(challenge, firstMessage));
            return true;
        }

        public async Task<byte[]> EstablishSessionAsync(byte[] firstMessage, int attempts = 3)
        {
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                if (!await HandshakeAsync(firstMessage)) continue;
                var reply = await ReceiveMessageAsync(TimeSpan.FromSeconds(5));
                if (reply != null) return reply;
            }
            return null;
        }

        public Task SendMessageAsync(byte[] plaintext) => SendRawAsync(BuildOrdinary(plaintext));

        public async Task<byte[]> ReceiveMessageAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                while (true)
                {
                    var result = await Udp.ReceiveAsync(cts.Token);
                    try
                    {
                        var (iv, header, enc, rawHeader) = Discv5Packet.DecodePacket(result.Buffer, NodeId);
                        if (header.Flag != Discv5Packet.PacketFlag.Ordinary) continue;
                        return Discv5Packet.DecryptMessage(_recipientKey, header.Nonce, Aad(iv, rawHeader), enc);
                    }
                    catch (Exception) { }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<System.Collections.Generic.List<byte[]>> CollectMessagesAsync(TimeSpan window)
        {
            var list = new System.Collections.Generic.List<byte[]>();
            var deadline = DateTime.UtcNow + window;
            while (true)
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) break;
                var msg = await ReceiveMessageAsync(left);
                if (msg == null) break;
                list.Add(msg);
            }
            return list;
        }

        public void Dispose() => Udp.Dispose();
    }
}
