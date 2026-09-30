using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip7954CodeSizeLimitsTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";

        private const int PragueMaxCodeSize = 24_576;
        private const int AmsterdamMaxCodeSize = 65_536;
        private const int PragueMaxInitcodeSize = 49_152;
        private const int AmsterdamMaxInitcodeSize = 131_072;

        private const string MaxCodeSizeExceeded = "MAX_CODE_SIZE_EXCEEDED";
        private const string CodeDepositOutOfGas = "CODE_DEPOSIT_OUT_OF_GAS";
        private const string InitcodeSizeExceeded = "INITCODE_SIZE_EXCEEDED";

        private static byte[] InitCodeReturning(int length) => new byte[]
        {
            0x62, (byte)(length >> 16), (byte)(length >> 8), (byte)length,
            0x60, 0x00,
            0xf3
        };

        private static HardforkConfig Amsterdam => HardforkConfig.Amsterdam.WithPrecompiles(
            Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static HardforkConfig Prague => HardforkConfig.Prague;

        private static async Task<TransactionExecutionResult> CreateAsync(
            HardforkConfig config, byte[] initCode)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("100000000000000000000000"));

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
                GasLimit = 16_000_000,
                Value = 0,
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

        [Fact]
        public async Task Given_ACreationReturningOneByteOverTheAmsterdamCodeCap_When_Executed_Then_ItIsRefusedForCodeSize()
        {
            var result = await CreateAsync(Amsterdam, InitCodeReturning(AmsterdamMaxCodeSize + 1));

            Assert.False(result.Success);
            Assert.Equal(MaxCodeSizeExceeded, result.Error);
        }

        [Fact]
        public async Task Given_ACreationReturningExactlyTheAmsterdamCodeCap_When_Executed_Then_ItIsNotRefusedForCodeSize()
        {
            var result = await CreateAsync(Amsterdam, InitCodeReturning(AmsterdamMaxCodeSize));

            Assert.Equal(CodeDepositOutOfGas, result.Error);
        }

        [Fact]
        public async Task Given_ACreationReturningJustOverThePragueCodeCap_When_ExecutedAtPrague_Then_ItIsRefusedForCodeSize()
        {
            var result = await CreateAsync(Prague, InitCodeReturning(PragueMaxCodeSize + 1));

            Assert.False(result.Success);
            Assert.Equal(MaxCodeSizeExceeded, result.Error);
        }

        [Fact]
        public async Task Given_ACreationReturningJustOverThePragueCodeCap_When_ExecutedAtAmsterdam_Then_ItIsNotRefusedForCodeSize()
        {
            var result = await CreateAsync(Amsterdam, InitCodeReturning(PragueMaxCodeSize + 1));

            Assert.Equal(CodeDepositOutOfGas, result.Error);
        }

        [Fact]
        public async Task Given_ACreationTransactionOneByteOverTheAmsterdamInitcodeCap_When_Validated_Then_ItIsRejected()
        {
            var result = await CreateAsync(Amsterdam, new byte[AmsterdamMaxInitcodeSize + 1]);

            Assert.False(result.Success);
            Assert.Equal(InitcodeSizeExceeded, result.Error);
        }

        [Fact]
        public async Task Given_ACreationTransactionAtExactlyTheAmsterdamInitcodeCap_When_Validated_Then_ItIsNotRejectedForInitcodeSize()
        {
            var result = await CreateAsync(Amsterdam, new byte[AmsterdamMaxInitcodeSize]);

            Assert.True(result.Success, result.Error);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task Given_ACreationTransactionJustOverThePragueInitcodeCap_When_ValidatedAtPrague_Then_ItIsRejected()
        {
            var result = await CreateAsync(Prague, new byte[PragueMaxInitcodeSize + 1]);

            Assert.False(result.Success);
            Assert.Equal(InitcodeSizeExceeded, result.Error);
        }

        [Fact]
        public async Task Given_ACreationTransactionJustOverThePragueInitcodeCap_When_ValidatedAtAmsterdam_Then_ItIsNotRejectedForInitcodeSize()
        {
            var result = await CreateAsync(Amsterdam, new byte[PragueMaxInitcodeSize + 1]);

            Assert.True(result.Success, result.Error);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Given_TheAmsterdamCeilings_When_Compared_Then_InitcodeIsTwiceCodeSize()
        {
            Assert.Equal(AmsterdamMaxCodeSize * 2, AmsterdamMaxInitcodeSize);
            Assert.Equal(AmsterdamMaxCodeSize, Amsterdam.MaxCodeSize);
            Assert.Equal(AmsterdamMaxInitcodeSize, Amsterdam.MaxInitcodeSize);
        }
    }
}
