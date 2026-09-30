using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-7843: <i>"slotNumber is a uint64 in big endian encoding."</i>
    ///
    /// <para>The host mirror of the guest engine's
    /// <c>Eip7843SlotNumberWidthTests</c>. The value travels
    /// <see cref="BlockHeader.SlotNumber"/> -> <see cref="BlockContext.SlotNumber"/>
    /// -> <c>TransactionExecutionContext.SlotNumber</c>, and the last leg builds
    /// an <see cref="EvmUInt256"/> from it: a signed carrier binds the
    /// sign-extending constructor and turns the top half of the uint64 range
    /// into 2^256-1. <c>BlockExecutorSlotNumberSourceTests</c> covers only the
    /// first leg, and only at 0/100/101, where sign extension is invisible.</para>
    /// </summary>
    public class SlotNumberWidthProductionPathTests
    {
        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string SlotnumContractAddress = "0x9965507D1a55bcC2695C58ba16FB37d819B0A4dc";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";

        private static readonly BigInteger ChainId = 1337;

        private static readonly byte[] SlotnumReturnRuntimeCode = "4B60005260206000F3".HexToByteArray();

        private static ISignedTransaction CallSlotnumContract() =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                SenderPrivateKey.HexToByteArray(), ChainId, SlotnumContractAddress, 0, 0, 0, 100_000, ""));

        private static async Task<string> SlotNumberReturnedBySlotnumAsync(ulong slotNumber)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });

            var codeHash = new Sha3Keccack().CalculateHash(SlotnumReturnRuntimeCode);
            await stateStore.SaveCodeAsync(codeHash, SlotnumReturnRuntimeCode);
            await stateStore.SaveAccountAsync(
                SlotnumContractAddress, new Account { Balance = 0, Nonce = 0, CodeHash = codeHash });

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                Hardfork = nameof(HardforkName.Amsterdam)
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = new BlockExecutor(
                stateStore,
                new InMemoryBlockStore(),
                new FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = 30_000_000,
                BaseFee = 0,
                Coinbase = FeeRecipientAddress,
                ParentHash = new byte[32],
                SlotNumber = slotNumber
            };

            var result = await engine.ExecuteAsync(
                header, new[] { new TxEntry(CallSlotnumContract()) }, uncles: null, withdrawals: null,
                options: new BlockExecutionOptions());

            Assert.Null(result.Exception);
            var receipt = Assert.Single(result.Receipts);
            Assert.True(receipt.Success, receipt.RevertReason);
            return receipt.ReturnData.ToHex();
        }

        [Fact]
        public async Task Given_ASlotNumberAtMaxUint64_When_SLOTNUM_Executes_Then_ItPushesTwoToTheSixtyFourMinusOneZeroExtended()
        {
            Assert.Equal(
                "000000000000000000000000000000000000000000000000ffffffffffffffff",
                await SlotNumberReturnedBySlotnumAsync(ulong.MaxValue));
        }

        [Fact]
        public async Task Given_AnOrdinarySlotNumber_When_SLOTNUM_Executes_Then_ItPushesThatValue()
        {
            Assert.Equal(
                "0000000000000000000000000000000000000000000000000000000000067932",
                await SlotNumberReturnedBySlotnumAsync(424_242));
        }
    }
}
