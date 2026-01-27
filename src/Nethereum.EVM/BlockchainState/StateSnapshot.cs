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

        public StateSnapshot(int snapshotId, Dictionary<string, AccountExecutionState> accountsState, HashSet<string> warmAddresses)
        {
            SnapshotId = snapshotId;
            AccountSnapshots = new Dictionary<string, AccountStateSnapshot>();
            WarmAddresses = new HashSet<string>(warmAddresses);

            foreach (var kvp in accountsState)
            {
                AccountSnapshots[kvp.Key] = CaptureAccountState(kvp.Value);
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
