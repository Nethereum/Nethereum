using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    /// <summary>
    /// AMS-2780-16 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>:
    /// a transaction that runs out of gas on a TOP-FRAME charge — before any
    /// EVM frame is built — still commits <c>succeeded=False</c> to its OWN
    /// receipt.
    ///
    /// <para>
    /// EIP-658 fixes what a receipt's first field means: <i>"a status code
    /// with value 1 (success) or 0 (failure)"</i>, per transaction. The
    /// failure is deliberately SANDWICHED between two successful transfers,
    /// because an executor that derives a receipt's status from block-level
    /// state left behind by the previous transaction produces the right
    /// answer whenever the failure stands alone, and the wrong one only
    /// here. nimbus-eth1 shipped exactly that and rejected finalized
    /// canonical blocks on glamsterdam-devnet-7 with a receiptRoot mismatch.
    /// </para>
    ///
    /// <para>
    /// The middle transaction transfers value to an address that is not
    /// alive, which at Amsterdam owes EIP-8037's NEW_ACCOUNT state charge
    /// before dispatch. Its gas limit clears the intrinsic cost and cannot
    /// reach that charge, so it forfeits its whole budget without a
    /// <c>Program</c> ever existing.
    /// </para>
    /// </summary>
    public class TopFrameChargeReceiptStatusTests
    {
        private const string AliveRecipient = "0x00000000000000000000000000000000000000a1";
        private const string NotAliveRecipient = "0x00000000000000000000000000000000000000d0";

        private const long TopFrameChargeGasLimit = 50_000;
        private const long AmpleGasLimit = 100_000;

        private static BlockWitnessTransaction Transfer(string to, ulong nonce, long gasLimit) =>
            TestTransactionHelper.CreateSignedTransfer(
                to,
                value: new EvmUInt256(1UL),
                nonce: new EvmUInt256(nonce),
                gasPrice: new EvmUInt256(1UL),
                gasLimit: new EvmUInt256((ulong)gasLimit));

        private static WitnessAccount Account(string address, EvmUInt256 balance, ulong nonce) =>
            new WitnessAccount
            {
                Address = address,
                Balance = balance,
                Nonce = nonce,
                Code = new byte[0],
                Storage = new List<WitnessStorageSlot>()
            };

        private static BlockExecutionResult ExecuteSandwich()
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction>
                {
                    Transfer(AliveRecipient, nonce: 0, gasLimit: AmpleGasLimit),
                    Transfer(NotAliveRecipient, nonce: 1, gasLimit: TopFrameChargeGasLimit),
                    Transfer(AliveRecipient, nonce: 2, gasLimit: AmpleGasLimit)
                },
                Accounts = new List<WitnessAccount>
                {
                    Account(sender, new EvmUInt256(1_000_000_000_000_000_000UL), 0),
                    Account(AliveRecipient, EvmUInt256.Zero, 1)
                }
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static bool Succeeded(Receipt receipt) =>
            receipt.PostStateOrStatus != null && receipt.PostStateOrStatus.Length == 1 && receipt.PostStateOrStatus[0] == 1;

        [Fact]
        [Trait("Category", "EIP2780")]
        public void Given_ATopFrameOogTransactionBetweenTwoSuccessfulOnes_When_ReceiptsAreBuilt_Then_TheMiddleStatusIsItsOwnFailure()
        {
            var result = ExecuteSandwich();

            Assert.Equal(3, result.Receipts.Count);
            Assert.True(Succeeded(result.Receipts[0]), "the first transfer must succeed");
            Assert.False(Succeeded(result.Receipts[1]),
                "the middle receipt carries the previous transaction's status instead of its own");
            Assert.True(Succeeded(result.Receipts[2]),
                "the third transfer must succeed — a status carried forward from the failure is the same defect in the other direction");
        }

        [Fact]
        [Trait("Category", "EIP2780")]
        public void Given_ATopFrameOogTransaction_When_Settled_Then_ItForfeitsItsWholeBudgetWithoutDispatching()
        {
            var result = ExecuteSandwich();

            var failed = result.TxResults[1];
            Assert.False(failed.Success);
            Assert.Equal(TopFrameChargeGasLimit, failed.GasUsed);
            Assert.Null(failed.ProgramResult);
            Assert.Empty(failed.Logs);
        }
    }
}
