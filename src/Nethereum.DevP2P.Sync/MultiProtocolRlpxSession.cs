using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Owns the receive loop on an RlpxConnection negotiated with both eth and
    /// snap capabilities, identifies each frame's capability by its message-id
    /// range (per the devp2p RLPx spec: each shared capability gets a
    /// contiguous offset window starting at 0x10), and routes to the right
    /// per-protocol handler.
    ///
    /// Required for go-ethereum's `devp2p rlpx snap-test` because the tool
    /// dials with both eth/69 and snap/1 advertised and exercises snap-protocol
    /// requests interleaved with the eth-protocol handshake.
    /// </summary>
    public class MultiProtocolRlpxSession
    {
        private readonly RlpxConnection _connection;
        private readonly Eth68ServerSession _eth;
        private readonly Snap1Handler _snap;
        private readonly int _ethOffset;
        private readonly int _ethEndExclusive;
        private readonly int _snapOffset;
        private readonly int _snapEndExclusive;

        public MultiProtocolRlpxSession(RlpxConnection connection, Eth68ServerSession eth, Snap1Handler snap)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eth = eth ?? throw new ArgumentNullException(nameof(eth));
            _snap = snap ?? throw new ArgumentNullException(nameof(snap));

            _ethOffset = _connection.GetCapabilityOffset("eth");
            var ethCap = _connection.SharedCapabilities.Find(c => c.Name == "eth");
            _ethEndExclusive = _ethOffset + ethCap.Length;

            _snapOffset = _connection.GetCapabilityOffset("snap");
            var snapCap = _connection.SharedCapabilities.Find(c => c.Name == "snap");
            _snapEndExclusive = _snapOffset + snapCap.Length;
        }

        public async Task RunAsync(TimeSpan idleTimeout = default, CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested && _connection.IsConnected)
            {
                int msgId; byte[] payload;
                if (idleTimeout > TimeSpan.Zero)
                {
                    using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    idleCts.CancelAfter(idleTimeout);
                    try
                    {
                        (msgId, payload) = await _connection.ReceiveMessageAsync(idleCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Silent multiplexed peer — close with UselessPeer so the inbound slot frees.
                        try { await _connection.DisconnectAsync(Nethereum.Model.P2P.DisconnectReason.UselessPeer); } catch { }
                        return;
                    }
                }
                else
                {
                    (msgId, payload) = await _connection.ReceiveMessageAsync(ct);
                }
                if (msgId >= _ethOffset && msgId < _ethEndExclusive)
                    await _eth.HandleEthMessageAsync(msgId - _ethOffset, payload, ct);
                else if (msgId >= _snapOffset && msgId < _snapEndExclusive)
                    await _snap.HandleSnapMessageAsync(msgId - _snapOffset, payload, ct);
                // Else: unknown capability range; per devp2p spec, silently ignore
                // (or send Disconnect ProtocolError). Production behaviour TBD.
            }
        }
    }
}
