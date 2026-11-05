using Nethereum.Documentation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain.Prefetch
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "StatePrefetcher - warm a block state on worker threads before it executes")]
    public sealed class StatePrefetcher : IDisposable
    {
        public const string WorkerThreadNamePrefix = "state-prefetch-";

        public static readonly TimeSpan DefaultShutdownBound = TimeSpan.FromSeconds(5);

        private readonly object _gate = new object();
        private readonly TransactionPrefetch _prefetch;
        private readonly SenderRecoveryStep _senderRecovery;
        private readonly IChainActivations _activations;
        private readonly Func<HardforkName, ChainConfig> _chainConfigFactory;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly Func<BlockHeader, string>? _authorResolver;
        private readonly int _workers;
        private readonly TimeSpan _shutdownBound;
        private readonly DedicatedThreads _threads;
        private readonly ConcurrentDictionary<PrefetchSession, byte> _openSessions = new ConcurrentDictionary<PrefetchSession, byte>();
        private int _disposed;
        private int _wakeups;

        public StatePrefetcher(
            IStateStore state,
            IBlockStore blocks,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            Func<BlockHeader, string>? authorResolver,
            int workers,
            TimeSpan? shutdownBound = null)
            : this(state, blocks, activations, chainConfigFactory, hardforkConfigFactory, authorResolver, workers, shutdownBound, null)
        {
        }

        public StatePrefetcher(
            IStateStore state,
            IBlockStore blocks,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            Func<BlockHeader, string>? authorResolver,
            int workers,
            TimeSpan? shutdownBound,
            ITransactionVerificationAndRecovery? verifier)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            if (workers < 1) throw new ArgumentOutOfRangeException(nameof(workers), "A prefetcher needs at least one worker.");
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _chainConfigFactory = chainConfigFactory ?? throw new ArgumentNullException(nameof(chainConfigFactory));
            _hardforkConfigFactory = hardforkConfigFactory ?? throw new ArgumentNullException(nameof(hardforkConfigFactory));
            _authorResolver = authorResolver;
            _workers = workers;
            _shutdownBound = shutdownBound ?? DefaultShutdownBound;
            State = state;
            _senderRecovery = new SenderRecoveryStep(verifier);
            _prefetch = new TransactionPrefetch(state, blocks, _senderRecovery.Verifier);
            _threads = new DedicatedThreads(WorkerThreadNamePrefix, workers, RunWorkerThread);
        }

        public int Workers => _workers;

        internal IStateStore State { get; }

        public int LiveWorkerThreads => _threads.Alive;

        public int Wakeups => Volatile.Read(ref _wakeups);

        public PrefetchSession? Begin(BlockHeader header, IList<ISignedTransaction> transactions)
        {
            var snapshot = new ISignedTransaction[transactions.Count];
            transactions.CopyTo(snapshot, 0);
            if (Volatile.Read(ref _disposed) == 1) return null;
            try
            {
                var session = new PrefetchSession(this, _prefetch, PlanBlock(header), snapshot, _senderRecovery.Begin(snapshot));
                _openSessions[session] = 0;
                Wake();
                return session;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private PrefetchBlock PlanBlock(BlockHeader header)
        {
            var number = (long)header.BlockNumber;
            var timestamp = (ulong)header.Timestamp;
            var fork = _activations.ResolveAt(number, timestamp);
            var hardforkConfig = _hardforkConfigFactory(fork).AtBlock(_activations, number, timestamp);
            return new PrefetchBlock(header, fork, _chainConfigFactory(fork), hardforkConfig, _authorResolver);
        }

        internal void Wake()
        {
            Interlocked.Increment(ref _wakeups);
            lock (_gate) Monitor.PulseAll(_gate);
        }

        internal void Release(PrefetchSession session) => _openSessions.TryRemove(session, out byte removed);

        private void RunWorkerThread()
        {
            using var unmetered = StateReadMeter.SuppressReads();
            while (Volatile.Read(ref _disposed) == 0)
            {
                if (!RunOneUnit()) WaitForWork();
            }
        }

        private bool RunOneUnit()
        {
            foreach (var session in _openSessions.Keys)
                if (session.TryRunTriePathUnit()) return true;
            foreach (var session in _openSessions.Keys)
                if (session.TryRunTransactionUnit()) return true;
            return false;
        }

        private void WaitForWork()
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _disposed) == 1 || HasWork()) return;
                Monitor.Wait(_gate);
            }
        }

        private bool HasWork()
        {
            foreach (var session in _openSessions.Keys)
                if (session.HasTriePathWork || session.HasTransactionWork) return true;
            return false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            foreach (var open in _openSessions.Keys) open.Stop();
            Wake();
            _threads.JoinWithin(_shutdownBound);
        }
    }
}
