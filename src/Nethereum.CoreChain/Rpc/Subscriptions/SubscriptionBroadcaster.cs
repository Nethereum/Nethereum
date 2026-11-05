using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Model;

namespace Nethereum.CoreChain.Rpc.Subscriptions
{
    public sealed class SubscriptionBroadcaster : IDisposable
    {
        public const int QueuedBlocks = 128;

        private readonly Channel<ProducedBlock> _blocks = Channel.CreateBounded<ProducedBlock>(
            new BoundedChannelOptions(QueuedBlocks)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

        private readonly WebSocketRpcHandler _handler;
        private readonly Func<BlockHeader, Task<List<FilteredLog>>> _logsOfBlock;
        private readonly Action<BlockHeader, Exception> _onFailed;
        private readonly Task _worker;

        public SubscriptionBroadcaster(
            WebSocketRpcHandler handler,
            Func<BlockHeader, Task<List<FilteredLog>>> logsOfBlock,
            Action<BlockHeader, Exception> onFailed)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logsOfBlock = logsOfBlock ?? throw new ArgumentNullException(nameof(logsOfBlock));
            _onFailed = onFailed ?? throw new ArgumentNullException(nameof(onFailed));
            _worker = Task.Run(BroadcastAsync);
        }

        public void Enqueue(BlockHeader header, byte[] blockHash) =>
            _blocks.Writer.TryWrite(new ProducedBlock(header, blockHash));

        public void Dispose() => _blocks.Writer.TryComplete();

        private async Task BroadcastAsync()
        {
            await foreach (var block in _blocks.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    var logs = await _logsOfBlock(block.Header).ConfigureAwait(false);
                    await _handler.BroadcastBlockAsync(block.Header, block.Hash, logs).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _onFailed(block.Header, ex);
                }
            }
        }

        private readonly struct ProducedBlock
        {
            public ProducedBlock(BlockHeader header, byte[] hash)
            {
                Header = header;
                Hash = hash;
            }

            public BlockHeader Header { get; }
            public byte[] Hash { get; }
        }
    }
}
