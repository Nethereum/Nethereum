using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Serving
{
    public class Eth68ServerSession
    {
        public const int MaxHeadersPerRequest = 1024;
        public const int MaxBodiesPerRequest = 1024;
        public const int MaxReceiptsPerRequest = 1024;
        public const int MaxBlockAccessListsPerRequest = 1024;

        public const ulong Eth70ReceiptsResponseSoftCapBytes = 2 * 1024 * 1024;

        private readonly RlpxConnection _connection;
        private readonly IEth68RequestHandler _handler;
        private readonly Eth68StatusMessage _localStatus;
        private readonly Nethereum.DevP2P.NodeDb.PersistentPeerCache? _peerCache;
        private readonly ILogger _logger;

        public int EthOffset { get; private set; }
        public Eth68StatusMessage? RemoteStatus { get; private set; }

        public event EventHandler<NewBlockHashesMessage>? NewBlockHashesReceived;
        public event EventHandler<TransactionsMessage>? TransactionsReceived;
        public event EventHandler<NewPooledTransactionHashesMessage>? NewPooledTransactionHashesReceived;
        public event EventHandler<BlockRangeUpdateMessage>? BlockRangeUpdateReceived;

        public Eth68ServerSession(
            RlpxConnection connection,
            IEth68RequestHandler handler,
            Eth68StatusMessage localStatus,
            Nethereum.DevP2P.NodeDb.PersistentPeerCache? peerCache = null,
            ILogger<Eth68ServerSession>? logger = null)
        {
            _connection = connection;
            _handler = handler;
            _localStatus = localStatus;
            _peerCache = peerCache;
            _logger = logger ?? (ILogger)NullLogger<Eth68ServerSession>.Instance;
        }

        public void BindCapabilityOffset(int ethOffset)
        {
            EthOffset = ethOffset;
        }

        public void BindRemoteStatus(Eth68StatusMessage remoteStatus)
        {
            RemoteStatus = remoteStatus;
        }

        public async Task ExchangeStatusAsync(CancellationToken cancellationToken = default)
        {
            EthOffset = _connection.GetCapabilityOffset("eth");

            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.Status,
                Eth68StatusMessageEncoder.Encode(_localStatus),
                cancellationToken);

            var (msgId, payload) = await _connection.ReceiveStatusAsync(cancellationToken);
            if (msgId != EthOffset + Eth68MessageIds.Status)
                throw new InvalidOperationException($"Expected Status, got 0x{msgId:x2}");

            RemoteStatus = Eth68StatusMessageEncoder.Decode(payload);
        }

        public async Task RunAsync(TimeSpan idleTimeout = default, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _connection.IsConnected)
            {
                int msgId; byte[] payload;
                if (idleTimeout > TimeSpan.Zero)
                {
                    try
                    {
                        (msgId, payload) = await _connection.ReceiveMessageAsync(idleTimeout, cancellationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        try { await _connection.DisconnectAsync(DisconnectReason.UselessPeer); } catch { }
                        return;
                    }
                }
                else
                {
                    (msgId, payload) = await _connection.ReceiveMessageAsync(_connection.FrameReadTimeout, cancellationToken);
                }
                using var lease = await _connection.AcquireDecodeLeaseAsync(payload.Length, cancellationToken);
                await HandleEthMessageAsync(msgId - EthOffset, payload, lease, cancellationToken);
            }
        }

        public Task HandleEthMessageAsync(int localId, byte[] payload, CancellationToken cancellationToken = default)
            => HandleEthMessageAsync(localId, payload, null, cancellationToken);

        public async Task HandleEthMessageAsync(int localId, byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken = default)
        {
            switch (localId)
            {
                case EthMessageIds.GetBlockHeaders:
                    await HandleGetHeadersAsync(payload, lease, cancellationToken);
                    break;
                case EthMessageIds.GetBlockBodies:
                    await HandleGetBodiesAsync(payload, lease, cancellationToken);
                    break;
                case EthMessageIds.GetReceipts:
                    await HandleGetReceiptsAsync(payload, lease, cancellationToken);
                    break;
                case EthMessageIds.GetPooledTransactions:
                    await HandleGetPooledTransactionsAsync(payload, lease, cancellationToken);
                    break;
                case EthMessageIds.GetBlockAccessLists:
                    if (RemoteStatus == null || RemoteStatus.ProtocolVersion < 71)
                    {
                        await ProtocolBreachAsync(localId, "GetBlockAccessLists received on eth/<71 (only valid on eth/71+)");
                        return;
                    }
                    await HandleGetBlockAccessListsAsync(payload, lease, cancellationToken);
                    break;
                case EthMessageIds.NewBlock:
                    if (NegotiatedEthVersion() >= 69)
                    {
                        await ProtocolBreachAsync(localId, "NewBlock received on eth/69+ (only valid on eth/68)");
                        return;
                    }
                    await HandlePushAsync<NewBlockMessage>(
                        localId, payload,
                        static p => NewBlockMessageEncoder.Decode(p),
                        static (_, _) => true,
                        null);
                    break;
                case EthMessageIds.NewBlockHashes:
                    if (NegotiatedEthVersion() >= 69)
                    {
                        await ProtocolBreachAsync(localId, "NewBlockHashes received on eth/69+ (only valid on eth/68)");
                        return;
                    }
                    await HandlePushAsync<NewBlockHashesMessage>(
                        localId, payload,
                        static p => NewBlockHashesMessageEncoder.Decode(p),
                        static (_, _) => true,
                        NewBlockHashesReceived);
                    break;
                case EthMessageIds.Transactions:
                    await HandlePushAsync<TransactionsMessage>(
                        localId, payload,
                        static p => TransactionsMessageEncoder.Decode(p),
                        static (m, _) => m.Transactions != null,
                        TransactionsReceived);
                    break;
                case EthMessageIds.NewPooledTransactionHashes:
                    await HandlePushAsync<NewPooledTransactionHashesMessage>(
                        localId, payload,
                        static p => NewPooledTransactionHashesMessageEncoder.Decode(p),
                        static (m, _) =>
                            m.Types != null && m.Sizes != null && m.Hashes != null &&
                            m.Types.Length == m.Sizes.Count &&
                            m.Sizes.Count == m.Hashes.Count,
                        NewPooledTransactionHashesReceived);
                    break;
                case EthMessageIds.BlockRangeUpdate:
                    if (RemoteStatus == null || RemoteStatus.ProtocolVersion < 69)
                    {
                        await ProtocolBreachAsync(localId, "BlockRangeUpdate received on eth/68 (only valid on eth/69+)");
                        return;
                    }
                    await HandlePushAsync<BlockRangeUpdateMessage>(
                        localId, payload,
                        static p => BlockRangeUpdateMessageEncoder.Decode(p),
                        static (m, _) => BlockRangeValidator.Violation(m.EarliestBlock, m.LatestBlock, m.LatestBlockHash) == null,
                        BlockRangeUpdateReceived);
                    break;
                default:
                    await ProtocolBreachAsync(localId, "message code not handled by the negotiated eth version or a response without a matching request");
                    return;
            }
        }

        private int NegotiatedEthVersion()
            => RemoteStatus?.ProtocolVersion ?? _connection.SharedCapabilities?.Find(c => c.Name == "eth")?.Version ?? 68;

        private async Task HandlePushAsync<T>(
            int localId,
            byte[] payload,
            Func<byte[], T> decoder,
            Func<T, Eth68ServerSession, bool> bodyValidator,
            EventHandler<T>? subscribers)
        {
            T decoded;
            try
            {
                decoded = decoder(payload);
            }
            catch (Exception ex)
            {
                _peerCache?.RecordFailure(_connection.RemoteEndpoint ?? string.Empty);
                _logger.LogWarning(ex, "eth decode failed for msgId 0x{MsgId:X2} from {Peer}; ProtocolBreach", localId, _connection.RemoteEndpoint);
                await SafeDisconnectAsync(DisconnectReason.ProtocolBreach);
                return;
            }

            if (!bodyValidator(decoded, this))
            {
                _peerCache?.RecordFailure(_connection.RemoteEndpoint ?? string.Empty);
                _logger.LogWarning("eth body-rule violation for msgId 0x{MsgId:X2} from {Peer}; ProtocolBreach", localId, _connection.RemoteEndpoint);
                await SafeDisconnectAsync(DisconnectReason.ProtocolBreach);
                return;
            }

            if (subscribers == null) return;
            try
            {
                subscribers.Invoke(this, decoded);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "eth subscriber threw on msgId 0x{MsgId:X2} from {Peer}; session continues", localId, _connection.RemoteEndpoint);
            }
        }

        private static byte[][] Clamp(byte[][] hashes, int max)
        {
            if (hashes.Length <= max) return hashes;
            var clamped = new byte[max][];
            Array.Copy(hashes, clamped, max);
            return clamped;
        }

        private async Task ProtocolBreachAsync(int localId, string reason)
        {
            _peerCache?.RecordFailure(_connection.RemoteEndpoint ?? string.Empty);
            _logger.LogWarning("eth ProtocolBreach on msgId 0x{MsgId:X2} from {Peer}: {Reason}", localId, _connection.RemoteEndpoint, reason);
            await SafeDisconnectAsync(DisconnectReason.ProtocolBreach);
        }

        private async Task SafeDisconnectAsync(DisconnectReason reason)
        {
            try { await _connection.DisconnectAsync(reason); }
            catch { }
        }

        private async Task HandleGetHeadersAsync(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var request = GetBlockHeadersMessageEncoder.Decode(payload);
            lease?.Dispose();
            if (request.Limit > (ulong)MaxHeadersPerRequest)
                request.Limit = MaxHeadersPerRequest;
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

        private async Task HandleGetBodiesAsync(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var request = GetBlockBodiesMessageEncoder.Decode(payload);
            lease?.Dispose();
            var bodies = await _handler.GetBodiesAsync(Clamp(request.BlockHashes, MaxBodiesPerRequest), cancellationToken);
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

        private async Task HandleGetReceiptsAsync(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var ethVersion = _connection.SharedCapabilities?.Find(c => c.Name == "eth")?.Version ?? 68;
            if (ethVersion >= 70)
            {
                await HandleGetReceipts70Async(payload, lease, cancellationToken);
                return;
            }

            var request = GetReceiptsMessageEncoder.Decode(payload);
            lease?.Dispose();
            var receipts = await _handler.GetReceiptsAsync(Clamp(request.BlockHashes, MaxReceiptsPerRequest), cancellationToken);
            var response = new ReceiptsMessage
            {
                RequestId = request.RequestId,
                ReceiptsByBlock = receipts
            };
            var encoded = ethVersion >= 69
                ? ReceiptsMessageEth69Encoder.Encode(response)
                : ReceiptsMessageEncoder.Encode(response);
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.Receipts,
                encoded,
                cancellationToken);
        }

        private async Task HandleGetReceipts70Async(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var request = GetReceiptsMessage70Encoder.Decode(payload);
            lease?.Dispose();
            var served = await _handler.GetReceipts70Async(
                Clamp(request.BlockHashes, MaxReceiptsPerRequest), request.FirstBlockReceiptIndex, Eth70ReceiptsResponseSoftCapBytes, cancellationToken);
            var response = new ReceiptsMessage70
            {
                RequestId = request.RequestId,
                LastBlockIncomplete = served.LastBlockIncomplete,
                ReceiptsByBlock = served.ReceiptsByBlock
            };
            await _connection.SendMessageAsync(
                EthOffset + Eth68MessageIds.Receipts,
                ReceiptsMessageEth70Encoder.Encode(response),
                cancellationToken);
        }

        private async Task HandleGetBlockAccessListsAsync(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var request = GetBlockAccessListsMessageEncoder.Decode(payload);
            lease?.Dispose();
            var bals = await _handler.GetBlockAccessListsAsync(Clamp(request.BlockHashes, MaxBlockAccessListsPerRequest), cancellationToken);
            var response = new BlockAccessListsMessage
            {
                RequestId = request.RequestId,
                BlockAccessListsByBlock = bals
            };
            await _connection.SendMessageAsync(
                EthOffset + EthMessageIds.BlockAccessLists,
                BlockAccessListsMessageEncoder.Encode(response),
                cancellationToken);
        }

        private async Task HandleGetPooledTransactionsAsync(byte[] payload, InFlightDecodeLease? lease, CancellationToken cancellationToken)
        {
            var request = GetPooledTransactionsMessageEncoder.Decode(payload);
            lease?.Dispose();
            var txs = await _handler.GetPooledTransactionsAsync(
                request.Hashes.ToArray(), cancellationToken);
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
