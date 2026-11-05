using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Core.Tests
{
    public partial class SnapshotJournalOracleTests
    {
        private static ExecutionStateService NewState() => new ExecutionStateService(new DeterministicReader());

        private static void FillStorage(ExecutionStateService state, EvmAddress address, EvmUInt256 key)
            => state.GetFromStorageWithoutRecordingAccess(address, key);

        private static void FillCode(ExecutionStateService state, EvmAddress address)
            => state.GetCode(address);

        private static void FillNonce(ExecutionStateService state, EvmAddress address)
            => state.GetNonce(address);

        private static void FillBalance(ExecutionStateService state, EvmAddress address)
            => state.GetTotalBalance(address);

        private static AccountExecutionState FillBalanceNonceAndCode(ExecutionStateService state, EvmAddress address)
            => state.LoadBalanceNonceAndCodeFromStorage(address);

        private static void RevertUnknown(ExecutionStateService state, int id)
            => state.RevertToSnapshot(id);

        private sealed class DeterministicReader : IStateReader
        {
            public EvmUInt256 GetBalance(byte[] address) => DeterministicChain.Balance(address);
            public EvmUInt256 GetBalance(string address) => GetBalance(EvmAddress.FromHex(address).ToByteArray());
            public byte[] GetCode(byte[] address) => DeterministicChain.Code(address);
            public byte[] GetCode(string address) => GetCode(EvmAddress.FromHex(address).ToByteArray());
            public byte[] GetStorageAt(byte[] address, EvmUInt256 position) => DeterministicChain.Storage(address, position);
            public byte[] GetStorageAt(string address, EvmUInt256 position) => GetStorageAt(EvmAddress.FromHex(address).ToByteArray(), position);
            public EvmUInt256 GetTransactionCount(byte[] address) => DeterministicChain.Nonce(address);
            public EvmUInt256 GetTransactionCount(string address) => GetTransactionCount(EvmAddress.FromHex(address).ToByteArray());
            public bool AccountExists(string address) => true;
            public byte[] GetBlockHash(long blockNumber) => new byte[32];
        }
    }
}
