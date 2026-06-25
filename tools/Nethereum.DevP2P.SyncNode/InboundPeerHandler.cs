using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Wires an inbound RLPx connection through the eth/68 handshake into our
    /// existing <see cref="StorageBackedEth68Handler"/> so we serve headers /
    /// bodies / receipts / pooled-txs to peers that dial us. The handshake
    /// mirrors the remote peer's Status — same trick
    /// <see cref="MainnetPeerSession"/> uses for outbound — so the peer
    /// accepts our reply regardless of our actual chain head. They'll
    /// disconnect on first failed data request (we return empty for blocks
    /// we don't have), but the listener-side code paths get exercised and
    /// any from-genesis follower can actually use us.
    /// </summary>
    internal static class InboundPeerHandler
    {
        public static async Task RunAsync(
            RlpxConnection connection,
            Nethereum.CoreChain.Storage.IChainStoreBundle bundle,
            Action<string> log,
            CancellationToken ct)
        {
            try
            {
                var ethCap = connection.SharedCapabilities.Find(c => c.Name == "eth");
                if (ethCap == null)
                {
                    log($"  inbound peer {connection.RemoteEndpoint}: no eth capability — closing");
                    await connection.DisconnectAsync(DisconnectReason.IncompatibleVersion);
                    return;
                }
                var ethOffset = connection.GetCapabilityOffset("eth");

                // Receive remote Status first.
                var (msgId, payload) = await connection.ReceiveMessageAsync(ct);
                if (msgId != ethOffset + Eth68MessageIds.Status)
                {
                    log($"  inbound peer {connection.RemoteEndpoint}: expected Status, got msgId=0x{msgId:x2}");
                    await connection.DisconnectAsync(DisconnectReason.ProtocolBreach);
                    return;
                }
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);
                log($"  inbound peer {connection.RemoteEndpoint}: " +
                    $"eth/{ethCap.Version} chain={remoteStatus.NetworkId} " +
                    $"head=0x{(remoteStatus.BestHash != null ? remoteStatus.BestHash.ToHex().Substring(0, 16) : "?")}…");

                // Mirror back. Peer accepts because we look chain-compatible.
                await connection.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                    {
                        ProtocolVersion = ethCap.Version,
                        NetworkId = remoteStatus.NetworkId,
                        TotalDifficulty = remoteStatus.TotalDifficulty,
                        BestHash = remoteStatus.BestHash,
                        GenesisHash = remoteStatus.GenesisHash,
                        ForkHash = remoteStatus.ForkHash,
                        ForkNext = remoteStatus.ForkNext,
                    }),
                    ct);

                // Hand off to the server session that dispatches GetBlockHeaders,
                // GetBlockBodies, GetReceipts, GetPooledTransactions. The handler
                // reads from our backend stores.
                var handler = new StorageBackedEth68Handler(
                    bundle.Blocks,
                    bundle.Transactions,
                    bundle.Receipts,
                    txPool: null);

                // Custom message loop — Eth68ServerSession.RunAsync expects to
                // exchange Status itself, which we already did above. Inline a
                // minimal dispatch loop instead.
                while (connection.IsConnected && !ct.IsCancellationRequested)
                {
                    var (id, body) = await connection.ReceiveMessageAsync(ct);
                    var ethId = id - ethOffset;
                    // The server doesn't act on every eth message — it just
                    // serves read requests. Unsolicited push messages (NewBlock,
                    // Transactions, NewPooledTransactionHashes) are silently
                    // accepted, no work.
                    if (ethId == Eth68MessageIds.GetBlockHeaders)
                    {
                        var req = GetBlockHeadersMessageEncoder.Decode(body);
                        var headers = await handler.GetHeadersAsync(req, ct);
                        await connection.SendMessageAsync(
                            ethOffset + Eth68MessageIds.BlockHeaders,
                            BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
                            {
                                RequestId = req.RequestId,
                                Headers = (System.Collections.Generic.List<Nethereum.Model.BlockHeader>)headers,
                            }),
                            ct);
                    }
                    else if (ethId == Eth68MessageIds.GetBlockBodies)
                    {
                        var req = GetBlockBodiesMessageEncoder.Decode(body);
                        var bodies = await handler.GetBodiesAsync(req.BlockHashes, ct);
                        await connection.SendMessageAsync(
                            ethOffset + Eth68MessageIds.BlockBodies,
                            BlockBodiesMessageEncoder.Encode(new BlockBodiesMessage
                            {
                                RequestId = req.RequestId,
                                Bodies = (System.Collections.Generic.List<BlockBody>)bodies,
                            }),
                            ct);
                    }
                    // Other request types ignored — peer will eventually disconnect.
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                log($"  inbound peer {connection.RemoteEndpoint} session ended: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                try { await connection.DisconnectAsync(DisconnectReason.ClientQuitting); }
                catch { }
            }
        }
    }
}
