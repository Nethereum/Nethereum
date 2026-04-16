using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class StateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public Dictionary<string, AccountStateSnapshot> AccountSnapshots { get; }
        public HashSet<string> WarmAddresses { get; }
        public Dictionary<string, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; }

        public StateSnapshot(int snapshotId, Dictionary<string, AccountExecutionState> accountsState, HashSet<string> warmAddresses, Dictionary<string, Dictionary<EvmUInt256, byte[]>> transientStorage)
        {
            SnapshotId = snapshotId;
            AccountSnapshots = new Dictionary<string, AccountStateSnapshot>();
            WarmAddresses = new HashSet<string>(warmAddresses);
            TransientStorage = new Dictionary<string, Dictionary<EvmUInt256, byte[]>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in accountsState)
            {
                AccountSnapshots[kvp.Key] = CaptureAccountState(kvp.Value);
            }

            foreach (var kvp in transientStorage)
            {
                var copy = new Dictionary<EvmUInt256, byte[]>();
                foreach (var inner in kvp.Value)
                {
                    copy[inner.Key] = (byte[])inner.Value?.Clone();
                }
                TransientStorage[kvp.Key] = copy;
            }
        }

        private static Dictionary<EvmUInt256, byte[]> CopyStorage(Dictionary<EvmUInt256, byte[]> source)
        {
            var result = new Dictionary<EvmUInt256, byte[]>();
            foreach (var kvp in source)
            {
                result[kvp.Key] = (byte[])kvp.Value?.Clone();
            }
            return result;
        }

        private AccountStateSnapshot CaptureAccountState(AccountExecutionState accountState)
        {
            return new AccountStateSnapshot
            {
                Address = accountState.Address,
                Storage = CopyStorage(accountState.Storage),
                ExecutionBalance = accountState.Balance.ExecutionBalance,
                InitialChainBalance = accountState.Balance.InitialChainBalance,
                Nonce = accountState.Nonce,
                Code = (byte[])accountState.Code?.Clone(),
                WarmStorageKeys = new HashSet<EvmUInt256>(accountState.WarmStorageKeys),
                IsNewContract = accountState.IsNewContract
            };
        }
    }
}
