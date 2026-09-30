using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class StateSnapshot : IStateSnapshot
    {
        public int SnapshotId { get; }
        public Dictionary<EvmAddress, AccountStateSnapshot> AccountSnapshots { get; }
        public HashSet<EvmAddress> WarmAddresses { get; }
        public HashSet<EvmAddress> SelfDestructedAddresses { get; }
        public Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; }

        public StateSnapshot(int snapshotId, Dictionary<EvmAddress, AccountExecutionState> accountsState, HashSet<EvmAddress> warmAddresses, HashSet<EvmAddress> selfDestructedAddresses, Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> transientStorage)
        {
            SnapshotId = snapshotId;
            AccountSnapshots = new Dictionary<EvmAddress, AccountStateSnapshot>();
            WarmAddresses = new HashSet<EvmAddress>(warmAddresses);
            SelfDestructedAddresses = new HashSet<EvmAddress>(selfDestructedAddresses);
            TransientStorage = new Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>>();

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
                IsNewContract = accountState.IsNewContract,
                IsTouched = accountState.IsTouched,
                IsRemoved = accountState.IsRemoved
            };
        }
    }
}
