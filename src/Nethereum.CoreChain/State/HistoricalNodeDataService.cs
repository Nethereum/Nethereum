using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.State
{
    public class HistoricalNodeDataService : INodeDataService
    {
        private readonly IHistoricalStateProvider _historyProvider;
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly BigInteger _blockNumber;

        public HistoricalNodeDataService(
            IHistoricalStateProvider historyProvider,
            IStateStore stateStore,
            IBlockStore blockStore,
            BigInteger blockNumber)
        {
            _historyProvider = historyProvider;
            _stateStore = stateStore;
            _blockStore = blockStore;
            _blockNumber = blockNumber;
        }

        public async Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return await GetBalanceAsync(address.ToHex());
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var account = await _historyProvider.GetAccountAtBlockAsync(address, _blockNumber);
            return account?.Balance ?? BigInteger.Zero;
        }

        public async Task<byte[]> GetCodeAsync(byte[] address)
        {
            return await GetCodeAsync(address.ToHex());
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var account = await _historyProvider.GetAccountAtBlockAsync(address, _blockNumber);
            if (account?.CodeHash == null)
                return null;

            if (IsEmptyCodeHash(account.CodeHash))
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
            return await _historyProvider.GetStorageAtBlockAsync(address, position, _blockNumber);
        }

        public async Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return await GetTransactionCount(address.ToHex());
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            var account = await _historyProvider.GetAccountAtBlockAsync(address, _blockNumber);
            return account?.Nonce ?? BigInteger.Zero;
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
    }
}
