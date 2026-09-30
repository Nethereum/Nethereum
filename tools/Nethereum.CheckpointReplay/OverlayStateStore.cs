using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CheckpointReplay
{
    /// <summary>
    /// Writable in-memory <see cref="IStateStore"/> overlay over the read-only checkpoint flat state. Mirrors
    /// <c>InMemoryStateStore</c>'s dirty-tracking + snapshot semantics, but on a read/write miss it lazily
    /// hydrates the value from the checkpoint (base) WITHOUT dirty-tracking, so pre-existing accounts/slots are
    /// served exactly as the follower's RocksDbStateStore would serve them — while every mutation stays in
    /// memory and the checkpoint is never touched. Writes hydrate-then-mutate so snapshot undo logs always
    /// capture the true prior value and revert re-exposes base values correctly.
    /// </summary>
    public sealed class OverlayStateStore : IStateStore
    {
        private readonly CheckpointReader _base;
        private readonly object _lock = new object();

        // Primary maps keyed by keccak(addr) hex (like InMemoryStateStore).
        private readonly Dictionary<string, Account> _accounts = new();
        private readonly Dictionary<string, Dictionary<BigInteger, byte[]>> _storage = new();
        private readonly Dictionary<string, byte[]> _code = new();
        private readonly Dictionary<string, string> _addressByAccountHash = new();

        // Base-hydration bookkeeping (NOT reverted by snapshots except where noted).
        private readonly HashSet<string> _residentAccounts = new();          // norm -> base settled
        private readonly Dictionary<string, HashSet<BigInteger>> _residentSlots = new();
        private readonly HashSet<string> _residentCode = new();
        private readonly HashSet<string> _baseStorageInvalidated = new();     // norm -> never re-hydrate storage from base

        // Per-block dirty tracking (original-address keyed, like InMemoryStateStore).
        private readonly HashSet<string> _dirtyAccounts = new();
        private readonly Dictionary<string, HashSet<BigInteger>> _dirtyStorageSlots = new();
        private readonly HashSet<string> _storageClearedThisBlock = new();

        private int _nextSnapshotId;
        private Snap _active;

        public OverlayStateStore(CheckpointReader baseReader)
        {
            _base = baseReader ?? throw new ArgumentNullException(nameof(baseReader));
        }

        private static string Norm(string address) => StateKeys.AccountKeyHex(address);
        private static string Orig(string address) => AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        private static bool IsZero(byte[] v) { if (v == null) return true; for (int i = 0; i < v.Length; i++) if (v[i] != 0) return false; return true; }

        private static Account Clone(Account a) => a == null ? null : new Account
        {
            Nonce = a.Nonce,
            Balance = a.Balance,
            StateRoot = a.StateRoot?.ToArray(),
            CodeHash = a.CodeHash?.ToArray()
        };

        // ---- base hydration (non-dirtying) ----

        private void EnsureAccountResident(string norm, string original)
        {
            if (_residentAccounts.Contains(norm)) return;
            var baseAcc = _base.GetAccount(original);
            if (baseAcc != null)
            {
                _accounts[norm] = baseAcc;
                _addressByAccountHash[norm] = original;
            }
            _residentAccounts.Add(norm);
        }

        private void EnsureSlotResident(string norm, string original, BigInteger slot)
        {
            if (!_residentSlots.TryGetValue(norm, out var set)) { set = new HashSet<BigInteger>(); _residentSlots[norm] = set; }
            if (set.Contains(slot)) return;
            set.Add(slot);
            if (_baseStorageInvalidated.Contains(norm)) return;
            var baseVal = _base.GetStorage(original, slot);
            if (baseVal != null && !IsZero(baseVal))
            {
                if (!_storage.TryGetValue(norm, out var acc)) { acc = new Dictionary<BigInteger, byte[]>(); _storage[norm] = acc; }
                acc[slot] = baseVal;
            }
        }

        // ---- IStateStore reads ----

        public Task<Account> GetAccountAsync(string address)
        {
            lock (_lock)
            {
                var norm = Norm(address);
                EnsureAccountResident(norm, Orig(address));
                _accounts.TryGetValue(norm, out var a);
                return Task.FromResult(Clone(a));
            }
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            lock (_lock)
            {
                var norm = Norm(address);
                EnsureAccountResident(norm, Orig(address));
                return Task.FromResult(_accounts.ContainsKey(norm));
            }
        }

        public Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            lock (_lock)
            {
                var norm = Norm(address);
                EnsureSlotResident(norm, Orig(address), slot);
                if (_storage.TryGetValue(norm, out var acc) && acc.TryGetValue(slot, out var v))
                    return Task.FromResult(v?.ToArray());
                return Task.FromResult<byte[]>(null);
            }
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            if (codeHash == null) return Task.FromResult<byte[]>(null);
            lock (_lock)
            {
                var hex = codeHash.ToHex();
                if (_code.TryGetValue(hex, out var c)) return Task.FromResult(c?.ToArray());
                if (!_residentCode.Contains(hex))
                {
                    _residentCode.Add(hex);
                    var baseCode = _base.GetCode(codeHash);
                    if (baseCode != null) { _code[hex] = baseCode; return Task.FromResult(baseCode.ToArray()); }
                }
                return Task.FromResult<byte[]>(null);
            }
        }

        // GetAllStorage: only the base-visible + overlay slots we have materialised. The incremental follow
        // path never calls this (it warm-starts storage tries from the persisted root and applies dirty slots),
        // so a partial view is acceptable and never reached for correctness in this experiment.
        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
        {
            lock (_lock)
            {
                var norm = Norm(address);
                var result = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
                if (_storage.TryGetValue(norm, out var acc))
                    foreach (var kv in acc) result[StateKeys.StorageSlotKey(kv.Key)] = kv.Value;
                return Task.FromResult(result);
            }
        }

        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
        {
            lock (_lock)
            {
                var r = new Dictionary<string, Account>();
                foreach (var kv in _accounts)
                {
                    var addr = _addressByAccountHash.TryGetValue(kv.Key, out var o) ? o : kv.Key;
                    r[addr] = Clone(kv.Value);
                }
                return Task.FromResult(r);
            }
        }

#pragma warning disable CS1998
        public async IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
#pragma warning restore CS1998
        {
            List<KeyValuePair<string, Account>> snapshot;
            lock (_lock)
            {
                snapshot = _accounts.Select(kv => new KeyValuePair<string, Account>(
                    _addressByAccountHash.TryGetValue(kv.Key, out var o) ? o : kv.Key, Clone(kv.Value))).ToList();
            }
            foreach (var kv in snapshot) yield return kv;
        }

        // ---- IStateStore writes (hydrate-then-mutate) ----

        public Task SaveAccountAsync(string address, Account account)
        {
            lock (_lock)
            {
                var norm = Norm(address); var orig = Orig(address);
                EnsureAccountResident(norm, orig);
                _active?.SaveAccountUndo(norm, _accounts.TryGetValue(norm, out var ex) ? Clone(ex) : null);
                _accounts[norm] = account;
                _addressByAccountHash[norm] = orig;
                _dirtyAccounts.Add(orig);
                return Task.CompletedTask;
            }
        }

        public Task DeleteAccountAsync(string address)
        {
            lock (_lock)
            {
                var norm = Norm(address); var orig = Orig(address);
                EnsureAccountResident(norm, orig);
                _active?.SaveAccountUndo(norm, _accounts.TryGetValue(norm, out var ex) ? Clone(ex) : null);
                _active?.SaveStorageClearUndo(norm, _storage.TryGetValue(norm, out var st) ? st : null, _baseStorageInvalidated.Contains(norm));
                _accounts.Remove(norm);
                _storage.Remove(norm);
                _baseStorageInvalidated.Add(norm);
                _dirtyAccounts.Add(orig);
                return Task.CompletedTask;
            }
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
        {
            lock (_lock)
            {
                var norm = Norm(address); var orig = Orig(address);
                EnsureSlotResident(norm, orig, slot);
                byte[] prior = null;
                if (_storage.TryGetValue(norm, out var acc)) acc.TryGetValue(slot, out prior);
                _active?.SaveStorageUndo(norm, slot, prior?.ToArray());
                if (value == null || IsZero(value))
                {
                    if (acc != null) acc.Remove(slot);
                }
                else
                {
                    if (acc == null) { acc = new Dictionary<BigInteger, byte[]>(); _storage[norm] = acc; }
                    acc[slot] = value;
                }
                _dirtyAccounts.Add(orig);
                if (!_dirtyStorageSlots.TryGetValue(orig, out var ds)) { ds = new HashSet<BigInteger>(); _dirtyStorageSlots[orig] = ds; }
                ds.Add(slot);
                return Task.CompletedTask;
            }
        }

        public Task ClearStorageAsync(string address)
        {
            lock (_lock)
            {
                var norm = Norm(address); var orig = Orig(address);
                _active?.SaveStorageClearUndo(norm, _storage.TryGetValue(norm, out var st) ? st : null, _baseStorageInvalidated.Contains(norm));
                _storage.Remove(norm);
                _baseStorageInvalidated.Add(norm);
                _dirtyAccounts.Add(orig);
                _storageClearedThisBlock.Add(orig);
                return Task.CompletedTask;
            }
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
        {
            if (codeHash == null) return Task.CompletedTask;
            lock (_lock)
            {
                var hex = codeHash.ToHex();
                _code.TryGetValue(hex, out var ex);
                _active?.SaveCodeUndo(hex, ex?.ToArray());
                _code[hex] = code;
                _residentCode.Add(hex);
                return Task.CompletedTask;
            }
        }

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => throw new NotSupportedException("Not needed for forward replay.");

        // ---- snapshots ----

        public Task<IStateSnapshot> CreateSnapshotAsync()
        {
            lock (_lock)
            {
                var s = new Snap(_nextSnapshotId++,
                    new HashSet<string>(_dirtyAccounts),
                    _dirtyStorageSlots.ToDictionary(k => k.Key, v => new HashSet<BigInteger>(v.Value)),
                    new HashSet<string>(_storageClearedThisBlock));
                _active = s;
                return Task.FromResult<IStateSnapshot>(s);
            }
        }

        public Task CommitSnapshotAsync(IStateSnapshot snapshot)
        {
            lock (_lock) { if (snapshot is Snap s && _active == s) _active = null; }
            return Task.CompletedTask;
        }

        public Task RevertSnapshotAsync(IStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (snapshot is not Snap s) return Task.CompletedTask;
                foreach (var kv in s.AccountUndo)
                {
                    if (kv.Value != null) _accounts[kv.Key] = kv.Value; else _accounts.Remove(kv.Key);
                }
                foreach (var clr in s.StorageClearUndo)
                {
                    // restore the storage dict captured at clear/delete time
                    if (clr.Value.Dict != null) _storage[clr.Key] = clr.Value.Dict.ToDictionary(k => k.Key, v => v.Value);
                    else _storage.Remove(clr.Key);
                    if (!clr.Value.WasInvalidated) _baseStorageInvalidated.Remove(clr.Key);
                }
                foreach (var addr in s.StorageUndo)
                {
                    if (!_storage.TryGetValue(addr.Key, out var acc)) { acc = new Dictionary<BigInteger, byte[]>(); _storage[addr.Key] = acc; }
                    foreach (var slot in addr.Value)
                    {
                        if (slot.Value != null) acc[slot.Key] = slot.Value; else acc.Remove(slot.Key);
                    }
                }
                foreach (var kv in s.CodeUndo)
                {
                    if (kv.Value != null) _code[kv.Key] = kv.Value; else _code.Remove(kv.Key);
                }
                _dirtyAccounts.Clear(); foreach (var a in s.DirtyAccounts) _dirtyAccounts.Add(a);
                _dirtyStorageSlots.Clear(); foreach (var kv in s.DirtyStorageSlots) _dirtyStorageSlots[kv.Key] = new HashSet<BigInteger>(kv.Value);
                _storageClearedThisBlock.Clear(); foreach (var a in s.StorageCleared) _storageClearedThisBlock.Add(a);
                _active = null;
                return Task.CompletedTask;
            }
        }

        // ---- dirty tracking surface (for the calculator) ----

        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
        { lock (_lock) return Task.FromResult<IReadOnlyCollection<string>>(_dirtyAccounts.ToList()); }

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
        {
            lock (_lock)
            {
                var orig = Orig(address);
                if (!_dirtyStorageSlots.TryGetValue(orig, out var ds))
                    return Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());
                return Task.FromResult<IReadOnlyCollection<BigInteger>>(ds.ToList());
            }
        }

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
        { lock (_lock) return Task.FromResult<IReadOnlyCollection<string>>(_storageClearedThisBlock.ToList()); }

        public Task ClearDirtyTrackingAsync()
        {
            lock (_lock)
            {
                _dirtyAccounts.Clear();
                _dirtyStorageSlots.Clear();
                _storageClearedThisBlock.Clear();
                return Task.CompletedTask;
            }
        }

        // Snapshot record.
        private sealed class Snap : IStateSnapshot
        {
            public int SnapshotId { get; }
            public HashSet<string> DirtyAccounts { get; }
            public Dictionary<string, HashSet<BigInteger>> DirtyStorageSlots { get; }
            public HashSet<string> StorageCleared { get; }
            public Dictionary<string, Account> AccountUndo { get; } = new();
            public Dictionary<string, Dictionary<BigInteger, byte[]>> StorageUndo { get; } = new();
            public Dictionary<string, byte[]> CodeUndo { get; } = new();
            public Dictionary<string, (Dictionary<BigInteger, byte[]> Dict, bool WasInvalidated)> StorageClearUndo { get; } = new();

            public Snap(int id, HashSet<string> da, Dictionary<string, HashSet<BigInteger>> ds, HashSet<string> sc)
            { SnapshotId = id; DirtyAccounts = da; DirtyStorageSlots = ds; StorageCleared = sc; }

            public void SaveAccountUndo(string norm, Account prior) { if (!AccountUndo.ContainsKey(norm)) AccountUndo[norm] = prior; }
            public void SaveStorageUndo(string norm, BigInteger slot, byte[] prior)
            {
                if (!StorageUndo.TryGetValue(norm, out var d)) { d = new Dictionary<BigInteger, byte[]>(); StorageUndo[norm] = d; }
                if (!d.ContainsKey(slot)) d[slot] = prior;
            }
            public void SaveCodeUndo(string hex, byte[] prior) { if (!CodeUndo.ContainsKey(hex)) CodeUndo[hex] = prior; }
            public void SaveStorageClearUndo(string norm, Dictionary<BigInteger, byte[]> dict, bool wasInvalidated)
            {
                if (StorageClearUndo.ContainsKey(norm)) return;
                StorageClearUndo[norm] = (dict?.ToDictionary(k => k.Key, v => v.Value?.ToArray()), wasInvalidated);
            }

            public void SetAccount(string a, Account acc) { }
            public void SetStorage(string a, BigInteger s, byte[] v) { }
            public void SetCode(byte[] h, byte[] c) { }
            public void DeleteAccount(string a) { }
            public void ClearStorage(string a) { }
            public void Dispose() { }
        }
    }
}
