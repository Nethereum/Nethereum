using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.State
{
    public class StateStoreNodeDataService : IStateReader
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;

        public StateStoreNodeDataService(IStateStore stateStore, IBlockStore blockStore = null)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
        }

        public async Task<EvmUInt256> GetBalanceAsync(byte[] address)
        {
            return await GetBalanceAsync(address.ToHex());
        }

        public async Task<EvmUInt256> GetBalanceAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Balance ?? EvmUInt256.Zero;
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

            // Empty code hash means no code
            if (IsEmptyCodeHash(account.CodeHash))
                return null;

            return await _stateStore.GetCodeAsync(account.CodeHash);
        }

        private static bool IsEmptyCodeHash(byte[] codeHash)
        {
            if (codeHash == null || codeHash.Length != DefaultValues.EMPTY_DATA_HASH.Length)
                return false;
            for (int i = 0; i < codeHash.Length; i++)
            {
                if (codeHash[i] != DefaultValues.EMPTY_DATA_HASH[i])
                    return false;
            }
            return true;
        }

        public async Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            if (_blockStore == null)
                return null;

            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
        {
            return await GetStorageAtAsync(address.ToHex(), position);
        }

        public async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            return await _stateStore.GetStorageAsync(address, position);
        }

        public async Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
        {
            return await GetTransactionCountAsync(address.ToHex());
        }

        public async Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Nonce ?? EvmUInt256.Zero;
        }
    }
}
