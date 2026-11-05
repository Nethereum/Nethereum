using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.Prefetch
{
    public sealed class PrefetchSession
    {
        public const int TriePathQueueCapacity = 4096;

        private readonly StatePrefetcher _host;
        private readonly TransactionPrefetch _prefetch;
        private readonly PrefetchBlock _block;
        private readonly ISignedTransaction[] _transactions;
        private readonly RecoveredSenders _senders;
        private readonly TaskCompletionSource<bool> _completed =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;
        private int _claimed;
        private int _prefetched;
        private int _abandoned;
        private int _failed;
        private volatile bool _stopped;
        private volatile TriePathFeed? _triePaths;

        internal PrefetchSession(
            StatePrefetcher host, TransactionPrefetch prefetch, PrefetchBlock block, ISignedTransaction[] transactions, RecoveredSenders senders)
        {
            _host = host;
            _prefetch = prefetch;
            _block = block;
            _transactions = transactions;
            _senders = senders;
            if (transactions.Length == 0) _completed.SetResult(true);
        }

        public Task Completed => _completed.Task;

        public PrefetchOutcome Outcome => new PrefetchOutcome(
            Volatile.Read(ref _prefetched), NotStarted + Volatile.Read(ref _abandoned), Volatile.Read(ref _failed));

        public TriePathOutcome TriePathOutcome => _triePaths?.Outcome ?? default;

        private int NotStarted => _transactions.Length - Math.Min(_transactions.Length, Volatile.Read(ref _claimed));

        internal bool HasTransactionWork =>
            !_stopped && Volatile.Read(ref _claimed) < _transactions.Length;

        internal bool HasTriePathWork => _triePaths?.HasWork ?? false;

        public bool StartTriePaths(IStateStore state, byte[]? parentRoot, IIncrementalStateRootCalculator? trieHost)
        {
            if (_stopped || _triePaths != null) return false;
            var feed = TriePathFeed.Attach(state, _host.State, trieHost, parentRoot, TriePathQueueCapacity, _host.Wake);
            if (feed == null) return false;
            _triePaths = feed;
            _host.Wake();
            return true;
        }

        public void EndTriePathsBeforeRoot()
        {
            _triePaths?.StopAndAwaitWalks();
        }

        public PrefetchOutcome Stop()
        {
            _stopped = true;
            _triePaths?.Stop();
            CompleteIfFinished();
            _host.Release(this);
            return Outcome;
        }

        internal RecoveredSenders Senders => _senders;

        public string? TryGetSender(int index, ISignedTransaction transaction) =>
            _senders.TryGetSender(index, transaction);

        internal bool TryRunTriePathUnit() => _triePaths?.TryRunUnit() ?? false;

        internal bool TryRunTransactionUnit()
        {
            Interlocked.Increment(ref _inFlight);
            try
            {
                if (_stopped) return false;
                var index = Interlocked.Increment(ref _claimed) - 1;
                if (index >= _transactions.Length) return false;
                PrefetchTransaction(index);
                return true;
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
                CompleteIfFinished();
            }
        }

        private void CompleteIfFinished()
        {
            if (Volatile.Read(ref _inFlight) != 0) return;
            if (_stopped || Volatile.Read(ref _claimed) >= _transactions.Length) _completed.TrySetResult(true);
        }

        private void PrefetchTransaction(int index)
        {
            try
            {
                var transaction = _transactions[index];
                var step = _prefetch.WarmAsync(_block, transaction, index, () => _stopped, _senders.WaitForSender(index, transaction))
                    .GetAwaiter().GetResult();
                Count(step);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _failed);
            }
        }

        private void Count(PrefetchStep step)
        {
            switch (step)
            {
                case PrefetchStep.Completed: Interlocked.Increment(ref _prefetched); break;
                case PrefetchStep.Interrupted: Interlocked.Increment(ref _abandoned); break;
                default: Interlocked.Increment(ref _failed); break;
            }
        }
    }
}
