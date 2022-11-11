using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    public class RpcNodeDataService : INodeDataService
    {
        public RpcNodeDataService(IEthApiService ethApiService, BlockParameter currentBlock) 
        {
            EthApiService = ethApiService;
            CurrentBlock = currentBlock;
        }

        public IEthApiService EthApiService { get; }
        public BlockParameter CurrentBlock { get; }

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
            var storage = await EthApiService.GetStorageAt.SendRequestAsync(address, new HexBigInteger(position), CurrentBlock);
            return storage.HexToByteArray();
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