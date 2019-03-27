using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.ModelFactories;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    //TODO:This needs to be moved to a custom testing library
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class BlockHeaderIntegrationTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public BlockHeaderIntegrationTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDecodeCliqueAuthor()
        {
            if (_ethereumClientIntegrationFixture.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var block =
                    await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(1));
                var blockHeader = BlockHeaderRPCFactory.FromRPC(block);
                var blockEncoded = BlockHeaderEncoder.Current.EncodeCliqueSigHeader(blockHeader);
                var signature = blockHeader.ExtraData.Skip(blockHeader.ExtraData.Length - 65).ToArray();
                var account =
                    new MessageSigner().EcRecover(BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(blockHeader),
                        signature.ToHex());
                Assert.True(AccountFactory.Address.IsTheSameAddress(account));

            }

        }


        [Fact]
        public async void ShouldEncodeDecode()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(1));
            var blockHeader = BlockHeaderRPCFactory.FromRPC(block);

            var encoded = BlockHeaderEncoder.Current.Encode(blockHeader);
            var decoded = BlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(blockHeader.StateRoot.ToHex(), decoded.StateRoot.ToHex());
            Assert.Equal(blockHeader.LogsBloom.ToHex(), decoded.LogsBloom.ToHex());
            Assert.Equal(blockHeader.MixHash.ToHex(), decoded.MixHash.ToHex());
            Assert.Equal(blockHeader.ReceiptHash.ToHex(), decoded.ReceiptHash.ToHex());
            Assert.Equal(blockHeader.Difficulty, decoded.Difficulty);

        }

    }
}