using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.EVM.UnitTests
{
    public class MockNodeDataService : IStateReader
    {
        private readonly InMemoryStateReader _reader;

        public MockNodeDataService(InMemoryStateReader reader = null)
        {
            _reader = reader ?? new InMemoryStateReader(new Dictionary<string, AccountState>());
        }

        public Task<EvmUInt256> GetBalanceAsync(byte[] address) => _reader.GetBalanceAsync(address);
        public Task<EvmUInt256> GetBalanceAsync(string address) => _reader.GetBalanceAsync(address);
        public Task<byte[]> GetCodeAsync(byte[] address) => _reader.GetCodeAsync(address);
        public Task<byte[]> GetCodeAsync(string address) => _reader.GetCodeAsync(address);

        public Task<byte[]> GetBlockHashAsync(long blockNumber) =>
            Task.FromResult(Sha3Keccack.Current.CalculateHashAsBytes(blockNumber.ToString()));

        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _reader.GetStorageAtAsync(address, position);
        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _reader.GetStorageAtAsync(address, position);
        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => _reader.GetTransactionCountAsync(address);
        public Task<EvmUInt256> GetTransactionCountAsync(string address) => _reader.GetTransactionCountAsync(address);
        public Task<bool> AccountExistsAsync(string address) => _reader.AccountExistsAsync(address);
    }
}
