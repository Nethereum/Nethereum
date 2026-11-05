using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainTestingRpcTests : IDisposable
    {
        private const string Recipient = "0x000000000000000000000000000000000000dEaD";
        private const string ZeroWord = "0x0000000000000000000000000000000000000000000000000000000000000000";

        private readonly DevChainConfig _config;
        private readonly EthECKey _sender = EthECKey.GenerateKey();
        private readonly DevChainNode _node;
        private readonly RpcDispatcher _dispatcher;

        public DevChainTestingRpcTests()
        {
            _config = DevChainConfig.Default;
            _config.AutoMine = false;
            _node = new DevChainNode(_config);
            _node.StartAsync(_sender).GetAwaiter().GetResult();
            _dispatcher = new RpcDispatcher(
                DevRpcHandlerExtensions.CreateDevChainRegistry(),
                new RpcContext(_node, _config.ChainId, new ServiceCollection().BuildServiceProvider()));
        }

        public void Dispose() => _node.Dispose();

        private string Transfer(int nonce)
        {
            var tx = new Transaction1559(
                chainId: _config.ChainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(10_000_000_000), gasLimit: new BigInteger(21_000),
                receiverAddress: Recipient, amount: new BigInteger(1_000_000_000), data: string.Empty, accessList: null);
            var signature = _sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx.GetRLPEncoded().ToHex(true);
        }

        private async Task<string> AttributesAsync()
        {
            var parent = await _node.Blocks.GetLatestAsync();
            return "{\"timestamp\":\"0x" + (parent.Timestamp + 12).ToString("x") + "\",\"prevRandao\":\"" + ZeroWord +
                   "\",\"suggestedFeeRecipient\":\"" + Recipient + "\",\"withdrawals\":[],\"parentBeaconBlockRoot\":\"" + ZeroWord + "\"}";
        }

        private async Task<string> HeadHashAsync()
        {
            var head = await _node.Blocks.GetLatestAsync();
            return (await _node.Blocks.GetHashByNumberAsync(head.BlockNumber)).ToHex(true);
        }

        private async Task<RpcResponseMessage> CallAsync(string method, string paramsJson)
        {
            using var doc = JsonDocument.Parse(paramsJson);
            return await _dispatcher.DispatchAsync(new RpcRequestMessage
            {
                Id = 1,
                Method = method,
                RawParameters = doc.RootElement.Clone()
            });
        }

        [Fact]
        public async Task Given_ExplicitTransactions_When_testing_buildBlockV1_Then_ThePayloadIsReturnedAndTheHeadAndPoolAreUntouched()
        {
            var parent = await HeadHashAsync();

            var response = await CallAsync("testing_buildBlockV1",
                "[\"" + parent + "\"," + await AttributesAsync() + ",[\"" + Transfer(0) + "\"],\"0x\"]");

            Assert.Null(response.Error);
            var payload = Assert.IsType<GetPayloadV4Response>(response.Result);
            Assert.Equal(parent, payload.ExecutionPayload.ParentHash);
            Assert.Single(payload.ExecutionPayload.Transactions);
            Assert.Equal(0L, (long)(await _node.Blocks.GetLatestAsync()).BlockNumber);
            Assert.Equal(parent, await HeadHashAsync());
        }

        [Fact]
        public async Task Given_ANullTransactionList_When_testing_buildBlockV1_Then_TheBlockIsBuiltFromThePoolWithoutDrainingIt()
        {
            await _node.SendTransactionAsync(TransactionFactory.CreateTransaction(Transfer(0).HexToByteArray()));
            var parent = await HeadHashAsync();

            var response = await CallAsync("testing_buildBlockV1",
                "[\"" + parent + "\"," + await AttributesAsync() + ",null,null]");

            Assert.Null(response.Error);
            var payload = Assert.IsType<GetPayloadV4Response>(response.Result);
            Assert.Single(payload.ExecutionPayload.Transactions);
            Assert.Equal(1, _node.GetPendingTransactionCount());
        }

        [Fact]
        public async Task Given_AParentThatIsNotTheHead_When_testing_buildBlockV1_Then_ItIsRefused()
        {
            var response = await CallAsync("testing_buildBlockV1",
                "[\"" + ZeroWord + "\"," + await AttributesAsync() + ",[],null]");

            Assert.NotNull(response.Error);
            Assert.Equal(-32000, response.Error.Code);
        }

        [Fact]
        public async Task Given_ATransactionThatCannotBeApplied_When_testing_buildBlockV1_Then_ItIsRefusedWithoutBuilding()
        {
            var response = await CallAsync("testing_buildBlockV1",
                "[\"" + await HeadHashAsync() + "\"," + await AttributesAsync() + ",[\"" + Transfer(7) + "\"],null]");

            Assert.NotNull(response.Error);
            Assert.Equal(-32000, response.Error.Code);
        }

        [Fact]
        public async Task Given_ExplicitTransactions_When_testing_commitBlockV1_Then_TheBlockBecomesTheCanonicalHeadAndTheReturnedHashIsItsHash()
        {
            var response = await CallAsync("testing_commitBlockV1",
                "[" + await AttributesAsync() + ",[\"" + Transfer(0) + "\"],\"0x01\"]");

            Assert.Null(response.Error);
            var committedHash = Assert.IsType<string>(response.Result);
            var head = await _node.Blocks.GetLatestAsync();
            Assert.Equal(1L, (long)head.BlockNumber);
            Assert.Equal(committedHash, await HeadHashAsync());
            Assert.Equal("0x01", head.ExtraData.ToHex(true));
            Assert.Single(_node.BlockManager.LastBlockProductionResult.IncludedTransactions);
        }

        [Fact]
        public async Task Given_ATransactionThatCannotBeApplied_When_testing_commitBlockV1_Then_ItIsRefusedAndTheHeadStays()
        {
            var before = await HeadHashAsync();

            var response = await CallAsync("testing_commitBlockV1",
                "[" + await AttributesAsync() + ",[\"" + Transfer(7) + "\"],null]");

            Assert.NotNull(response.Error);
            Assert.Equal(before, await HeadHashAsync());
            Assert.Equal(0L, (long)(await _node.Blocks.GetLatestAsync()).BlockNumber);
        }

        [Fact]
        public async Task Given_ANullTransactionList_When_testing_commitBlockV1_Then_ThePoolIsIncludedAndDrained()
        {
            await _node.SendTransactionAsync(TransactionFactory.CreateTransaction(Transfer(0).HexToByteArray()));

            var response = await CallAsync("testing_commitBlockV1", "[" + await AttributesAsync() + ",null,null]");

            Assert.Null(response.Error);
            Assert.Equal(1L, (long)(await _node.Blocks.GetLatestAsync()).BlockNumber);
            Assert.Equal(0, _node.GetPendingTransactionCount());
        }
    }
}
