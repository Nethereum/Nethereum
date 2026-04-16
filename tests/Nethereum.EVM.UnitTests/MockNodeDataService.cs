using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.UnitTests
{
    public class MockNodeDataService : IStateReader
    {
        private static readonly byte[] GenericHash = Enumerable.Repeat((byte)0x42, 32).ToArray();

        public Task<EvmUInt256> GetBalanceAsync(byte[] address) => Task.FromResult(EvmUInt256.Zero);
        public Task<EvmUInt256> GetBalanceAsync(string address) => Task.FromResult(EvmUInt256.Zero);
        public Task<byte[]> GetCodeAsync(byte[] address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetCodeAsync(string address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetBlockHashAsync(long blockNumber) => Task.FromResult(GenericHash);
        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => Task.FromResult(Array.Empty<byte>());
        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => Task.FromResult(EvmUInt256.Zero);
        public Task<EvmUInt256> GetTransactionCountAsync(string address) => Task.FromResult(EvmUInt256.Zero);
    }
}
