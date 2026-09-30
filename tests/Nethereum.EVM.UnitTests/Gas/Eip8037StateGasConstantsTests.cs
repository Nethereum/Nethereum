using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037StateGasConstantsTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string ContractAddress = "0x2222222222222222222222222222222222222222";
        private const string FreshRecipientAddress = "0x3333333333333333333333333333333333333333";
        private const string AliveRecipientAddress = "0x4444444444444444444444444444444444444444";

        private const long CostPerStateByte = 1_530;
        private const int NewAccountStateBytes = 120;
        private const int StorageSetStateBytes = 64;

        private const long NewAccountStateGas = NewAccountStateBytes * CostPerStateByte;
        private const long StorageSetStateGas = StorageSetStateBytes * CostPerStateByte;

        private static readonly byte[] SetsSlotZeroFromEmpty = { 0x60, 0x07, 0x60, 0x00, 0x55, 0x00 };

        private static readonly byte[] OverwritesSlotZero = SetsSlotZeroFromEmpty;

        private static HardforkConfig Amsterdam => HardforkConfig.Amsterdam.WithPrecompiles(
            Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static byte[] NonZeroWord(byte b)
        {
            var word = new byte[32];
            word[31] = b;
            return word;
        }

        private static async Task<TransactionExecutionResult> RunAsync(
            HardforkConfig config,
            string to,
            byte[] contractCode = null,
            byte[] preExistingSlotZero = null,
            BigInteger? value = null,
            byte[] data = null,
            bool isContractCreation = false)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000"));
            await node.SetBalanceAsync(AliveRecipientAddress, 1);

            if (contractCode != null)
            {
                await node.SetCodeAsync(ContractAddress, contractCode);
                await node.SetBalanceAsync(ContractAddress, 1);
            }
            if (preExistingSlotZero != null)
                await node.SetStorageAsync(ContractAddress, 0, preExistingSlotZero);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = data ?? new byte[0],
                IsContractCreation = isContractCreation,
                GasLimit = 1_000_000,
                Value = value ?? 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            return await new TransactionExecutor(config).ExecuteAsync(ctx);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "state-gas", "EIP-8037: creating an account charges 120 state bytes on the state-gas dimension", Order = 1)]
        [Fact]
        public async Task Given_AValueTransferBringingAnAccountIntoExistence_When_Settled_Then_ItCosts120StateBytes()
        {
            var result = await RunAsync(Amsterdam, FreshRecipientAddress, value: 1000);

            Assert.True(result.Success, result.Error);
            Assert.Equal(NewAccountStateGas, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AValueTransferToAnAccountThatAlreadyExists_When_Settled_Then_NoStateGasIsCharged()
        {
            var result = await RunAsync(Amsterdam, AliveRecipientAddress, value: 1000);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0L, result.StateGasUsed);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "state-gas", "EIP-8037: an SSTORE that grows the state charges 64 state bytes; overwriting charges none", Order = 2)]
        [Fact]
        public async Task Given_AnSstoreSettingASlotThatHeldZero_When_Settled_Then_ItCosts64StateBytes()
        {
            var result = await RunAsync(Amsterdam, ContractAddress, contractCode: SetsSlotZeroFromEmpty);

            Assert.True(result.Success, result.Error);
            Assert.Equal(StorageSetStateGas, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AnSstoreOverwritingASlotThatWasAlreadySet_When_Settled_Then_NoStateGasIsCharged()
        {
            var result = await RunAsync(
                Amsterdam, ContractAddress,
                contractCode: OverwritesSlotZero,
                preExistingSlotZero: NonZeroWord(0x2a));

            Assert.True(result.Success, result.Error);
            Assert.Equal(0L, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_AValueTransferBringingAnAccountIntoExistence_AtPrague_When_Settled_Then_ThereIsNoStateDimension()
        {
            var result = await RunAsync(HardforkConfig.Prague, FreshRecipientAddress, value: 1000);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0L, result.StateGasUsed);
        }

        [Fact]
        public void Given_TheEip8037StateCosts_When_Decomposed_Then_EachIsItsByteCountTimesTheRate()
        {
            Assert.Equal(CostPerStateByte, GasConstants.EIP8037_COST_PER_STATE_BYTE);
            Assert.Equal(NewAccountStateBytes * CostPerStateByte, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            Assert.Equal(StorageSetStateBytes * CostPerStateByte, GasConstants.EIP8037_STORAGE_SET_STATE_GAS);
            Assert.Equal(23 * CostPerStateByte, GasConstants.EIP8037_AUTH_BASE_STATE_GAS);
        }
    }
}
