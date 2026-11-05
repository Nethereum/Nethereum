using System;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed record UnitContext(FlatReconcileEnv Env, byte[] Root, WorkQueue Queue, HashingTally Tally, Action<string> Progress, bool DryRun, bool Journal);
}
