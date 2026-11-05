using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class FlatStateBatch
    {
        private readonly IReadOnlyList<string> _deletedAccountText;
        private readonly IReadOnlyList<string> _clearedStorageText;
        private readonly IReadOnlyList<(string Address, BigInteger Slot, byte[] Value)> _nonZeroStorageText;
        private readonly IReadOnlyList<(string Address, BigInteger Slot)> _deletedSlotsText;
        private readonly IReadOnlyList<(string Address, Account Account)> _accountsText;

        private readonly IReadOnlyList<EvmAddress> _deletedAccountBytes;
        private readonly IReadOnlyList<EvmAddress> _clearedStorageBytes;
        private readonly IReadOnlyList<(EvmAddress Address, BigInteger Slot, byte[] Value)> _nonZeroStorageBytes;
        private readonly IReadOnlyList<(EvmAddress Address, BigInteger Slot)> _deletedSlotsBytes;
        private readonly IReadOnlyList<(EvmAddress Address, Account Account)> _accountsBytes;

        public IReadOnlyList<(byte[] CodeHash, byte[] Code)> Code { get; }

        public FlatStateBatch(
            IReadOnlyList<string> deletedAccountAddresses,
            IReadOnlyList<string> clearedStorageAddresses,
            IReadOnlyList<(string Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            IReadOnlyList<(string Address, BigInteger Slot)> deletedSlots,
            IReadOnlyList<(string Address, Account Account)> accounts,
            IReadOnlyList<(byte[] CodeHash, byte[] Code)> code)
        {
            _deletedAccountText = deletedAccountAddresses ?? Array.Empty<string>();
            _clearedStorageText = clearedStorageAddresses ?? Array.Empty<string>();
            _nonZeroStorageText = nonZeroStorage ?? Array.Empty<(string, BigInteger, byte[])>();
            _deletedSlotsText = deletedSlots ?? Array.Empty<(string, BigInteger)>();
            _accountsText = accounts ?? Array.Empty<(string, Account)>();
            Code = code ?? Array.Empty<(byte[], byte[])>();
        }

        private FlatStateBatch(
            IReadOnlyList<EvmAddress> deletedAccounts,
            IReadOnlyList<EvmAddress> clearedStorage,
            IReadOnlyList<(EvmAddress Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            IReadOnlyList<(EvmAddress Address, BigInteger Slot)> deletedSlots,
            IReadOnlyList<(EvmAddress Address, Account Account)> accounts,
            IReadOnlyList<(byte[] CodeHash, byte[] Code)> code)
        {
            _deletedAccountBytes = deletedAccounts ?? Array.Empty<EvmAddress>();
            _clearedStorageBytes = clearedStorage ?? Array.Empty<EvmAddress>();
            _nonZeroStorageBytes = nonZeroStorage ?? Array.Empty<(EvmAddress, BigInteger, byte[])>();
            _deletedSlotsBytes = deletedSlots ?? Array.Empty<(EvmAddress, BigInteger)>();
            _accountsBytes = accounts ?? Array.Empty<(EvmAddress, Account)>();
            Code = code ?? Array.Empty<(byte[], byte[])>();
        }

        public static FlatStateBatch FromAddresses(
            IReadOnlyList<EvmAddress> deletedAccounts,
            IReadOnlyList<EvmAddress> clearedStorage,
            IReadOnlyList<(EvmAddress Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            IReadOnlyList<(EvmAddress Address, BigInteger Slot)> deletedSlots,
            IReadOnlyList<(EvmAddress Address, Account Account)> accounts,
            IReadOnlyList<(byte[] CodeHash, byte[] Code)> code)
            => new FlatStateBatch(deletedAccounts, clearedStorage, nonZeroStorage, deletedSlots, accounts, code);

        public IReadOnlyList<string> DeletedAccountAddresses
            => _deletedAccountText ?? Map(_deletedAccountBytes, a => a.ToHexLower());

        public IReadOnlyList<string> ClearedStorageAddresses
            => _clearedStorageText ?? Map(_clearedStorageBytes, a => a.ToHexLower());

        public IReadOnlyList<(string Address, BigInteger Slot, byte[] Value)> NonZeroStorage
            => _nonZeroStorageText ?? Map(_nonZeroStorageBytes, e => (e.Address.ToHexLower(), e.Slot, e.Value));

        public IReadOnlyList<(string Address, BigInteger Slot)> DeletedSlots
            => _deletedSlotsText ?? Map(_deletedSlotsBytes, e => (e.Address.ToHexLower(), e.Slot));

        public IReadOnlyList<(string Address, Account Account)> Accounts
            => _accountsText ?? Map(_accountsBytes, e => (e.Address.ToHexLower(), e.Account));

        public IReadOnlyList<EvmAddress> DeletedAccountEvmAddresses
            => _deletedAccountBytes ?? Map(_deletedAccountText, EvmAddress.FromHex);

        public IReadOnlyList<EvmAddress> ClearedStorageEvmAddresses
            => _clearedStorageBytes ?? Map(_clearedStorageText, EvmAddress.FromHex);

        public IReadOnlyList<(EvmAddress Address, BigInteger Slot, byte[] Value)> NonZeroStorageEvm
            => _nonZeroStorageBytes ?? Map(_nonZeroStorageText, e => (EvmAddress.FromHex(e.Address), e.Slot, e.Value));

        public IReadOnlyList<(EvmAddress Address, BigInteger Slot)> DeletedSlotsEvm
            => _deletedSlotsBytes ?? Map(_deletedSlotsText, e => (EvmAddress.FromHex(e.Address), e.Slot));

        public IReadOnlyList<(EvmAddress Address, Account Account)> AccountsEvm
            => _accountsBytes ?? Map(_accountsText, e => (EvmAddress.FromHex(e.Address), e.Account));

        public bool IsEmpty =>
            Count(_deletedAccountText, _deletedAccountBytes) == 0 &&
            Count(_clearedStorageText, _clearedStorageBytes) == 0 &&
            Count(_nonZeroStorageText, _nonZeroStorageBytes) == 0 &&
            Count(_deletedSlotsText, _deletedSlotsBytes) == 0 &&
            Count(_accountsText, _accountsBytes) == 0 &&
            Code.Count == 0;

        private static int Count<TText, TBytes>(IReadOnlyList<TText> text, IReadOnlyList<TBytes> bytes)
            => text != null ? text.Count : bytes.Count;

        private static IReadOnlyList<TOut> Map<TIn, TOut>(IReadOnlyList<TIn> source, Func<TIn, TOut> convert)
        {
            var mapped = new TOut[source.Count];
            var index = 0;
            foreach (var item in source) mapped[index++] = convert(item);
            return mapped;
        }
    }
}
