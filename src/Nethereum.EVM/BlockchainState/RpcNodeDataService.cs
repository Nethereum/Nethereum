using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.BlockchainState
{
    public class RpcNodeDataService : INodeDataService
    {
        public RpcNodeDataService(IEthApiService ethApiService, BlockParameter currentBlock)
        {
            EthApiService = ethApiService;
            CurrentBlock = currentBlock;
            UseDebugStorageAt = false;
        }

        public RpcNodeDataService(IEthApiService ethApiService, BlockParameter currentBlock, IDebugApiService debugApiService, string blockHash, int transactionIndex, bool useDebugStorageAt = true):this(ethApiService, currentBlock)
        {
            DebugApiService = debugApiService;
            BlockHash = blockHash;
            TransactionIndex = transactionIndex;
            UseDebugStorageAt = useDebugStorageAt;
        }


        public IEthApiService EthApiService { get; }
        public BlockParameter CurrentBlock { get; }
        public IDebugApiService DebugApiService { get; }
        public string BlockHash { get; }
        public int TransactionIndex { get; }
        public bool UseDebugStorageAt { get; }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var balance = await EthApiService.GetBalance.SendRequestAsync(address, CurrentBlock);
            return balance.Value;
        }

        public Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync(address.ConvertToEthereumChecksumAddress());
        }

        public async Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            var block = await EthApiService.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));
            return block.BlockHash.HexToByteArray();
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
        {
            return GetCodeAsync(address.ConvertToEthereumChecksumAddress());
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var code = await EthApiService.GetCode.SendRequestAsync(address, CurrentBlock);
            return code.HexToByteArray();
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            if (UseDebugStorageAt)
            {
               
                var fullStorage = await DebugApiService.StorageRangeAt.SendRequestAsync(BlockHash, TransactionIndex, address, position, 1);
                var foundStorage = fullStorage.Storage.Where(x => x.Value.Key.Value == position);
                if (foundStorage.Any())
                {
                    return foundStorage.FirstOrDefault().Value.Value.HexToByteArray();
                }
                else
                {
                    var storage = await EthApiService.GetStorageAt.SendRequestAsync(address, new HexBigInteger(position), CurrentBlock);
                    return storage.HexToByteArray();
                }
                
            }
            else
            {
                var storage = await EthApiService.GetStorageAt.SendRequestAsync(address, new HexBigInteger(position), CurrentBlock);
                return storage.HexToByteArray();
            }
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return GetStorageAtAsync(address.ConvertToEthereumChecksumAddress(), position);
        }

        public Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return GetTransactionCount(address.ConvertToEthereumChecksumAddress());
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            var count = await EthApiService.Transactions.GetTransactionCount.SendRequestAsync(address.ConvertToEthereumChecksumAddress(), CurrentBlock);
            return count.Value;
        }

    }
}