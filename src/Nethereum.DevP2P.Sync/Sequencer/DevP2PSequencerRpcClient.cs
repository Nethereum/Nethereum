using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util.HashProviders;

using Nethereum.CoreChain.Sync;

namespace Nethereum.DevP2P.Sync.Sequencer
{
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
            var blocks = await GetBlocksWithReceiptsAsync(blockNumber, 1, cancellationToken);
            return blocks.Count > 0 ? blocks[0] : null;
        }

        public async Task<IList<LiveBlockData>> GetBlocksWithReceiptsAsync(BigInteger fromBlock, int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0) return new List<LiveBlockData>();

            var headers = await GetHeadersInternalAsync(fromBlock, count, cancellationToken);
            if (headers.Count == 0) return new List<LiveBlockData>();

            var hashes = new byte[headers.Count][];
            for (int i = 0; i < headers.Count; i++)
                hashes[i] = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(headers[i]);

            var bodies = await GetBodiesInternalAsync(hashes, cancellationToken);
            var receiptsByBlock = await GetReceiptsInternalAsync(hashes, cancellationToken);

            var result = new List<LiveBlockData>(headers.Count);
            for (int i = 0; i < headers.Count && i < bodies.Count; i++)
            {
                result.Add(new LiveBlockData
                {
                    Header = headers[i],
                    Transactions = bodies[i].Transactions,
                    Receipts = i < receiptsByBlock.Count ? receiptsByBlock[i] : new List<Receipt>(),
                    BlockHash = hashes[i],
                    IsSoft = true
                });
            }

            UpdateTipIfHigher(headers[headers.Count - 1].BlockNumber);
            return result;
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
                    ForkHash = Nethereum.Model.P2P.ForkId.ComputeHash(_genesisHash, _forkBlocks),
                    ForkNext = 0
                };
                await _connection.SendMessageAsync(
                    _ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(status));

                var (_, payload) = await _connection.ReceiveMessageAsync(cancellationToken);
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);

                _remoteTip = await DiscoverRemoteTipAsync(remoteStatus.BestHash, cancellationToken);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task<BigInteger> DiscoverRemoteTipAsync(byte[] bestHash, CancellationToken cancellationToken)
        {
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
