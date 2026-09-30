using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.EthTransfers;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("HttpRpcAmsterdam")]
    public class Eip7708EthTransferLogRpcTests : IClassFixture<DevChainAmsterdamHttpFixture>
    {
        private readonly DevChainAmsterdamHttpFixture _fixture;
        private static readonly BigInteger OneEther = BigInteger.Parse("1000000000000000000");

        public Eip7708EthTransferLogRpcTests(DevChainAmsterdamHttpFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<TransactionReceipt> SendAsync(BigInteger wei) =>
            _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(
                new TransactionInput
                {
                    From = _fixture.Account.Address,
                    To = DevChainHttpFixture.RecipientAddress,
                    Value = new HexBigInteger(wei),
                    Gas = new HexBigInteger(21_000)
                });

        private static System.Collections.Generic.List<EventLog<ERC20Transfer>> SystemEmittedTransfers(
            TransactionReceipt receipt) =>
            receipt.Logs.DecodeAllEvents<ERC20Transfer>().NativeTransfers().ToList();

        [Fact]
        public async Task Given_TheAmsterdamFixture_When_ItStarts_Then_TheNodeReportsAmsterdamOverRealJsonRpc()
        {
            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(0)));

            Assert.NotNull(block);
            Assert.NotNull(block.BlockAccessListHash);
            Assert.NotNull(block.SlotNumber);
        }

        [Fact]
        public async Task Given_AValueTransfer_When_TheReceiptIsReadOverJsonRpc_Then_TheSystemEmittedTransferDecodesWithTheShippedHelper()
        {
            var receipt = await SendAsync(OneEther);

            var native = SystemEmittedTransfers(receipt);

            Assert.Single(native);
            Assert.True(native[0].Event.From.IsTheSameAddress(_fixture.Account.Address));
            Assert.True(native[0].Event.To.IsTheSameAddress(DevChainHttpFixture.RecipientAddress));
            Assert.Equal(OneEther, native[0].Event.Value);
        }

        [Fact]
        public async Task Given_AZeroValueTransfer_When_TheReceiptIsRead_Then_NoSystemEmittedTransferIsPresent()
        {
            var receipt = await SendAsync(0);

            Assert.Equal(new HexBigInteger(1), receipt.Status);
            Assert.Empty(SystemEmittedTransfers(receipt));
        }
    }
}
