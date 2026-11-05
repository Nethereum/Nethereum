using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    internal sealed class NoOpStateSnapshot : IStateSnapshot
    {
        public NoOpStateSnapshot(int id) { SnapshotId = id; }
        public int SnapshotId { get; }
        public void SetAccount(string address, Account account) { }
        public void SetStorage(string address, BigInteger slot, byte[] value) { }
        public void SetCode(byte[] codeHash, byte[] code) { }
        public void DeleteAccount(string address) { }
        public void ClearStorage(string address) { }
        public void Dispose() { }
    }
}
