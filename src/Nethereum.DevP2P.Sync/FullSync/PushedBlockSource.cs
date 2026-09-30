using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class PushedBlockSource : IBlockSource
    {
        private readonly SortedDictionary<ulong, BlockBundle> _pending = new SortedDictionary<ulong, BlockBundle>();
        private readonly object _gate = new object();
        private readonly ILogger _logger;
        private readonly string _sourceName;

        private SemaphoreSlim _arrived = new SemaphoreSlim(0);
        private ulong _highestSeen;
        private ulong _lastHandedOffNumber;
        private byte[] _lastHandedOffHash;
        private DivergenceSignal _lastChainBreak;
        private readonly Func<Task<ulong>> _committedHeight;

        public PushedBlockSource(
            string sourceName = "devp2p-push",
            ILogger<PushedBlockSource> logger = null,
            Func<Task<ulong>> committedHeight = null)
        {
            _sourceName = sourceName;
            _logger = (ILogger)logger ?? NullLogger.Instance;
            _committedHeight = committedHeight;
        }

        public DivergenceSignal LastChainBreak
        {
            get { lock (_gate) return _lastChainBreak; }
        }

        public string Name => _sourceName;

        public int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        public void OnNewBlock(NewBlockMessage message) => OnNewBlock(message, sourcePeerNodeId: null);

        public void OnNewBlock(NewBlockMessage message, string sourcePeerNodeId)
        {
            if (message?.Header == null) return;

            var number = (ulong)message.Header.BlockNumber;
            var headerHash = BlockHashCalculator.ForHeader(message.Header);
            var bundle = new BlockBundle(
                message.Header,
                message.Transactions ?? new List<Model.ISignedTransaction>(),
                message.Uncles ?? new List<Model.BlockHeader>(),
                message.Withdrawals,
                headerHash);

            lock (_gate)
            {
                if (IsCompetingWithAnAlreadyHandedOffBlock(number, headerHash))
                {
                    if (_lastChainBreak == null)
                    {
                        _lastChainBreak = new DivergenceSignal(
                            AtBlock: number,
                            PeerParentHash: message.Header.ParentHash,
                            OurParentHash: _lastHandedOffHash,
                            QuorumPeerCount: 1,
                            SourceName: _sourceName,
                            IncomingHeader: message.Header,
                            IncomingHash: headerHash,
                            SourcePeerNodeId: sourcePeerNodeId,
                            IncomingTransactions: new List<Model.ISignedTransaction>(bundle.Transactions));
                        _logger.LogWarning(
                            "Pushed block {Block} competes with an already-imported block; signalling divergence", number);
                    }
                }
                else
                {
                    _pending[number] = bundle;
                    if (number > _highestSeen) _highestSeen = number;
                }
            }

            _logger.LogDebug("Pushed block {Block} queued for import", number);
            _arrived.Release();
        }

        private bool IsCompetingWithAnAlreadyHandedOffBlock(ulong number, byte[] headerHash)
        {
            if (number > _lastHandedOffNumber) return false;
            if (number == _lastHandedOffNumber && ByteUtil.AreEqual(headerHash, _lastHandedOffHash)) return false;
            return true;
        }

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            lock (_gate) _lastChainBreak = null;
            var next = fromBlock;

            while (!ct.IsCancellationRequested)
            {
                if (LastChainBreak != null) yield break;

                var bundle = TakeContiguous(ref next);
                if (bundle != null)
                {
                    yield return bundle;
                    continue;
                }

                var committed = await CommittedHeightOrUnknown().ConfigureAwait(false);
                if (committed >= next)
                {
                    lock (_gate) _lastHandedOffNumber = Math.Max(_lastHandedOffNumber, committed.Value);
                    next = committed.Value + 1;
                    continue;
                }

                try { await _arrived.WaitAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
        }

        public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
        {
            lock (_gate)
            {
                return Task.FromResult(_pending.Count > 0
                    ? BlockSourceHealth.Healthy
                    : BlockSourceHealth.Degraded);
            }
        }

        public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
        {
            lock (_gate) _pending.Remove(blockNumber);
            _logger.LogWarning("Pushed block {Block} rejected on import: {Reason}", blockNumber, reason);
            return Task.CompletedTask;
        }

        private BlockBundle TakeContiguous(ref ulong next)
        {
            lock (_gate)
            {
                DropAlreadyPassed(next);

                if (!_pending.TryGetValue(next, out var bundle)) return null;

                _pending.Remove(next);
                _lastHandedOffNumber = next;
                _lastHandedOffHash = bundle.HeaderHash;
                next++;
                return bundle;
            }
        }

        private async Task<ulong?> CommittedHeightOrUnknown()
        {
            if (_committedHeight == null) return null;

            try
            {
                return await _committedHeight().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Committed height unavailable, holding at {Block}", _highestSeen);
                return null;
            }
        }

        private void DropAlreadyPassed(ulong next)
        {
            var stale = new List<ulong>();
            foreach (var number in _pending.Keys)
            {
                if (number >= next) break;
                stale.Add(number);
            }

            foreach (var number in stale) _pending.Remove(number);
        }
    }
}
