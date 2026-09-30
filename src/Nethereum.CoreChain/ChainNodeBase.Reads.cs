using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<BigInteger> GetBlockNumberAsync()
        {
            return await _blockStore.GetHeightAsync();
        }

        public virtual async Task<BlockHeader> GetBlockByHashAsync(byte[] hash)
        {
            return await _blockStore.GetByHashAsync(hash);
        }

        public virtual async Task<BlockHeader> GetBlockByNumberAsync(BigInteger number)
        {
            return await _blockStore.GetByNumberAsync(number);
        }

        public virtual async Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            return await _blockStore.GetHashByNumberAsync(blockNumber);
        }

        public virtual async Task<BlockHeader> GetLatestBlockAsync()
        {
            return await _blockStore.GetLatestAsync();
        }

        public virtual async Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash)
        {
            return await _transactionStore.GetByHashAsync(txHash);
        }

        public virtual async Task<Receipt> GetTransactionReceiptAsync(byte[] txHash)
        {
            return await _receiptStore.GetByTxHashAsync(txHash);
        }

        public virtual async Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash)
        {
            return await _receiptStore.GetInfoByTxHashAsync(txHash);
        }

        public virtual async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await _nodeDataService.GetBalanceAsync(address);
        }

        public virtual async Task<BigInteger> GetNonceAsync(string address)
        {
            return await _nodeDataService.GetTransactionCountAsync(address);
        }

        public virtual async Task<byte[]> GetCodeAsync(string address)
        {
            return await _nodeDataService.GetCodeAsync(address);
        }

        public virtual async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot)
        {
            return await _nodeDataService.GetStorageAtAsync(address, slot);
        }

        public virtual async Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            return await dataService.GetBalanceAsync(address);
        }

        public virtual async Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            return await dataService.GetTransactionCountAsync(address);
        }

        public virtual async Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            return await dataService.GetCodeAsync(address);
        }

        public virtual async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot, BigInteger blockNumber)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            return await dataService.GetStorageAtAsync(address, slot);
        }

        public virtual async Task<List<Storage.BlobSidecarRecord>> GetBlobSidecarsByBlockNumberAsync(System.Numerics.BigInteger blockNumber)
        {
            if (_blobStore == null) return new List<Storage.BlobSidecarRecord>();
            return await _blobStore.GetBlobsByBlockNumberAsync(blockNumber);
        }
    }
}
