using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Rsk.RPC.RskEth.DTOs;
using Nethereum.Rsk.Util;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Rsk.IntegrationTests
{
    public class RskDocExampleTests
    {
        private sealed class CannedResponseClient : ClientBase
        {
            private readonly JToken _result;

            public CannedResponseClient(JToken result)
            {
                _result = result;
            }

            public List<RpcRequestMessage> Sent { get; } = new List<RpcRequestMessage>();

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                Sent.Add(rpcRequestMessage);
                return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _result));
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "rsk-client", "Read a block with Rsk's minimumGasPrice field", Order = 1)]
        public async Task ShouldReadTheRskMinimumGasPriceFromABlock()
        {
            var client = new CannedResponseClient(JObject.Parse("{\"number\":\"0x143828\",\"minimumGasPrice\":\"0x387ee40\"}"));
            var web3 = new Web3Rsk(client);

            var block = await web3.RskEth.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger("0x143828"));
            var request = web3.RskEth.GetBlockWithTransactionsByNumber.BuildRequest(BlockParameter.CreateLatest());

            Assert.Equal("0x387ee40", block.MinimumGasPriceString);
            Assert.Equal(59240000, block.GetMinimumGasPriceAsBigInteger());
            Assert.Equal("eth_getBlockByNumber", client.Sent.Single().Method);
            Assert.Equal("eth_getBlockByNumber", request.Method);
            Assert.Equal(true, request.RawParameters[1]);
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "rsk-client", "RSKIP-60 checksum addresses with a chain id", Order = 2)]
        public void ShouldConvertAndValidateRskChecksumAddresses()
        {
            var checksum = AddressUtil.Current.ConvertToChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAED", 30);
            var validOnRskMainnet = AddressUtil.Current.IsChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 30);
            var validForOtherChain = AddressUtil.Current.IsChecksumAddress("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", 1234);
            var eip55 = AddressUtil.Current.ConvertToChecksumAddress("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");

            Assert.Equal("0x5aaEB6053f3e94c9b9a09f33669435E7ef1bEAeD", checksum);
            Assert.True(validOnRskMainnet);
            Assert.False(validForOtherChain);
            Assert.Equal("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", eip55);
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "rsk-client", "Null argument handling of the four block handlers", Order = 3)]
        public void ShouldThrowForANullHashInThreeHandlersAndPassItThroughInTheWithTransactionsByHashHandler()
        {
            var client = new CannedResponseClient(new JObject());
            var web3 = new Web3Rsk(client);

            var passedThrough = web3.RskEth.GetBlockWithTransactionsByHash.BuildRequest(null);

            Assert.Null(passedThrough.RawParameters[0]);
            Assert.Throws<ArgumentNullException>(() => web3.RskEth.GetBlockWithTransactionsHashesByHash.BuildRequest(null));
            Assert.Throws<ArgumentNullException>(() => web3.RskEth.GetBlockWithTransactionsByNumber.BuildRequest((BlockParameter)null));
            Assert.Throws<ArgumentNullException>(() => web3.RskEth.GetBlockWithTransactionsHashesByNumber.BuildRequest((BlockParameter)null));
        }
    }
}
