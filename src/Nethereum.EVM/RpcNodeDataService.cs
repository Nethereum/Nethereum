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

        public async Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            var balance = await EthApiService.GetBalance.SendRequestAsync(address.ConvertToEthereumChecksumAddress(), CurrentBlock);
            return balance.Value;
        }

        public async Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            var block = await EthApiService.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));
            return block.BlockHash.HexToByteArray();
        }

        public async Task<byte[]> GetCodeAsync(byte[] address)
        {
            var code = await EthApiService.GetCode.SendRequestAsync(address.ConvertToEthereumChecksumAddress(), CurrentBlock);
            return code.HexToByteArray();
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            var storage = await EthApiService.GetStorageAt.SendRequestAsync(address.ConvertToEthereumChecksumAddress(), new HexBigInteger(position), CurrentBlock);
            return storage.HexToByteArray();
        }
       
    }
}