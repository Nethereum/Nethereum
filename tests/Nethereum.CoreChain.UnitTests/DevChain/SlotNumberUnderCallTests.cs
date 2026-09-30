using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class SlotNumberUnderCallTests
    {
        private const string SlotReporter = "0x5107510751075107510751075107510751075107";
        private static readonly byte[] ReturnSlotNumber = "4b60005260206000f3".HexToByteArray();

        private static async Task<DevChainNode> StartAsync(string fork)
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = fork, ChainId = 1337 });
            await node.StartAsync();
            await node.SetCodeAsync(SlotReporter, ReturnSlotNumber);
            return node;
        }

        private static async Task<BigInteger> SlotnumUnderCallAsync(DevChainNode node, BlockParameter block)
        {
            var returned = await node.CreateWeb3().Eth.Transactions.Call.SendRequestAsync(
                new CallInput { To = SlotReporter, Gas = new HexBigInteger(200_000) }, block);

            return new HexBigInteger(returned).Value;
        }

        [Fact]
        public async Task Given_AnAmsterdamChain_When_SlotnumRunsUnderEthCall_Then_ItReportsTheBlockSlotRatherThanZero()
        {
            using var node = await StartAsync("amsterdam");
            await node.MineBlockAsync();
            await node.MineBlockAsync();

            var observed = await SlotnumUnderCallAsync(node, BlockParameter.CreateLatest());

            Assert.NotEqual(BigInteger.Zero, observed);
        }

        [Fact]
        public async Task Given_AnAmsterdamChain_When_SlotnumRunsAgainstAnOlderBlock_Then_ItReportsThatBlocksSlotNotTheLatest()
        {
            using var node = await StartAsync("amsterdam");
            await node.MineBlockAsync();
            await node.MineBlockAsync();
            await node.MineBlockAsync();

            var atFirst = await SlotnumUnderCallAsync(node, new BlockParameter(new HexBigInteger(1)));
            var atThird = await SlotnumUnderCallAsync(node, new BlockParameter(new HexBigInteger(3)));

            Assert.NotEqual(atFirst, atThird);

            var first = await node.GetBlockByNumberAsync(1);
            var third = await node.GetBlockByNumberAsync(3);
            Assert.Equal((BigInteger)first.SlotNumber.Value, atFirst);
            Assert.Equal((BigInteger)third.SlotNumber.Value, atThird);
        }

        [Fact]
        public async Task Given_APragueChain_When_TheSameCodeRunsUnderEthCall_Then_TheCallFailsBecauseSlotnumIsNotAnOpcodeThere()
        {
            using var node = await StartAsync("prague");
            await node.MineBlockAsync();

            await Assert.ThrowsAnyAsync<System.Exception>(
                () => SlotnumUnderCallAsync(node, BlockParameter.CreateLatest()));
        }
    }
}
