using System.Numerics;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class Web3NodeDataServiceAdapter : INodeDataService
    {
        private readonly IWeb3 _web3;
        private readonly BlockParameter _blockParameter;

        public Web3NodeDataServiceAdapter(IWeb3 web3, BlockParameter? blockParameter = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _blockParameter = blockParameter ?? BlockParameter.CreateLatest();
        }

        public static Web3NodeDataServiceAdapter CreateForLatest(IWeb3 web3)
        {
            return new Web3NodeDataServiceAdapter(web3, BlockParameter.CreateLatest());
        }

        public static Web3NodeDataServiceAdapter CreateForPending(IWeb3 web3)
        {
            return new Web3NodeDataServiceAdapter(web3, BlockParameter.CreatePending());
        }

        public static Web3NodeDataServiceAdapter CreateForBlock(IWeb3 web3, BigInteger blockNumber)
        {
            return new Web3NodeDataServiceAdapter(web3, new BlockParameter(new HexBigInteger(blockNumber)));
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(address, _blockParameter);
            return balance.Value;
        }

        public Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync(address.ConvertToEthereumChecksumAddress());
        }

        public async Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));

            if (block?.BlockHash == null)
            {
                return new byte[32];
            }

            return block.BlockHash.HexToByteArray();
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(address, _blockParameter);
            if (string.IsNullOrEmpty(code) || code == "0x")
            {
                return Array.Empty<byte>();
            }
            return code.HexToByteArray();
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
        {
            return GetCodeAsync(address.ConvertToEthereumChecksumAddress());
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            var storage = await _web3.Eth.GetStorageAt
                .SendRequestAsync(address, new HexBigInteger(position), _blockParameter);

            if (string.IsNullOrEmpty(storage) || storage == "0x")
            {
                return new byte[32];
            }

            var bytes = storage.HexToByteArray();
            if (bytes.Length < 32)
            {
                var padded = new byte[32];
                Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
                return padded;
            }

            return bytes;
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return GetStorageAtAsync(address.ConvertToEthereumChecksumAddress(), position);
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            var count = await _web3.Eth.Transactions.GetTransactionCount
                .SendRequestAsync(address, _blockParameter);
            return count.Value;
        }

        public Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return GetTransactionCount(address.ConvertToEthereumChecksumAddress());
        }
    }
}
