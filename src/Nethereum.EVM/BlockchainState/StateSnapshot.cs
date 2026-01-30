using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM.BlockchainState
{
    public class StateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public Dictionary<string, AccountStateSnapshot> AccountSnapshots { get; }
        public HashSet<string> WarmAddresses { get; }
        public Dictionary<string, Dictionary<BigInteger, byte[]>> TransientStorage { get; }

        public StateSnapshot(int snapshotId, Dictionary<string, AccountExecutionState> accountsState, HashSet<string> warmAddresses, Dictionary<string, Dictionary<BigInteger, byte[]>> transientStorage)
        {
            SnapshotId = snapshotId;
            AccountSnapshots = new Dictionary<string, AccountStateSnapshot>();
            WarmAddresses = new HashSet<string>(warmAddresses);
            TransientStorage = new Dictionary<string, Dictionary<BigInteger, byte[]>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in accountsState)
            {
                AccountSnapshots[kvp.Key] = CaptureAccountState(kvp.Value);
            }

            foreach (var kvp in transientStorage)
            {
                TransientStorage[kvp.Key] = new Dictionary<BigInteger, byte[]>(kvp.Value.ToDictionary(x => x.Key, x => x.Value?.ToArray()));
            }
        }

        private AccountStateSnapshot CaptureAccountState(AccountExecutionState accountState)
        {
            return new AccountStateSnapshot
            {
                Address = accountState.Address,
                Storage = accountState.Storage.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToArray()
                ),
                ExecutionBalance = accountState.Balance.ExecutionBalance,
                Nonce = accountState.Nonce,
                Code = accountState.Code?.ToArray(),
                WarmStorageKeys = new HashSet<BigInteger>(accountState.WarmStorageKeys)
            };
        }
    }
}
