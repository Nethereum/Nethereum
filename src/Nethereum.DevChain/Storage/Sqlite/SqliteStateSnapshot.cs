using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteStateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public string SavepointName { get; }
        public HashSet<string> DirtyAccountsCopy { get; }
        public Dictionary<string, HashSet<BigInteger>> DirtyStorageSlotsCopy { get; }

        public SqliteStateSnapshot(
            int snapshotId,
            HashSet<string> dirtyAccountsCopy,
            Dictionary<string, HashSet<BigInteger>> dirtyStorageSlotsCopy)
        {
            SnapshotId = snapshotId;
            SavepointName = $"sp_{snapshotId}";
            DirtyAccountsCopy = dirtyAccountsCopy;
            DirtyStorageSlotsCopy = dirtyStorageSlotsCopy;
        }

        public void SetAccount(string address, Account account) { }
        public void SetStorage(string address, BigInteger slot, byte[] value) { }
        public void SetCode(byte[] codeHash, byte[] code) { }
        public void DeleteAccount(string address) { }
        public void ClearStorage(string address) { }

        public void Dispose() { }
    }
}
