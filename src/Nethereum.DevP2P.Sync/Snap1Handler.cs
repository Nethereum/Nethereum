using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Dispatches an already-classified snap/1 wire message (localId = msgId - SnapOffset)
    /// to an ISnapRequestHandler and writes the typed response back to the
    /// RlpxConnection. Pure dispatch — no own receive loop. Multiplexed
    /// alongside eth via MultiProtocolRlpxSession.
    /// </summary>
    public class Snap1Handler
    {
        private readonly RlpxConnection _connection;
        private readonly ISnapRequestHandler _handler;
        public int SnapOffset { get; private set; }

        public Snap1Handler(RlpxConnection connection, ISnapRequestHandler handler)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            SnapOffset = _connection.GetCapabilityOffset("snap");
        }

        public async Task HandleSnapMessageAsync(int localId, byte[] payload, CancellationToken ct = default)
        {
            switch (localId)
            {
                case SnapMessageIds.GetAccountRange:
                {
                    var req = GetAccountRangeMessageEncoder.Decode(payload);
                    var resp = await _handler.GetAccountRangeAsync(req, ct);
                    await _connection.SendMessageAsync(
                        SnapOffset + SnapMessageIds.AccountRange,
                        AccountRangeMessageEncoder.Encode(resp), ct);
                    break;
                }
                case SnapMessageIds.GetStorageRanges:
                {
                    var req = GetStorageRangesMessageEncoder.Decode(payload);
                    var resp = await _handler.GetStorageRangesAsync(req, ct);
                    await _connection.SendMessageAsync(
                        SnapOffset + SnapMessageIds.StorageRanges,
                        StorageRangesMessageEncoder.Encode(resp), ct);
                    break;
                }
                case SnapMessageIds.GetByteCodes:
                {
                    var req = GetByteCodesMessageEncoder.Decode(payload);
                    var resp = await _handler.GetByteCodesAsync(req, ct);
                    await _connection.SendMessageAsync(
                        SnapOffset + SnapMessageIds.ByteCodes,
                        ByteCodesMessageEncoder.Encode(resp), ct);
                    break;
                }
                case SnapMessageIds.GetTrieNodes:
                {
                    var req = GetTrieNodesMessageEncoder.Decode(payload);
                    var resp = await _handler.GetTrieNodesAsync(req, ct);
                    await _connection.SendMessageAsync(
                        SnapOffset + SnapMessageIds.TrieNodes,
                        TrieNodesMessageEncoder.Encode(resp), ct);
                    break;
                }
                default:
                    // Snap response messages (AccountRange/StorageRanges/etc.)
                    // arriving from a peer mean the peer is responding to OUR
                    // GetXxx requests — currently no client-side snap pulls
                    // happen on this connection, so safe to ignore.
                    break;
            }
        }
    }
}
