using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Drives the shared EIP-7928 <see cref="BlockAccessListBuilder"/> from
    /// CoreChain's <see cref="IStateStore"/>-backed execution loop. Same
    /// seam and model as <see cref="Nethereum.EVM.Execution.BlockAccessListCollector"/>
    /// (which drives the identical builder from the witness-replay engine's
    /// in-memory reader) — only the source of "committed" values differs,
    /// because this engine persists into a durable <see cref="IStateStore"/>
    /// rather than an in-memory snapshot.
    ///
    /// <para><b>Ordering contract.</b> For every unit of work (a
    /// transaction or a system call) the caller MUST call
    /// <see cref="PrepareUnitOfWorkAsync"/> BEFORE the unit's changes are
    /// persisted to the store, then <see cref="RecordUnitOfWorkAsync"/>
    /// AFTER. Reversed order reads the "before" basis from already-mutated
    /// state and silently drops every change the unit made. A unit that
    /// does not commit (nonce mismatch, validation error, reverted host
    /// call) must call <see cref="DiscardUnit"/> instead of
    /// <see cref="RecordUnitOfWorkAsync"/>, so its reads never reach the
    /// builder.</para>
    ///
    /// <para><b>Fault contract.</b> This class only ever reads from
    /// <see cref="IStateStore"/> and mutates its own in-memory bookkeeping —
    /// it never decides transaction success/failure, and a fault here says
    /// nothing about whether execution succeeded. Every call site that
    /// drives this class (the per-transaction loop, EIP-7685 request system
    /// calls, EIP-4788/2935 pre-transaction system calls, withdrawal
    /// crediting) routes every Prepare*/Record* call through
    /// <c>TransactionProcessor.RunBalRecordingAsync</c>, which classifies
    /// any fault raised here as an <see cref="EvmHostException"/> before it
    /// can reach a caller's own exception handling. This matters most for
    /// <c>TransactionProcessor.ExecuteTransactionAsync</c>'s
    /// revert-and-continue path: its generic catch converts an
    /// unclassified exception into a forfeit-all-gas reverted receipt, and
    /// without the wrapper a fault here would produce a wrong-but-plausible
    /// receipt for a transaction that actually succeeded. The other call
    /// sites have no such absorbing catch — an unwrapped fault there would
    /// already propagate and halt the block — but are wrapped anyway so
    /// the halt is uniformly phase-labelled and classified rather than
    /// classified only where a transaction happened to be involved. A new
    /// call site that drives this class outside that helper reopens the
    /// gap; there is no enforcement of this beyond "every current call site
    /// goes through it".</para>
    ///
    /// <para><b>Atomicity.</b> <see cref="RecordUnitOfWorkAsync"/> computes
    /// every change the unit produced into local staging before touching
    /// <see cref="BlockAccessListBuilder"/> at all, then applies every
    /// computed change in a second, non-failing pass. A fault partway
    /// through computation (a state-store read failure, a malformed nonce)
    /// therefore leaves the builder exactly as it was before the call —
    /// there is nothing for a caller to undo, and no earlier account in the
    /// same unit can end up recorded while a later one in the same unit
    /// faults.</para>
    /// </summary>
    public sealed class BlockAccessListRecorder : IStateAccessRecorder
    {
        private readonly BlockAccessListBuilder _builder = new BlockAccessListBuilder();
        private readonly IStateStore _stateStore;

        private readonly List<string> _pendingAccountReads = new List<string>();
        private readonly List<(string Address, EvmUInt256 Slot)> _pendingStorageReads = new List<(string, EvmUInt256)>();

        private sealed class AccountBasis
        {
            public bool Loaded;
            public EvmUInt256 Balance;
            public ulong Nonce;
            public byte[]? Code;
            public readonly Dictionary<EvmUInt256, EvmUInt256> Storage = new Dictionary<EvmUInt256, EvmUInt256>();
        }

        private readonly Dictionary<string, AccountBasis> _basis =
            new Dictionary<string, AccountBasis>(StringComparer.Ordinal);

        public BlockAccessListRecorder(IStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void BeginUnit(ulong blockAccessIndex)
        {
            _builder.BlockAccessIndex = blockAccessIndex;
            _pendingAccountReads.Clear();
            _pendingStorageReads.Clear();
        }

        public void RecordAccountRead(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            _pendingAccountReads.Add(address);
        }

        public void RecordStorageRead(string address, EvmUInt256 key)
        {
            if (string.IsNullOrEmpty(address)) return;
            _pendingStorageReads.Add((address, key));
        }

        public void DiscardUnit()
        {
            _pendingAccountReads.Clear();
            _pendingStorageReads.Clear();
        }

        public void TouchAccount(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            _builder.EnsureAccount(address);
        }

        public async Task PrepareUnitOfWorkAsync(ExecutionStateService executionState)
        {
            if (executionState?.AccountsState == null) return;
            foreach (var pair in executionState.AccountsState)
            {
                var address = pair.Key.ToHexLower();
                await PrepareAccountAsync(address).ConfigureAwait(false);
                if (pair.Value.Storage != null)
                {
                    foreach (var slot in pair.Value.Storage.Keys)
                        await PrepareStorageAsync(address, slot).ConfigureAwait(false);
                }
            }
        }

        public async Task RecordUnitOfWorkAsync(ExecutionStateService executionState)
        {
            if (executionState?.AccountsState == null)
            {
                FlushPendingReads();
                return;
            }

            var balanceChanges = new List<(string Address, EvmUInt256 NewBalance)>();
            var nonceChanges = new List<(string Address, ulong NewNonce)>();
            var codeChanges = new List<(string Address, byte[] NewCode)>();
            var storageWrites = new List<(string Address, EvmUInt256 Slot, EvmUInt256 NewValue)>();

            foreach (var pair in executionState.AccountsState)
            {
                var address = pair.Key.ToHexLower();
                await ComputeAccountDeltaAsync(address, balanceChanges, nonceChanges, codeChanges).ConfigureAwait(false);
                if (pair.Value.Storage != null)
                {
                    foreach (var slot in pair.Value.Storage.Keys)
                        await ComputeStorageDeltaAsync(address, slot, storageWrites).ConfigureAwait(false);
                }
            }

            FlushPendingReads();
            foreach (var c in balanceChanges) ApplyBalanceChange(c.Address, c.NewBalance);
            foreach (var c in nonceChanges) ApplyNonceChange(c.Address, c.NewNonce);
            foreach (var c in codeChanges) ApplyCodeChange(c.Address, c.NewCode);
            foreach (var c in storageWrites) ApplyStorageWrite(c.Address, c.Slot, c.NewValue);
        }

        public async Task RecordAccountAsync(string address)
        {
            var balanceChanges = new List<(string Address, EvmUInt256 NewBalance)>();
            var nonceChanges = new List<(string Address, ulong NewNonce)>();
            var codeChanges = new List<(string Address, byte[] NewCode)>();
            await ComputeAccountDeltaAsync(address, balanceChanges, nonceChanges, codeChanges).ConfigureAwait(false);

            foreach (var c in balanceChanges) ApplyBalanceChange(c.Address, c.NewBalance);
            foreach (var c in nonceChanges) ApplyNonceChange(c.Address, c.NewNonce);
            foreach (var c in codeChanges) ApplyCodeChange(c.Address, c.NewCode);
        }

        public async Task RecordStorageAsync(string address, EvmUInt256 slot)
        {
            var storageWrites = new List<(string Address, EvmUInt256 Slot, EvmUInt256 NewValue)>();
            await ComputeStorageDeltaAsync(address, slot, storageWrites).ConfigureAwait(false);
            foreach (var c in storageWrites) ApplyStorageWrite(c.Address, c.Slot, c.NewValue);
        }

        public async Task PrepareAccountAsync(string address)
        {
            var basis = GetOrAddBasis(address);
            if (basis.Loaded) return;
            var account = await _stateStore.GetAccountAsync(address).ConfigureAwait(false);
            basis.Balance = account?.Balance ?? EvmUInt256.Zero;
            basis.Nonce = BlockAccessListValueConventions.NonceOf(account?.Nonce ?? EvmUInt256.Zero);
            basis.Code = account != null ? await ResolveCodeAsync(account).ConfigureAwait(false) : null;
            basis.Loaded = true;
        }

        public async Task PrepareStorageAsync(string address, EvmUInt256 slot)
        {
            var basis = GetOrAddBasis(address);
            if (basis.Storage.ContainsKey(slot)) return;
            var raw = await _stateStore.GetStorageAsync(address, slot).ConfigureAwait(false);
            basis.Storage[slot] = EvmUInt256.FromBigEndian(raw);
        }

        private async Task ComputeAccountDeltaAsync(
            string address,
            List<(string Address, EvmUInt256 NewBalance)> balanceChanges,
            List<(string Address, ulong NewNonce)> nonceChanges,
            List<(string Address, byte[] NewCode)> codeChanges)
        {
            var basis = GetOrAddBasis(address);
            var account = await _stateStore.GetAccountAsync(address).ConfigureAwait(false);

            var balance = account?.Balance ?? EvmUInt256.Zero;
            if (!balance.Equals(basis.Balance))
                balanceChanges.Add((address, balance));

            var nonce = BlockAccessListValueConventions.NonceOf(account?.Nonce ?? EvmUInt256.Zero);
            if (nonce != basis.Nonce)
                nonceChanges.Add((address, nonce));

            var code = account != null ? await ResolveCodeAsync(account).ConfigureAwait(false) : null;
            if (code != null && !BlockAccessListValueConventions.SameCode(code, basis.Code))
                codeChanges.Add((address, code));
        }

        private async Task ComputeStorageDeltaAsync(
            string address, EvmUInt256 slot, List<(string Address, EvmUInt256 Slot, EvmUInt256 NewValue)> storageWrites)
        {
            var basis = GetOrAddBasis(address);
            var raw = await _stateStore.GetStorageAsync(address, slot).ConfigureAwait(false);
            var value = EvmUInt256.FromBigEndian(raw);
            var previous = basis.Storage.TryGetValue(slot, out var known) ? known : EvmUInt256.Zero;
            if (!value.Equals(previous))
                storageWrites.Add((address, slot, value));
        }

        private void ApplyBalanceChange(string address, EvmUInt256 newBalance)
        {
            _builder.AddBalanceChange(address, newBalance);
            GetOrAddBasis(address).Balance = newBalance;
        }

        private void ApplyNonceChange(string address, ulong newNonce)
        {
            _builder.AddNonceChange(address, newNonce);
            GetOrAddBasis(address).Nonce = newNonce;
        }

        private void ApplyCodeChange(string address, byte[] newCode)
        {
            _builder.AddCodeChange(address, newCode);
            GetOrAddBasis(address).Code = newCode;
        }

        private void ApplyStorageWrite(string address, EvmUInt256 slot, EvmUInt256 newValue)
        {
            _builder.AddStorageWrite(address, slot, newValue);
            GetOrAddBasis(address).Storage[slot] = newValue;
        }

        public void FlushPendingReads()
        {
            foreach (var address in _pendingAccountReads)
                _builder.EnsureAccount(address);
            foreach (var (address, slot) in _pendingStorageReads)
                _builder.AddStorageRead(address, slot);
            _pendingAccountReads.Clear();
            _pendingStorageReads.Clear();
        }

        public List<AccountChanges> Build() => _builder.Build();

        private AccountBasis GetOrAddBasis(string address)
        {
            var key = BlockAccessListValueConventions.NormaliseAddress(address);
            if (!_basis.TryGetValue(key, out var basis))
            {
                basis = new AccountBasis();
                _basis[key] = basis;
            }
            return basis;
        }

        private async Task<byte[]?> ResolveCodeAsync(Account account)
        {
            if (account.CodeHash == null || IsEmptyCodeHash(account.CodeHash)) return Array.Empty<byte>();
            return await _stateStore.GetCodeAsync(account.CodeHash).ConfigureAwait(false);
        }

        private static bool IsEmptyCodeHash(byte[] codeHash)
        {
            if (codeHash == null) return true;
            return ByteUtil.AreEqual(codeHash, DefaultValues.EMPTY_DATA_HASH);
        }
    }
}
