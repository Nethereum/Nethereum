using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Prefetch
{
    internal sealed class TriePathFeed : IDirtyKeyObserver
    {
        private readonly BlockTrieSet _tries;
        private readonly IStateStore _accounts;
        private readonly IDirtyKeySource _source;
        private readonly int _capacity;
        private readonly Action _wake;
        private readonly OwnerQueue _accountOwner = OwnerQueue.ForAccounts();
        private readonly ConcurrentDictionary<EvmAddress, OwnerQueue> _storageOwners =
            new ConcurrentDictionary<EvmAddress, OwnerQueue>();
        private readonly ConcurrentQueue<OwnerQueue> _ready = new ConcurrentQueue<OwnerQueue>();
        private readonly ManualResetEventSlim _walksEnded = new ManualResetEventSlim(false);
        private int _queued;
        private int _signalled;
        private int _stopped;
        private int _walking;
        private int _enqueued;
        private int _warmed;
        private int _dropped;
        private int _failed;
        private int _skipped;
        private int _ended;

        private TriePathFeed(BlockTrieSet tries, IStateStore accounts, IDirtyKeySource source, int capacity, Action wake)
        {
            _tries = tries;
            _accounts = accounts;
            _source = source;
            _capacity = capacity;
            _wake = wake;
        }

        public static TriePathFeed? Attach(
            IStateStore stateStore, IStateStore accounts, IIncrementalStateRootCalculator? host, byte[]? parentRoot, int capacity, Action wake)
        {
            if (!(stateStore is IDirtyKeySource source) || host == null || parentRoot == null) return null;
            var tries = host.BeginBlockTries(parentRoot);
            if (tries == null) return null;
            var feed = new TriePathFeed(tries, accounts, source, capacity, wake);
            return source.TryAttachDirtyKeyObserver(feed) ? feed : null;
        }

        public bool HasWork => !_ready.IsEmpty && IsActive;

        public bool IsActive => Volatile.Read(ref _stopped) == 0;

        public TriePathOutcome Outcome => new TriePathOutcome(
            Volatile.Read(ref _enqueued), Volatile.Read(ref _warmed), Volatile.Read(ref _dropped), Volatile.Read(ref _failed), Volatile.Read(ref _skipped));

        public void OnAccountDirty(EvmAddress address) => Offer(_accountOwner, new DirtyKey(address, false, BigInteger.Zero));

        public void OnStorageSlotDirty(EvmAddress address, BigInteger slot)
            => Offer(_storageOwners.GetOrAdd(address, a => OwnerQueue.ForStorage(a)), new DirtyKey(address, true, slot));

        private void Offer(OwnerQueue owner, DirtyKey key)
        {
            if (!IsActive) return;
            if (Interlocked.Increment(ref _queued) > _capacity)
            {
                Interlocked.Decrement(ref _queued);
                Interlocked.Increment(ref _dropped);
                return;
            }
            owner.Keys.Enqueue(key);
            Interlocked.Increment(ref _enqueued);
            if (owner.TryClaim())
            {
                _ready.Enqueue(owner);
                if (Interlocked.Exchange(ref _signalled, 1) == 0) _wake();
            }
        }

        private bool TryTakeOwner(out OwnerQueue owner)
        {
            if (_ready.TryDequeue(out owner)) return true;
            Interlocked.Exchange(ref _signalled, 0);
            return _ready.TryDequeue(out owner);
        }

        public bool TryRunUnit()
        {
            if (!IsActive || !TryTakeOwner(out var owner)) return false;
            Interlocked.Increment(ref _walking);
            try
            {
                if (IsActive) DrainOwner(owner);
            }
            finally
            {
                if (Interlocked.Decrement(ref _walking) == 0 && !IsActive) _walksEnded.Set();
            }
            return true;
        }

        private void DrainOwner(OwnerQueue owner)
        {
            do
            {
                while (IsActive && owner.Keys.TryDequeue(out var key))
                {
                    Interlocked.Decrement(ref _queued);
                    WalkAndCount(owner, key);
                }
                owner.Release();
            }
            while (IsActive && !owner.Keys.IsEmpty && owner.TryClaim());
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            _source.DetachDirtyKeyObserver(this);
            while (_ready.TryDequeue(out _)) { }
            DropUnstartedKeys(_accountOwner);
            foreach (var owner in _storageOwners.Values) DropUnstartedKeys(owner);
        }

        public void StopAndAwaitWalks()
        {
            Stop();
            if (Interlocked.Exchange(ref _ended, 1) != 0) return;
            if (Volatile.Read(ref _walking) != 0) _walksEnded.Wait();
            _walksEnded.Dispose();
        }

        private void DropUnstartedKeys(OwnerQueue owner)
        {
            while (owner.Keys.TryDequeue(out _)) Interlocked.Decrement(ref _queued);
        }

        private void WalkAndCount(OwnerQueue owner, DirtyKey key)
        {
            try
            {
                if (Walk(owner, key)) Interlocked.Increment(ref _warmed);
                else Interlocked.Increment(ref _skipped);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _failed);
            }
        }

        private bool Walk(OwnerQueue owner, DirtyKey key)
        {
            if (!owner.Opened) Open(owner);
            if (owner.Trie == null) return false;
            owner.Trie.Get(key.IsSlot ? StateKeys.StorageSlotKey(key.Slot) : StateKeys.AccountKey(key.Address, _accounts));
            return true;
        }

        private void Open(OwnerQueue owner)
        {
            if (owner.IsAccounts)
            {
                owner.Trie = _tries.AccountTrie;
            }
            else
            {
                var hashedAddress = StateKeys.AccountKey(owner.Address, _accounts);
                var root = _accounts.GetAccountAsync(owner.Address).GetAwaiter().GetResult()?.StateRoot;
                owner.Trie = HasStorage(root) ? _tries.StorageTrie(hashedAddress, root!) : null;
            }
            owner.Opened = true;
        }

        private static bool HasStorage(byte[]? root)
            => root != null && root.Length == 32 && !ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH);

        private sealed class OwnerQueue
        {
            private int _claimed;

            private OwnerQueue(bool isAccounts, EvmAddress address)
            {
                IsAccounts = isAccounts;
                Address = address;
            }

            public static OwnerQueue ForAccounts() => new OwnerQueue(true, EvmAddress.Zero);

            public static OwnerQueue ForStorage(EvmAddress address) => new OwnerQueue(false, address);

            public bool IsAccounts { get; }
            public EvmAddress Address { get; }
            public ConcurrentQueue<DirtyKey> Keys { get; } = new ConcurrentQueue<DirtyKey>();
            public PatriciaTrie? Trie { get; set; }
            public bool Opened { get; set; }

            public bool TryClaim() => Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;

            public void Release() => Interlocked.Exchange(ref _claimed, 0);
        }

        private readonly struct DirtyKey
        {
            public DirtyKey(EvmAddress address, bool isSlot, BigInteger slot)
            {
                Address = address;
                IsSlot = isSlot;
                Slot = slot;
            }

            public EvmAddress Address { get; }
            public bool IsSlot { get; }
            public BigInteger Slot { get; }
        }
    }
}
