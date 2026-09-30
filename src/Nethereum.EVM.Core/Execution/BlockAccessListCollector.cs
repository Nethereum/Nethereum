using System;
using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class BlockAccessListCollector : IStateAccessRecorder
    {
        private readonly BlockAccessListBuilder _builder;
        private readonly InMemoryStateReader _preBlockReader;

        private readonly Dictionary<string, AccountBasis> _basis =
            new Dictionary<string, AccountBasis>(StringComparer.Ordinal);

        private class AccountBasis
        {
            public EvmUInt256 Balance;
            public ulong Nonce;
            public byte[] Code;
            public readonly Dictionary<EvmUInt256, EvmUInt256> Storage =
                new Dictionary<EvmUInt256, EvmUInt256>();
            public AccountState PreBlock;
        }

        public BlockAccessListCollector(BlockAccessListBuilder builder, InMemoryStateReader preBlockReader)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _preBlockReader = preBlockReader ?? throw new ArgumentNullException(nameof(preBlockReader));
        }

        public void BeginUnit(ulong blockAccessIndex)
        {
            _builder.BlockAccessIndex = blockAccessIndex;
        }

        public void RecordAccountRead(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            _builder.EnsureAccount(address);
        }

        public void RecordStorageRead(string address, EvmUInt256 key)
        {
            if (string.IsNullOrEmpty(address)) return;
            _builder.AddStorageRead(address, key);
        }

        public void Record(ulong blockAccessIndex, ExecutionStateService executionState, InMemoryStateReader committedState)
        {
            if (executionState?.AccountsState == null) return;

            _builder.BlockAccessIndex = blockAccessIndex;

            foreach (var pair in executionState.AccountsState)
            {
                var address = pair.Key.ToHexLower();
                var state = pair.Value;
                var key = Key(address);

                if (!_basis.TryGetValue(key, out var basis))
                {
                    var preBlock = _preBlockReader.GetAccountState(address);
                    basis = new AccountBasis
                    {
                        Balance = preBlock?.Balance ?? EvmUInt256.Zero,
                        Nonce = preBlock != null ? NonceOf(preBlock.Nonce) : 0,
                        Code = preBlock?.Code,
                        PreBlock = preBlock
                    };
                    _basis[key] = basis;
                }

                var committed = committedState?.GetAccountState(address);

                var balance = committed != null
                    ? committed.Balance
                    : (state.Balance != null ? state.Balance.GetTotalBalance() : EvmUInt256.Zero);
                if (!balance.Equals(basis.Balance))
                {
                    _builder.AddBalanceChange(address, balance);
                    basis.Balance = balance;
                }

                var nonce = committed != null
                    ? NonceOf(committed.Nonce)
                    : (state.Nonce.HasValue ? NonceOf(state.Nonce.Value) : basis.Nonce);
                if (nonce != basis.Nonce)
                {
                    _builder.AddNonceChange(address, nonce);
                    basis.Nonce = nonce;
                }

                var code = committed != null ? committed.Code : state.Code;
                if (code != null && !SameCode(code, basis.Code))
                {
                    _builder.AddCodeChange(address, code);
                    basis.Code = code;
                }

                if (state.Storage != null)
                {
                    foreach (var slotPair in state.Storage)
                    {
                        var slot = slotPair.Key;

                        var raw = slotPair.Value;
                        if (committed?.Storage != null
                            && committed.Storage.TryGetValue(slot, out var committedSlot))
                        {
                            raw = committedSlot;
                        }

                        var value = raw == null
                            ? EvmUInt256.Zero
                            : EvmUInt256.FromBigEndian(PadTo32(raw));

                        var previous = basis.Storage.TryGetValue(slot, out var known)
                            ? known
                            : PreBlockSlotValue(basis.PreBlock, slot);

                        if (!value.Equals(previous))
                        {
                            _builder.AddStorageWrite(address, slot, value);
                            basis.Storage[slot] = value;
                        }
                    }
                }
            }
        }

        private static ulong NonceOf(EvmUInt256 value) => BlockAccessListValueConventions.NonceOf(value);

        private static EvmUInt256 PreBlockSlotValue(AccountState preBlock, EvmUInt256 slot)
        {
            if (preBlock?.Storage != null && preBlock.Storage.TryGetValue(slot, out var raw))
                return EvmUInt256.FromBigEndian(PadTo32(raw));
            return EvmUInt256.Zero;
        }

        private static bool SameCode(byte[] a, byte[] b) => BlockAccessListValueConventions.SameCode(a, b);

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length == 32) return value;
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }

        private static string Key(string address) => BlockAccessListValueConventions.NormaliseAddress(address);
    }
}
