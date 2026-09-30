using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// AMS-4844-03. EIP-7516: "<c>BLOBBASEFEE</c> returns the result of the
    /// <c>get_blob_gasprice(header) -&gt; int</c> function as defined in EIP-4844 §Gas
    /// accounting" — the block's excess blob gas is its only input.
    ///
    /// <para>The executor used to seed the value at 1 and overwrite it only for a type-3
    /// transaction, so every ordinary transaction read the EIP-4844 floor whatever the block
    /// carried. No fixture in the blockchain corpus reaches it: the Amsterdam corpus ships no
    /// blob module at all, and the state-test runner sets <c>ProgramContext.BlobBaseFee</c>
    /// from the fork rule itself rather than through
    /// <see cref="TransactionExecutor"/>. These tests are the evidence for the rule.</para>
    /// </summary>
    public class BlobBaseFeeIsABlockPropertyTests
    {
        private const string SenderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ReporterAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly byte[] ReportBlobBaseFee = "4A5F5260205FF3".HexToByteArray();

        private const ulong ExcessBlobGasWellAboveTheFloor = 50_000_000;

        private static readonly HardforkConfig Shanghai =
            HardforkConfig.Shanghai.WithPrecompiles(DefaultPrecompileRegistries.BerlinBase());
        private static readonly HardforkConfig Cancun =
            HardforkConfig.Cancun.WithPrecompiles(DefaultPrecompileRegistries.CancunBase());
        private static readonly HardforkConfig Prague =
            HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase());
        private static readonly HardforkConfig Amsterdam =
            HardforkConfig.Amsterdam.WithPrecompiles(DefaultPrecompileRegistries.OsakaBase());

        private static TransactionExecutionContext CallingTheReporter(ulong excessBlobGas)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000), Nonce = 0 },
                [ReporterAddress] = new AccountState { Code = ReportBlobBaseFee }
            };

            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = SenderAddress,
                To = ReporterAddress,
                Data = new byte[0],
                GasLimit = new EvmUInt256(1_000_000),
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ExcessBlobGas = new EvmUInt256(excessBlobGas),
                ExecutionState = new ExecutionStateService(new InMemoryStateReader(accounts))
            };
        }

        private static TransactionExecutionContext ABlobTransactionCallingTheReporter(
            ulong excessBlobGas, EvmUInt256 maxFeePerBlobGas)
        {
            var ctx = CallingTheReporter(excessBlobGas);
            ctx.IsType3Transaction = true;
            ctx.MaxFeePerBlobGas = maxFeePerBlobGas;
            ctx.BlobVersionedHashes = new List<string>
            {
                "0x01" + new string('a', 62)
            };
            return ctx;
        }

        private static EvmUInt256 BlobBaseFeeReportedBy(HardforkConfig fork, TransactionExecutionContext ctx)
        {
            var result = new TransactionExecutor(fork).Execute(ctx);

            Assert.False(result.IsValidationError, $"Unexpected validation error: {result.Error}");
            Assert.True(result.Success, $"Execution failed: {result.Error}");
            return EvmUInt256.FromBigEndian(result.ReturnData);
        }

        [Fact]
        [Trait("Rule", "AMS-4844-03")]
        public void Given_ANonBlobTransactionInABlockWithExcessBlobGas_When_BLOBBASEFEE_Executes_Then_ItReturnsTheBlockBlobBaseFee()
        {
            var expected = AmsterdamBlobGasRule.Instance.CalculateBlobBaseFee(
                new EvmUInt256(ExcessBlobGasWellAboveTheFloor));

            Assert.NotEqual(EvmUInt256.One, expected);
            Assert.Equal(expected, BlobBaseFeeReportedBy(
                Amsterdam, CallingTheReporter(ExcessBlobGasWellAboveTheFloor)));
        }

        [Fact]
        [Trait("Rule", "AMS-4844-03")]
        public void Given_ABlobTransaction_When_BLOBBASEFEE_Executes_Then_ItIsUnchanged()
        {
            var expected = AmsterdamBlobGasRule.Instance.CalculateBlobBaseFee(
                new EvmUInt256(ExcessBlobGasWellAboveTheFloor));

            var ctx = ABlobTransactionCallingTheReporter(ExcessBlobGasWellAboveTheFloor, expected);

            Assert.Equal(expected, BlobBaseFeeReportedBy(Amsterdam, ctx));
        }

        [Fact]
        [Trait("Rule", "AMS-4844-03")]
        public void Given_ABlockWithNoExcessBlobGas_When_BLOBBASEFEE_Executes_Then_ItReturnsOne()
        {
            Assert.Equal(EvmUInt256.One, BlobBaseFeeReportedBy(Amsterdam, CallingTheReporter(0)));
        }

        [Fact]
        [Trait("Rule", "AMS-4844-03")]
        public void Given_TheSameExcessBlobGas_When_BLOBBASEFEE_ExecutesAtEachFork_Then_EachForkPricesItWithItsOwnRule()
        {
            var atCancun = BlobBaseFeeReportedBy(Cancun, CallingTheReporter(ExcessBlobGasWellAboveTheFloor));
            var atPrague = BlobBaseFeeReportedBy(Prague, CallingTheReporter(ExcessBlobGasWellAboveTheFloor));
            var atAmsterdam = BlobBaseFeeReportedBy(Amsterdam, CallingTheReporter(ExcessBlobGasWellAboveTheFloor));

            var excess = new EvmUInt256(ExcessBlobGasWellAboveTheFloor);
            Assert.Equal(Eip4844BlobGasRule.Instance.CalculateBlobBaseFee(excess), atCancun);
            Assert.Equal(Eip7691BlobGasRule.Instance.CalculateBlobBaseFee(excess), atPrague);
            Assert.Equal(AmsterdamBlobGasRule.Instance.CalculateBlobBaseFee(excess), atAmsterdam);

            Assert.NotEqual(atCancun, atPrague);
            Assert.NotEqual(atPrague, atAmsterdam);
        }

        [Fact]
        [Trait("Rule", "AMS-4844-03")]
        public void Given_AForkBeforeCancun_When_BLOBBASEFEE_IsExecuted_Then_TheOpcodeIsNotAvailable()
        {
            var atShanghai = new TransactionExecutor(Shanghai)
                .Execute(CallingTheReporter(ExcessBlobGasWellAboveTheFloor));

            Assert.False(atShanghai.IsValidationError, $"Unexpected validation error: {atShanghai.Error}");
            Assert.False(atShanghai.Success);
            Assert.Equal("execution_error", atShanghai.Error);

            Assert.Equal(
                Eip4844BlobGasRule.Instance.CalculateBlobBaseFee(new EvmUInt256(ExcessBlobGasWellAboveTheFloor)),
                BlobBaseFeeReportedBy(Cancun, CallingTheReporter(ExcessBlobGasWellAboveTheFloor)));
        }
    }
}
