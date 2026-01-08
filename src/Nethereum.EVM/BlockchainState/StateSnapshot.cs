using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM.BlockchainState
{
    public class StateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public Dictionary<string, AccountStateSnapshot> AccountSnapshots { get; }

        public StateSnapshot(int snapshotId, Dictionary<string, AccountExecutionState> accountsState)
        {
            SnapshotId = snapshotId;
            AccountSnapshots = new Dictionary<string, AccountStateSnapshot>();

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
                Code = accountState.Code?.ToArray()
            };
        }
    }
}
