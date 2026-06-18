using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// ISequencerRpcClient implementation backed by an eth/68 DevP2P session.
    /// Connects lazily on first request, performs RLPx + eth/68 Status handshake,
    /// then services block/header/receipt requests via GetBlockHeaders, GetBlockBodies,
    /// and GetReceipts messages.
    /// </summary>
    public class DevP2PSequencerRpcClient : ISequencerRpcClient, IAsyncDisposable
    {
        private readonly string _enode;
        private readonly DevP2PConfig _config;
        private readonly byte[] _genesisHash;
        private readonly ulong[] _forkBlocks;

        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private RlpxConnection? _connection;
        private int _ethOffset;
        private BigInteger _remoteTip;

        /// <summary>
        /// Fires when the remote peer pushes a NewBlock message during a pending
        /// request. (Pushes that arrive between requests stay queued in the OS
        /// TCP buffer and surface on the next request — for fully continuous
        /// reception use a dedicated Eth68ServerSession instead.)
        /// </summary>
        public event EventHandler<NewBlockMessage>? NewBlockReceived;
        public event EventHandler<NewBlockHashesMessage>? NewBlockHashesReceived;
        public event EventHandler<TransactionsMessage>? TransactionsReceived;
        public event EventHandler<NewPooledTransactionHashesMessage>? NewPooledTransactionHashesReceived;
        public event EventHandler<BlockRangeUpdateMessage>? BlockRangeUpdateReceived;

        public DevP2PSequencerRpcClient(
            string enode,
            DevP2PConfig config,
            byte[] genesisHash,
            ulong[]? forkBlocks = null)
        {
            _enode = enode ?? throw new ArgumentNullException(nameof(enode));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _genesisHash = genesisHash ?? throw new ArgumentNullException(nameof(genesisHash));
            _forkBlocks = forkBlocks ?? Array.Empty<ulong>();
        }

        public async Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);
            return _remoteTip;
        }

        public async Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var headers = await GetHeadersInternalAsync(blockNumber, 1, cancellationToken);
            return headers.Count > 0 ? headers[0] : null;
        }

        public async Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var header = await GetBlockHeaderAsync(blockNumber, cancellationToken);
            return header != null
                ? RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header)
                : null;
        }

        public async Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            var header = await GetBlockHeaderAsync(blockNumber, cancellationToken);
            if (header == null) return null;

            var blockHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
            var hashes = new[] { blockHash };

            var bodies = await GetBodiesInternalAsync(hashes, cancellationToken);
            if (bodies.Count == 0) return null;

            var receiptsByBlock = await GetReceiptsInternalAsync(hashes, cancellationToken);
            var receipts = receiptsByBlock.Count > 0 ? receiptsByBlock[0] : new List<Receipt>();

            UpdateTipIfHigher(header.BlockNumber);

            return new LiveBlockData
            {
                Header = header,
                Transactions = bodies[0].Transactions,
                Receipts = receipts,
                BlockHash = blockHash,
                IsSoft = true
            };
        }

        private async Task<IList<BlockHeader>> GetHeadersInternalAsync(
            BigInteger startBlock, int count, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken);
            var conn = _connection!;

            var request = new GetBlockHeadersMessage
            {
                RequestId = conn.NextRequestId(),
                StartBlock = (ulong)startBlock,
                Limit = (ulong)count,
                Skip = 0,
                Reverse = false
            };

            var (_, payload) = await conn.RequestAsync(
                _ethOffset + Eth68MessageIds.GetBlockHeaders,
                GetBlockHeadersMessageEncoder.Encode(request),
                _ethOffset + Eth68MessageIds.BlockHeaders);

            return BlockHeadersMessageEncoder.Decode(payload).Headers;
        }

        private async Task<IList<BlockBody>> GetBodiesInternalAsync(
            byte[][] blockHashes, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken);
            var conn = _connection!;

            var request = new GetBlockBodiesMessage
            {
                RequestId = conn.NextRequestId(),
                BlockHashes = blockHashes
            };

            var (_, payload) = await conn.RequestAsync(
                _ethOffset + Eth68MessageIds.GetBlockBodies,
                GetBlockBodiesMessageEncoder.Encode(request),
                _ethOffset + Eth68MessageIds.BlockBodies);

            return BlockBodiesMessageEncoder.Decode(payload).Bodies;
        }

        private async Task<List<List<Receipt>>> GetReceiptsInternalAsync(
            byte[][] blockHashes, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken);
            var conn = _connection!;

            var request = new GetReceiptsMessage
            {
                RequestId = conn.NextRequestId(),
                BlockHashes = blockHashes
            };

            var (_, payload) = await conn.RequestAsync(
                _ethOffset + Eth68MessageIds.GetReceipts,
                GetReceiptsMessageEncoder.Encode(request),
                _ethOffset + Eth68MessageIds.Receipts);

            return ReceiptsMessageEncoder.Decode(payload).ReceiptsByBlock;
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_connection != null && _connection.IsConnected) return;

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection != null && _connection.IsConnected) return;

                var connector = new StaticPeerConnector(config: _config);
                _connection = await connector.ConnectAsync(_enode, cancellationToken);
                _ethOffset = _connection.GetCapabilityOffset("eth");
                _connection.PushMessageReceived += OnPushMessageReceived;

                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = _config.NetworkId,
                    TotalDifficulty = BigInteger.One,
                    BestHash = _genesisHash,
                    GenesisHash = _genesisHash,
                    ForkHash = ForkId.ComputeHash(_genesisHash, _forkBlocks),
                    ForkNext = 0
                };
                await _connection.SendMessageAsync(
                    _ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(status));

                var (_, payload) = await _connection.ReceiveMessageAsync(cancellationToken);
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);

                // Discover remote tip: query the header for remoteStatus.BestHash.
                // For v1 simplicity, just walk forward from 0 with one big header
                // request. Future: track tip via NewBlock notifications.
                _remoteTip = await DiscoverRemoteTipAsync(remoteStatus.BestHash, cancellationToken);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task<BigInteger> DiscoverRemoteTipAsync(byte[] bestHash, CancellationToken cancellationToken)
        {
            // Walk forward from block 0 in chunks until we hit an empty response (past tip).
            // This is O(N/chunk) round-trips; for the 2-follower test (~5-20 blocks) that's fine.
            // Production would use NewBlock push notifications + cached tip.
            const int chunkSize = 192;
            ulong nextStart = 0;
            ulong lastKnown = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var headers = await GetHeadersInternalAsync(nextStart, chunkSize, cancellationToken);
                if (headers.Count == 0) break;
                lastKnown = (ulong)headers[headers.Count - 1].BlockNumber;
                if (headers.Count < chunkSize) break;
                nextStart = lastKnown + 1;
            }
            return lastKnown;
        }

        private void UpdateTipIfHigher(BigInteger blockNumber)
        {
            if (blockNumber > _remoteTip)
                _remoteTip = blockNumber;
        }

        private void OnPushMessageReceived(object? sender, RlpxConnection.RlpxPushMessageEventArgs e)
        {
            var localId = e.MessageId - _ethOffset;
            switch (localId)
            {
                case EthMessageIds.NewBlock:
                    DispatchPush(localId, e.Payload, NewBlockMessageEncoder.Decode, NewBlockReceived);
                    break;
                case EthMessageIds.NewBlockHashes:
                    DispatchPush(localId, e.Payload, NewBlockHashesMessageEncoder.Decode, NewBlockHashesReceived);
                    break;
                case EthMessageIds.Transactions:
                    DispatchPush(localId, e.Payload, TransactionsMessageEncoder.Decode, TransactionsReceived);
                    break;
                case EthMessageIds.NewPooledTransactionHashes:
                    DispatchPush(localId, e.Payload, NewPooledTransactionHashesMessageEncoder.Decode, NewPooledTransactionHashesReceived);
                    break;
                case EthMessageIds.BlockRangeUpdate:
                    DispatchPush(localId, e.Payload, BlockRangeUpdateMessageEncoder.Decode, BlockRangeUpdateReceived);
                    break;
            }
        }

        private void DispatchPush<T>(int localId, byte[] payload, Func<byte[], T> decoder, EventHandler<T>? subscribers)
        {
            T decoded;
            try
            {
                decoded = decoder(payload);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                _ = SafeDisconnectAsync(DisconnectReason.ProtocolBreach);
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
            catch
            {
            }
        }

        private async Task SafeDisconnectAsync(DisconnectReason reason)
        {
            var conn = _connection;
            if (conn == null) return;
            try { await conn.DisconnectAsync(reason); }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                try { await _connection.DisconnectAsync(); } catch { }
                _connection = null;
            }
        }
    }
}
