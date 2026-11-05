using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    internal sealed class RawRlpxPeer : IDisposable
    {
        public TcpClient Tcp { get; }
        public NetworkStream Stream { get; }
        public RlpxFrameWriter Writer { get; }
        public RlpxFrameReader Reader { get; }
        public EthECKey Key { get; }

        private RawRlpxPeer(TcpClient tcp, EthECKey key, RlpxFrameWriter writer, RlpxFrameReader reader)
        {
            Tcp = tcp;
            Stream = tcp.GetStream();
            Key = key;
            Writer = writer;
            Reader = reader;
        }

        public static async Task<RawRlpxPeer> ConnectAsync(
            IPAddress source, int port, byte[] serverPub, EthECKey key = null, int receiveBufferSize = 0)
        {
            key ??= EthECKey.GenerateKey();
            var tcp = source == null
                ? new TcpClient()
                : new TcpClient(new IPEndPoint(source, 0));
            if (receiveBufferSize > 0) tcp.ReceiveBufferSize = receiveBufferSize;
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            var stream = tcp.GetStream();

            var (auth, state) = RlpxHandshake.CreateAuth(key, serverPub);
            await stream.WriteAsync(auth);

            var size = new byte[2];
            await ReadExactAsync(stream, size);
            var body = new byte[(size[0] << 8) | size[1]];
            await ReadExactAsync(stream, body);
            var ack = new byte[2 + body.Length];
            Buffer.BlockCopy(size, 0, ack, 0, 2);
            Buffer.BlockCopy(body, 0, ack, 2, body.Length);

            var secrets = RlpxHandshake.HandleAck(state, ack);
            return new RawRlpxPeer(
                tcp, key,
                new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac),
                new RlpxFrameReader(secrets.AesSecret, secrets.MacSecret, secrets.IngressMac));
        }

        public static HelloMessage HelloFor(EthECKey key, string clientId = "attacker", byte[] nodeId = null) =>
            new HelloMessage
            {
                ProtocolVersion = 5,
                ClientId = clientId,
                Capabilities = { new P2PCapability { Name = "eth", Version = 68 } },
                ListenPort = 0,
                NodeId = nodeId ?? key.GetPubKeyNoPrefix()
            };

        public Task SendFrameAsync(int msgId, byte[] payload, CancellationToken ct = default)
            => Stream.WriteAsync(Writer.WriteFrame(msgId, payload), ct).AsTask();

        public async Task<(int msgId, byte[] payload)> ReadFrameAsync(CancellationToken ct = default)
        {
            var header = new byte[32];
            await ReadExactAsync(Stream, header, ct);
            var frameSize = Reader.ReadHeader(header);
            var padded = ((frameSize + 15) / 16) * 16;
            var body = new byte[padded + 16];
            await ReadExactAsync(Stream, body, ct);
            return Reader.ReadBody(frameSize, body);
        }

        public static async Task ReadExactAsync(System.IO.Stream stream, byte[] buffer, CancellationToken ct = default)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
                if (read == 0) throw new System.IO.IOException("closed");
                offset += read;
            }
        }

        public void Dispose()
        {
            try { Tcp.Dispose(); } catch { }
        }
    }
}
