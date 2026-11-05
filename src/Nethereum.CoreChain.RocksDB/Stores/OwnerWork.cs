using System;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal readonly record struct OwnerWork(byte[] Owner, string Range, string Cause, Action<FlatLeafWalk> Walk);
}
