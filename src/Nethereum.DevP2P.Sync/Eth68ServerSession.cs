using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Runs the eth/68 server-side message loop on an accepted RlpxConnection:
    /// exchanges Status with the remote peer, then dispatches incoming
    /// GetBlockHeaders / GetBlockBodies / GetReceipts to an IEth68RequestHandler
    /// and writes back the typed response. Ping/Pong is handled by RlpxConnection.
    /// </summary>
    public class Eth68ServerSession
    {
        /// <summary>
        /// Per-request response caps matching geth's eth/handler_eth.go:
        /// answering more than this on a single GetX request is a DoS surface
        /// (memory + bandwidth amplification), so a peer asking for more
        /// gets a ProtocolBreach disconnect.
        /// </summary>
        public const int MaxHeadersPerRequest = 192;
        public const int MaxBodiesPerRequest = 1024;
        public const int MaxReceiptsPerRequest = 256;
        public const int MaxPooledTransactionsPerRequest = 256;

        private readonly RlpxConnection _connection;
        private readonly IEth68RequestHandler _handler;
        private readonly Eth68StatusMessage _localStatus;

        public int EthOffset { get; private set; }
        public Eth68StatusMessage? RemoteStatus { get; private set; }

        public event EventHandler<NewBlockMessage>? NewBlockReceived;
        public event EventHandler<NewBlockHashesMessage>? NewBlockHashesReceived;
        public event EventHandler<TransactionsMessage>? TransactionsReceived;
        public event EventHandler<NewPooledTransactionHashesMessage>? NewPooledTransactionHashesReceived;
        public event EventHandler<BlockRangeUpdateMessage>? BlockRangeUpdateReceived;

        public Eth68ServerSession(
            RlpxConnection connection,
            IEth68RequestHandler handler,
            Eth68StatusMessage localStatus)
        {
            _connection = connection;
            _handler = handler;
            _localStatus = localStatus;
        }

        /// <summary>
        /// Stamps the eth capability offset for callers that have already
        /// driven the Status exchange themselves (e.g. an inbound listener
        /// that mirrors remote Status before constructing the session).
        /// Must be invoked before <see cref="RunAsync"/>.
        /// </summary>
        public void BindCapabilityOffset(int ethOffset)
        {
            EthOffset = ethOffset;
        }

        public async Task ExchangeStatusAsync(CancellationToken cancellationToken = default)
        {
            EthOffset = _connection.GetCapabilityOffset("eth");

            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(_localStatus),
                cancellationToken);

            var (msgId, payload) = await _connection.ReceiveMessageAsync(cancellationToken);
            if (msgId != EthOffset + Eth68MessageIds.Status)
                throw new InvalidOperationException($"Expected Status, got 0x{msgId:x2}");

            RemoteStatus = Eth68StatusMessageEncoder.Decode(payload);
        }

        /// <summary>
        /// Drives the message loop until the connection drops, the caller cancels,
        /// or <paramref name="idleTimeout"/> elapses without an inbound message.
        /// On idle timeout, sends Disconnect(UselessPeer) and returns — silent peers
        /// occupying inbound slots without sending anything are a DoS surface
        /// (slow-loris on the eth/68 server). Pass TimeSpan.Zero (default) to
        /// disable for back-compat with callers that don't yet wire the cap.
        /// </summary>
        public async Task RunAsync(TimeSpan idleTimeout = default, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _connection.IsConnected)
            {
                int msgId; byte[] payload;
                if (idleTimeout > TimeSpan.Zero)
                {
                    using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    idleCts.CancelAfter(idleTimeout);
                    try
                    {
                        (msgId, payload) = await _connection.ReceiveMessageAsync(idleCts.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        try { await _connection.DisconnectAsync(DisconnectReason.UselessPeer); } catch { }
                        return;
                    }
                }
                else
                {
                    (msgId, payload) = await _connection.ReceiveMessageAsync(cancellationToken);
                }
                await HandleEthMessageAsync(msgId - EthOffset, payload, cancellationToken);
            }
        }

        /// <summary>
        /// Dispatch a single already-classified eth message (localId = msgId - EthOffset).
        /// Exposed so a multiplexed-protocol session (e.g. eth+snap on the same
        /// RlpxConnection) can route by capability offset and call us only for
        /// messages in the eth range.
        /// </summary>
        public async Task HandleEthMessageAsync(int localId, byte[] payload, CancellationToken cancellationToken = default)
        {
            switch (localId)
            {
                case EthMessageIds.GetBlockHeaders:
                    await HandleGetHeadersAsync(payload, cancellationToken);
                    break;
                case EthMessageIds.GetBlockBodies:
                    await HandleGetBodiesAsync(payload, cancellationToken);
                    break;
                case EthMessageIds.GetReceipts:
                    await HandleGetReceiptsAsync(payload, cancellationToken);
                    break;
                case EthMessageIds.GetPooledTransactions:
                    await HandleGetPooledTransactionsAsync(payload, cancellationToken);
                    break;
                case EthMessageIds.NewBlock:
                    try { NewBlockReceived?.Invoke(this, NewBlockMessageEncoder.Decode(payload)); } catch { }
                    break;
                case EthMessageIds.NewBlockHashes:
                    try { NewBlockHashesReceived?.Invoke(this, NewBlockHashesMessageEncoder.Decode(payload)); } catch { }
                    break;
                case EthMessageIds.Transactions:
                    try { TransactionsReceived?.Invoke(this, TransactionsMessageEncoder.Decode(payload)); } catch { }
                    break;
                case EthMessageIds.NewPooledTransactionHashes:
                    try { NewPooledTransactionHashesReceived?.Invoke(this, NewPooledTransactionHashesMessageEncoder.Decode(payload)); } catch { }
                    break;
                case EthMessageIds.BlockRangeUpdate:
                    try { BlockRangeUpdateReceived?.Invoke(this, BlockRangeUpdateMessageEncoder.Decode(payload)); } catch { }
                    break;
                default:
                    break;
            }
        }

        private async Task HandleGetHeadersAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var request = GetBlockHeadersMessageEncoder.Decode(payload);
            if (request.Limit > (ulong)MaxHeadersPerRequest)
            {
                try { await _connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                return;
            }
            var headers = await _handler.GetHeadersAsync(request, cancellationToken);
            var response = new BlockHeadersMessage
            {
                RequestId = request.RequestId,
                Headers = new System.Collections.Generic.List<Nethereum.Model.BlockHeader>(headers)
            };
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.BlockHeaders,
                BlockHeadersMessageEncoder.Encode(response),
                cancellationToken);
        }

        private async Task HandleGetBodiesAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var request = GetBlockBodiesMessageEncoder.Decode(payload);
            if (request.BlockHashes.Length > MaxBodiesPerRequest)
            {
                try { await _connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                return;
            }
            var bodies = await _handler.GetBodiesAsync(request.BlockHashes, cancellationToken);
            var response = new BlockBodiesMessage
            {
                RequestId = request.RequestId,
                Bodies = new System.Collections.Generic.List<BlockBody>(bodies)
            };
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.BlockBodies,
                BlockBodiesMessageEncoder.Encode(response),
                cancellationToken);
        }

        private async Task HandleGetReceiptsAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var request = GetReceiptsMessageEncoder.Decode(payload);
            if (request.BlockHashes.Length > MaxReceiptsPerRequest)
            {
                try { await _connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                return;
            }
            var receipts = await _handler.GetReceiptsAsync(request.BlockHashes, cancellationToken);
            var response = new ReceiptsMessage
            {
                RequestId = request.RequestId,
                ReceiptsByBlock = receipts
            };
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.Receipts,
                ReceiptsMessageEncoder.Encode(response),
                cancellationToken);
        }

        private async Task HandleGetPooledTransactionsAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var request = GetPooledTransactionsMessageEncoder.Decode(payload);
            if (request.Hashes.Count > MaxPooledTransactionsPerRequest)
            {
                try { await _connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                return;
            }
            var txs = await _handler.GetPooledTransactionsAsync(request.Hashes.ToArray(), cancellationToken);
            var response = new PooledTransactionsMessage
            {
                RequestId = request.RequestId,
                Transactions = new System.Collections.Generic.List<Nethereum.Model.ISignedTransaction>(txs)
            };
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.PooledTransactions,
                PooledTransactionsMessageEncoder.Encode(response),
                cancellationToken);
        }
    }
}
