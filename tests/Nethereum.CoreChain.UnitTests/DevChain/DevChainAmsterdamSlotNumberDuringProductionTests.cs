using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using static Nethereum.CoreChain.UnitTests.AmsterdamBlockPipelineHarness;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainAmsterdamSlotNumberDuringProductionTests
    {
        private const string SlotRecorder = "0x5107510751075107510751075107510751075107";

        private static readonly byte[] StoreSlotNumberInSlotZero = "4b600055".HexToByteArray();
        private static readonly byte[] ReturnSlotNumber = "4b60005260206000f3".HexToByteArray();

        private const ulong SlotUnlikeAnyBlockNumber = 424_242;

        private static ISignedTransaction CallTo(string address, BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, address, 0, nonce, 2_000_000_000, 200_000, ""));

        [Fact]
        public async Task Given_ASlotUnlikeTheBlockNumber_When_SlotnumRunsInThatBlock_Then_ItObservesTheSlotAndNotTheBlockNumber()
        {
            var produced = await ProduceBlockAsync(
                new List<ISignedTransaction> { CallTo(SlotRecorder, nonce: 0) },
                DefaultBlockGasLimit,
                seed: store => SystemCallBlockHarness.DeployAsync(store, SlotRecorder, ReturnSlotNumber),
                slotNumber: SlotUnlikeAnyBlockNumber);

            Assert.Equal(1, produced.Header.BlockNumber);
            Assert.Equal(SlotUnlikeAnyBlockNumber, produced.Header.SlotNumber);

            var observed = produced.TransactionResults[0];
            Assert.True(observed.Success);
            Assert.Equal(
                (BigInteger)SlotUnlikeAnyBlockNumber,
                new BigInteger(observed.ReturnData, isUnsigned: true, isBigEndian: true));
        }

        [Fact]
        public async Task Given_TheSameCodeAtPrague_When_TheBlockIsProduced_Then_TheTransactionFailsBecauseSlotnumIsNotAnOpcodeThere()
        {
            var produced = await ProduceBlockAsync(
                new List<ISignedTransaction> { CallTo(SlotRecorder, nonce: 0) },
                DefaultBlockGasLimit,
                fork: HardforkName.Prague,
                seed: store => SystemCallBlockHarness.DeployAsync(store, SlotRecorder, ReturnSlotNumber));

            Assert.False(produced.TransactionResults[0].Success);
            Assert.Null(produced.Header.SlotNumber);
        }

        [Fact]
        public async Task Given_AnAmsterdamBlockBeingProduced_When_SlotnumIsExecutedInIt_Then_ItEqualsTheSlotStampedIntoThatBlocksHeader()
        {
            using var node = DevChainNode.CreateInMemory(
                new DevChainConfig { Hardfork = "amsterdam", ChainId = (int)ChainId });
            await node.StartAsync(new[] { SenderAddress }, BigInteger.Parse("10000000000000000000000"));
            await node.SetCodeAsync(SlotRecorder, StoreSlotNumberInSlotZero);

            var nonce = await node.GetNonceAsync(SenderAddress);
            await node.SendTransactionAsync(TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, SlotRecorder, BigInteger.Zero, nonce,
                    1_000_000_000, 200_000, "")));
            await node.MineBlockAsync();

            var header = await node.GetBlockByNumberAsync(1);
            Assert.NotNull(header);
            Assert.True(header.SlotNumber.HasValue, "the header must carry a slot at Amsterdam");

            var observed = await node.CreateWeb3().Eth.GetStorageAt.SendRequestAsync(
                SlotRecorder,
                new HexBigInteger(0),
                new RPC.Eth.DTOs.BlockParameter(new HexBigInteger(1)));

            Assert.Equal(
                (BigInteger)header.SlotNumber.Value,
                new HexBigInteger(observed).Value);
        }
    }
}
