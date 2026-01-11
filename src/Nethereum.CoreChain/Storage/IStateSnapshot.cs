using System;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateSnapshot : IDisposable
    {
        int SnapshotId { get; }
        void SetAccount(string address, Account account);
        void SetStorage(string address, BigInteger slot, byte[] value);
        void SetCode(byte[] codeHash, byte[] code);
        void DeleteAccount(string address);
        void ClearStorage(string address);
    }
}
