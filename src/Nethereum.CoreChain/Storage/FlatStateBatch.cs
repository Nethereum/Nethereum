using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public sealed class FlatStateBatch
    {
        public IReadOnlyList<string> DeletedAccountAddresses { get; }
        public IReadOnlyList<string> ClearedStorageAddresses { get; }
        public IReadOnlyList<(string Address, BigInteger Slot, byte[] Value)> NonZeroStorage { get; }
        public IReadOnlyList<(string Address, BigInteger Slot)> DeletedSlots { get; }
        public IReadOnlyList<(string Address, Account Account)> Accounts { get; }
        public IReadOnlyList<(byte[] CodeHash, byte[] Code)> Code { get; }

        public FlatStateBatch(
            IReadOnlyList<string> deletedAccountAddresses,
            IReadOnlyList<string> clearedStorageAddresses,
            IReadOnlyList<(string Address, BigInteger Slot, byte[] Value)> nonZeroStorage,
            IReadOnlyList<(string Address, BigInteger Slot)> deletedSlots,
            IReadOnlyList<(string Address, Account Account)> accounts,
            IReadOnlyList<(byte[] CodeHash, byte[] Code)> code)
        {
            DeletedAccountAddresses = deletedAccountAddresses ?? Array.Empty<string>();
            ClearedStorageAddresses = clearedStorageAddresses ?? Array.Empty<string>();
            NonZeroStorage = nonZeroStorage ?? Array.Empty<(string, BigInteger, byte[])>();
            DeletedSlots = deletedSlots ?? Array.Empty<(string, BigInteger)>();
            Accounts = accounts ?? Array.Empty<(string, Account)>();
            Code = code ?? Array.Empty<(byte[], byte[])>();
        }

        public bool IsEmpty =>
            DeletedAccountAddresses.Count == 0 &&
            ClearedStorageAddresses.Count == 0 &&
            NonZeroStorage.Count == 0 &&
            DeletedSlots.Count == 0 &&
            Accounts.Count == 0 &&
            Code.Count == 0;
    }
}
