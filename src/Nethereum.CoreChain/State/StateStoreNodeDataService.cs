using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.State
{
    public class StateStoreNodeDataService : INodeDataService
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;

        public StateStoreNodeDataService(IStateStore stateStore, IBlockStore blockStore = null)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
        }

        public async Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return await GetBalanceAsync(address.ToHex());
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Balance ?? BigInteger.Zero;
        }

        public async Task<byte[]> GetCodeAsync(byte[] address)
        {
            return await GetCodeAsync(address.ToHex());
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            if (account?.CodeHash == null)
                return null;

            return await _stateStore.GetCodeAsync(account.CodeHash);
        }

        public async Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            if (_blockStore == null)
                return null;

            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return await GetStorageAtAsync(address.ToHex(), position);
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            return await _stateStore.GetStorageAsync(address, position);
        }

        public async Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return await GetTransactionCount(address.ToHex());
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Nonce ?? BigInteger.Zero;
        }
    }
}
