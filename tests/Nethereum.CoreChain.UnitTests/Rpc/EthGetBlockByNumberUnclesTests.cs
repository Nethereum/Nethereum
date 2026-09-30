using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.UnitTests.Rpc.TestSupport;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EthGetBlockByNumberUnclesTests
    {
        [Fact]
        public async Task Given_an_imported_pre_merge_block_with_uncles_When_eth_getBlockByNumber_is_called_Then_uncles_lists_their_hashes()
        {
            var uncle = PreMergeHeader(blockNumber: 0, extraNonce: 1);
            var chain = await ChainWithUnclesAsync(new List<BlockHeader> { uncle });

            var block = await AskForBlockAsync(chain.Node, "0x1");

            var expectedHash = BlockHashCalculator.ForHeader(uncle).ToHex(true);
            var actualHash = Assert.Single(block.Uncles);
            Assert.Equal(expectedHash, actualHash);
        }

        [Fact]
        public async Task Given_an_imported_block_with_no_uncles_When_eth_getBlockByNumber_is_called_Then_uncles_is_the_empty_array_not_null()
        {
            var chain = await ChainWithUnclesAsync(uncles: null);

            var block = await AskForBlockAsync(chain.Node, "0x1");

            Assert.NotNull(block.Uncles);
            Assert.Empty(block.Uncles);
        }

        [Fact]
        public async Task Given_an_imported_block_with_uncles_When_eth_getBlockByNumber_is_called_Then_size_reflects_the_real_uncle_bytes()
        {
            var uncle = PreMergeHeader(blockNumber: 0, extraNonce: 1);

            var chainWithUncle = await ChainWithUnclesAsync(new List<BlockHeader> { uncle });
            var chainWithoutUncle = await ChainWithUnclesAsync(uncles: null);

            var blockWithUncle = await AskForBlockAsync(chainWithUncle.Node, "0x1");
            var blockWithoutUncle = await AskForBlockAsync(chainWithoutUncle.Node, "0x1");

            Assert.True(blockWithUncle.Size.Value > blockWithoutUncle.Size.Value);

            var expectedSize = BlockHeaderExtensions.CalculateFullBlockSize(
                chainWithUncle.Header, new List<ISignedTransaction>(), new List<BlockHeader> { uncle });
            Assert.Equal(expectedSize, (int)blockWithUncle.Size.Value);
        }

        [Fact]
        public async Task Given_an_imported_pre_merge_block_with_uncles_When_eth_getBlockByHash_is_called_Then_uncles_lists_their_hashes()
        {
            var uncle = PreMergeHeader(blockNumber: 0, extraNonce: 1);
            var chain = await ChainWithUnclesAsync(new List<BlockHeader> { uncle });

            var block = await AskForBlockByHashAsync(chain.Node, chain.BlockHash);

            var expectedHash = BlockHashCalculator.ForHeader(uncle).ToHex(true);
            var actualHash = Assert.Single(block.Uncles);
            Assert.Equal(expectedHash, actualHash);
        }

        private static BlockHeader PreMergeHeader(long blockNumber, byte extraNonce) => new BlockHeader
        {
            BlockNumber = blockNumber,
            Difficulty = new EvmUInt256(1_000_000UL),
            ExtraData = new byte[] { extraNonce },
            WithdrawalsRoot = null
        };

        private static async Task<BlockWithTransactionHashes> AskForBlockAsync(FakeRpcChainNode node, string blockParameter)
        {
            var response = await new EthGetBlockByNumberHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockByNumber", blockParameter, false),
                new RpcContext(node, chainId: 1, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            return Assert.IsType<BlockWithTransactionHashes>(response.ResultNewtonsoft);
        }

        private static async Task<BlockWithTransactionHashes> AskForBlockByHashAsync(FakeRpcChainNode node, byte[] blockHash)
        {
            var response = await new EthGetBlockByHashHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockByHash", blockHash.ToHex(true), false),
                new RpcContext(node, chainId: 1, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            return Assert.IsType<BlockWithTransactionHashes>(response.ResultNewtonsoft);
        }

        private static async Task<(FakeRpcChainNode Node, BlockHeader Header, byte[] BlockHash)> ChainWithUnclesAsync(IList<BlockHeader> uncles)
        {
            var blocks = new InMemoryBlockStore();
            var uncleStore = new InMemoryUncleStore(blocks);
            var transactions = new InMemoryTransactionStore(blocks);

            var header = PreMergeHeader(blockNumber: 1, extraNonce: 0);
            var blockHash = new byte[32];
            blockHash[31] = 0xaa;

            await blocks.SaveAsync(header, blockHash);
            if (uncles != null)
                await uncleStore.SaveAsync(blockHash, uncles);

            var node = new FakeRpcChainNode
            {
                Blocks = blocks,
                Transactions = transactions,
                Uncles = uncleStore
            };

            return (node, header, blockHash);
        }
    }
}
