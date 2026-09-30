using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Sinks
{
    public sealed class TrieSnapSyncSink : ISnapSyncSink
    {
        private readonly object _stateLock = new();
        private readonly ITrieNodeStore _nodeStore;
        internal readonly ITrieNodeStore NodeStore;
        internal ISnapFlatStateWriter FlatWriter => _flatWriter;
        private readonly IStateStore _stateStore;
        private readonly ISnapFlatStateWriter _flatWriter;
        private readonly ILogger _logger;

        private readonly PatriciaTrie _stateTrie;
        private int _accountCount;
        private int _slotCount;
        private int _bytecodeCount;

        private const int CollapseIntervalAccounts = 50_000;
        private int _accountsSinceCollapse;

        public TrieSnapSyncSink(ITrieNodeStore stateTrieStore, IStateStore stateStore, ILogger logger = null)
            : this(stateTrieStore, stateStore, stateStore as ISnapFlatStateWriter, logger) { }

        public TrieSnapSyncSink(ITrieNodeStore stateTrieStore, IStateStore stateStore, ISnapFlatStateWriter flatWriter, ILogger logger = null)
        {
            _nodeStore = stateTrieStore ?? throw new ArgumentNullException(nameof(stateTrieStore));
            NodeStore = _nodeStore;
            _stateTrie = new PatriciaTrie(NodeStore);
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _flatWriter = flatWriter;
            _logger = logger ?? NullLogger.Instance;
        }

        public int AccountCount => _accountCount;
        public int SlotCount => _slotCount;
        public int BytecodeCount => _bytecodeCount;

        public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

        public async ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var canonical = SlimAccountEncoder.FromSlim(slimRlp);
            lock (_stateLock)
            {
                _stateTrie.Put(accountHash, canonical);
                _accountCount++;
                if (++_accountsSinceCollapse >= CollapseIntervalAccounts)
                {
                    _stateTrie.SaveDirtyNodesToStorageAndCollapse();
                    _accountsSinceCollapse = 0;
                }
            }
            if (_flatWriter != null)
            {
                var account = new AccountEncoder().Decode(canonical);
                await _flatWriter.SaveAccountByHashAsync(accountHash, account).ConfigureAwait(false);
            }
        }

        public ValueTask<IStorageScope> BeginAccountStorageAsync(
            byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
            => new ValueTask<IStorageScope>(new StorageScope(this, accountHash, expectedStorageRoot));

        public async ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
        {
            await _stateStore.SaveCodeAsync(codeHash, code).ConfigureAwait(false);
            Interlocked.Increment(ref _bytecodeCount);
        }

        public void FlushAccountTrieForCheckpoint()
        {
            lock (_stateLock)
            {
                _stateTrie.SaveDirtyNodesToStorage();
                FlushMemtablesIfAvailable("checkpoint");
            }
        }

        public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
        {
            lock (_stateLock)
            {
                _stateTrie.SaveDirtyNodesToStorage();
                FlushMemtablesIfAvailable("finalise");
                return new ValueTask<byte[]>(_stateTrie.Root.GetHash());
            }
        }

        private void FlushMemtablesIfAvailable(string stage)
        {
            try
            {
                _nodeStore.Flush();
            }
            catch (TransientFlushUnavailableException ex)
            {
                _logger.LogWarning(
                    "snap.phase2.flush_skipped stage={Stage} reason=write_stall detail={Detail} — account trie nodes remain WAL-durable; the next flush retries once writes resume",
                    stage, ex.Message);
            }
        }

        private sealed class StorageScope : IStorageScope
        {
            private readonly TrieSnapSyncSink _sink;
            private readonly byte[] _accountHash;
            private readonly byte[] _expectedRoot;
            private readonly PatriciaTrie _trie;
            private readonly object _slotLock = new();
            private readonly List<(byte[] SlotHash, byte[] RawValue)> _flatSlots = new();
            private bool _wroteSlots;

            public StorageScope(TrieSnapSyncSink sink, byte[] accountHash, byte[] expectedRoot)
            {
                _sink = sink;
                _accountHash = accountHash;
                _expectedRoot = expectedRoot;
                _trie = new PatriciaTrie(sink.NodeStore, accountHash);
            }

            public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var rawValue = _sink._flatWriter == null
                    ? null
                    : Nethereum.RLP.RLP.Decode(valueRlp).RLPData;
                lock (_slotLock)
                {
                    _trie.Put(slotHash, valueRlp);
                    if (rawValue != null)
                        _flatSlots.Add(((byte[])slotHash.Clone(), rawValue));
                    _wroteSlots = true;
                }
                Interlocked.Increment(ref _sink._slotCount);
                return default;
            }

            public async ValueTask EndAsync(CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                List<(byte[] SlotHash, byte[] RawValue)> flatSlots;
                lock (_slotLock)
                {
                    if (_wroteSlots && _expectedRoot != null && _expectedRoot.Length == 32
                        && !ByteUtil.AreEqual(_trie.Root.GetHash(), _expectedRoot))
                        throw new InvalidOperationException(
                            "Snap storage-root mismatch: the completed storage trie does not match the account's expected storage root.");
                    _trie.SaveDirtyNodesToStorage();
                    flatSlots = new List<(byte[] SlotHash, byte[] RawValue)>(_flatSlots);
                    _flatSlots.Clear();
                }

                if (_sink._flatWriter != null)
                {
                    foreach (var slot in flatSlots)
                    {
                        ct.ThrowIfCancellationRequested();
                        await _sink._flatWriter.SaveStorageByHashAsync(
                            _accountHash, slot.SlotHash, slot.RawValue).ConfigureAwait(false);
                    }
                }
            }

            public ValueTask AbortAsync(CancellationToken ct) => default;
        }
    }
}
