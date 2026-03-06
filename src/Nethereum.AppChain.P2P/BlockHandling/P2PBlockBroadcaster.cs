using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.P2P;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.AppChain.P2P.BlockHandling
{
    public class P2PBlockBroadcaster
    {
        private readonly IP2PTransport _transport;
        private readonly ILogger<P2PBlockBroadcaster>? _logger;

        public P2PBlockBroadcaster(IP2PTransport transport, ILogger<P2PBlockBroadcaster>? logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _logger = logger;
        }

        public async Task BroadcastBlockAsync(BlockHeader header, byte[][] transactionHashes)
        {
            try
            {
                _logger?.LogDebug("[P2P Broadcast] Broadcasting block {BlockNumber}, ParentHash: {ParentHash}",
                    header.BlockNumber,
                    header.ParentHash?.ToHex(true) ?? "null");

                var headerBytes = BlockHeaderEncoder.Current.Encode(header);

                _logger?.LogDebug("[P2P Broadcast] Encoded header to {Length} bytes", headerBytes.Length);

                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms);

                writer.Write(headerBytes.Length);
                writer.Write(headerBytes);
                writer.Write(transactionHashes.Length);

                foreach (var txHash in transactionHashes)
                {
                    if (txHash != null)
                    {
                        writer.Write(txHash.Length);
                        writer.Write(txHash);
                    }
                }

                var blockMsg = new P2PMessage(P2PMessageType.NewBlock, ms.ToArray());
                var peerCount = _transport.ConnectedPeers?.Count ?? 0;

                await _transport.BroadcastAsync(blockMsg);

                _logger?.LogInformation("[P2P Broadcast] Block {BlockNumber} broadcast to {PeerCount} peers",
                    header.BlockNumber, peerCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[P2P Broadcast] Failed to broadcast block {BlockNumber}", header.BlockNumber);
                throw;
            }
        }
    }
}
