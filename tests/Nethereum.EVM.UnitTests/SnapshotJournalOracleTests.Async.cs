using System;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class SnapshotJournalOracleTests
    {
        private static ExecutionStateService NewState() => new ExecutionStateService(new DeterministicReader());

        private static void FillStorage(ExecutionStateService state, EvmAddress address, EvmUInt256 key)
            => state.GetFromStorageWithoutRecordingAccessAsync(address, key).GetAwaiter().GetResult();

        private static void FillCode(ExecutionStateService state, EvmAddress address)
            => state.GetCodeAsync(address).GetAwaiter().GetResult();

        private static void FillNonce(ExecutionStateService state, EvmAddress address)
            => state.GetNonceAsync(address).GetAwaiter().GetResult();

        private static void FillBalance(ExecutionStateService state, EvmAddress address)
            => state.GetTotalBalanceAsync(address).GetAwaiter().GetResult();

        private static AccountExecutionState FillBalanceNonceAndCode(ExecutionStateService state, EvmAddress address)
            => state.LoadBalanceNonceAndCodeFromStorageAsync(address).GetAwaiter().GetResult();

        private static void RevertUnknown(ExecutionStateService state, int id)
            => Assert.Throws<InvalidOperationException>(() => state.RevertToSnapshot(id));

        private sealed class DeterministicReader : IStateReader
        {
            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => Task.FromResult(DeterministicChain.Balance(address));
            public Task<EvmUInt256> GetBalanceAsync(string address) => GetBalanceAsync(EvmAddress.FromHex(address).ToByteArray());
            public Task<byte[]> GetCodeAsync(byte[] address) => Task.FromResult(DeterministicChain.Code(address));
            public Task<byte[]> GetCodeAsync(string address) => GetCodeAsync(EvmAddress.FromHex(address).ToByteArray());
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => Task.FromResult(DeterministicChain.Storage(address, position));
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => GetStorageAtAsync(EvmAddress.FromHex(address).ToByteArray(), position);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => Task.FromResult(DeterministicChain.Nonce(address));
            public Task<EvmUInt256> GetTransactionCountAsync(string address) => GetTransactionCountAsync(EvmAddress.FromHex(address).ToByteArray());
            public Task<bool> AccountExistsAsync(string address) => Task.FromResult(true);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => Task.FromResult(new byte[32]);
        }
    }
}
