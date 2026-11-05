using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.State
{
    public class StateStoreNodeDataService : IStateReader, IAccountStorageAddressReader
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;

        public StateStoreNodeDataService(IStateStore stateStore, IBlockStore blockStore = null)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
        }

        public Task<EvmUInt256> GetBalanceAsync(byte[] address) => GetBalanceAsync(EvmAddress.From(address));

        public Task<EvmUInt256> GetBalanceAsync(string address) => GetBalanceAsync(EvmAddress.FromHex(address));

        private async Task<EvmUInt256> GetBalanceAsync(EvmAddress address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Balance ?? EvmUInt256.Zero;
        }

        public async Task<bool> AccountHasStorageAsync(string address)
        {
            var slots = await _stateStore.GetAllStorageAsync(address);
            return AccountStorageValues.AnyNonZero(slots?.Values);
        }

        public async Task<bool> AccountHasStorageAsync(EvmAddress address)
        {
            var slots = await _stateStore.GetAllStorageAsync(address);
            return AccountStorageValues.AnyNonZero(slots?.Values);
        }

        public Task<byte[]> GetCodeAsync(byte[] address) => GetCodeAsync(EvmAddress.From(address));

        public Task<byte[]> GetCodeAsync(string address) => GetCodeAsync(EvmAddress.FromHex(address));

        private async Task<byte[]> GetCodeAsync(EvmAddress address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            if (account?.CodeHash == null)
                return null;

            if (ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                return null;

            return await _stateStore.GetCodeAsync(account.CodeHash);
        }

        public async Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            if (_blockStore == null)
                return null;

            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
            => _stateStore.GetStorageAsync(EvmAddress.From(address), position);

        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
            => _stateStore.GetStorageAsync(EvmAddress.FromHex(address), position);

        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => GetTransactionCountAsync(EvmAddress.From(address));

        public Task<EvmUInt256> GetTransactionCountAsync(string address) => GetTransactionCountAsync(EvmAddress.FromHex(address));

        private async Task<EvmUInt256> GetTransactionCountAsync(EvmAddress address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Nonce ?? EvmUInt256.Zero;
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return _stateStore.AccountExistsAsync(EvmAddress.FromHex(address));
        }
    }
}
