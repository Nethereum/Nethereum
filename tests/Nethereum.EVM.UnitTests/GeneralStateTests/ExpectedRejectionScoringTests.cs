using System;
using Nethereum.CoreChain;
using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    [Trait("Category", "GeneralStateTests")]
    public class ExpectedRejectionScoringTests
    {
        private const string IntrinsicTooLow = "TransactionException.INTRINSIC_GAS_TOO_LOW";
        private const string BelowFloor = "TransactionException.INTRINSIC_GAS_BELOW_FLOOR_GAS_COST";
        private const string InitCodeTooLarge = "TransactionException.INITCODE_SIZE_EXCEEDED";
        private const string CapExceeded = "TransactionException.GAS_LIMIT_EXCEEDS_MAXIMUM";
        private const string InvalidAccessList = "BlockException.INVALID_BLOCK_ACCESS_LIST";
        private const string SystemContractCallFailed = "BlockException.SYSTEM_CONTRACT_CALL_FAILED";

        private static ObservedRejection Refused(TransactionError reason) =>
            ObservedRejection.FromTransaction(reason);

        private static ObservedRejection BlockFailed(params BlockValidityCheck[] checks) =>
            ObservedRejection.FromImport(checks, TransactionError.None, null);


        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_HostFault_When_FixtureExpectsRejection_Then_NotAccepted()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, new EvmHostException("state provider unreachable"), out var detail);

            Assert.False(satisfied);
            Assert.Contains("host/system fault", detail);
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_OutOfMemory_When_FixtureExpectsRejection_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, new OutOfMemoryException(), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_EngineCrash_When_FixtureExpectsRejection_Then_NotAccepted()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, new NullReferenceException("Object reference not set"), out var detail);

            Assert.False(satisfied);
            Assert.Contains("unexpected NullReferenceException", detail);
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ARefusalNamingNoReason_When_FixtureExpectsRejection_Then_NotAccepted()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, ObservedRejection.FromImport(null, TransactionError.None, null), out var detail);

            Assert.False(satisfied);
            Assert.Contains("without naming a reason", detail);
        }


        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_IntrinsicRejection_When_FixtureExpectsIt_Then_Accepted()
        {
            Assert.True(ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, Refused(TransactionError.IntrinsicGasTooLow), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_IntrinsicRejection_When_FixtureExpectsBelowFloor_Then_Accepted()
        {
            Assert.True(ExpectedRejectionMatcher.Satisfies(
                BelowFloor, Refused(TransactionError.IntrinsicGasTooLow), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ACapRejection_When_FixtureExpectsAnIntrinsicLabel_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, Refused(TransactionError.GasLimitExceedsMaximum), out _));
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                BelowFloor, Refused(TransactionError.GasLimitExceedsMaximum), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_AnIntrinsicRejection_When_FixtureExpectsTheCapLabel_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                CapExceeded, Refused(TransactionError.IntrinsicGasTooLow), out _));

            Assert.True(ExpectedRejectionMatcher.Satisfies(
                CapExceeded, Refused(TransactionError.GasLimitExceedsMaximum), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_InitCodeRejection_When_FixtureExpectsGasLabel_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                IntrinsicTooLow, Refused(TransactionError.InitcodeSizeExceeded), out _));
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                InitCodeTooLarge, Refused(TransactionError.IntrinsicGasTooLow), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ATypedValidationThrow_When_ItsReasonAgrees_Then_Accepted()
        {
            Assert.True(ExpectedRejectionMatcher.Satisfies(
                "TransactionException.SENDER_NOT_EOA",
                new TransactionValidationException(TransactionError.SenderNotEOA, "sender has code"),
                out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ATypedValidationThrow_When_ItsReasonDisagrees_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                "TransactionException.SENDER_NOT_EOA",
                new TransactionValidationException(TransactionError.NonceIsMax, "nonce is max"),
                out _));
        }


        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ABlockAccessListHashFailure_When_FixtureExpectsAnInvalidAccessList_Then_Accepted()
        {
            Assert.True(ExpectedRejectionMatcher.Satisfies(
                InvalidAccessList, BlockFailed(BlockValidityCheck.BlockAccessListHash), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_AStateRootFailure_When_FixtureExpectsAnInvalidAccessList_Then_NotAccepted()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                InvalidAccessList, BlockFailed(BlockValidityCheck.StateRoot, BlockValidityCheck.GasUsed), out var detail);

            Assert.False(satisfied);
            Assert.Contains("stateRoot", detail);
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ASystemCallFailure_When_FixtureExpectsIt_Then_Accepted()
        {
            Assert.True(ExpectedRejectionMatcher.Satisfies(
                SystemContractCallFailed,
                new SystemCallFailedException("0x00000000219ab540356cbb839cbe05303d7705fa", "out of gas"),
                out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_AnEngineCrashInTheSystemCall_When_FixtureExpectsASystemCallFailure_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                SystemContractCallFailed,
                new InvalidOperationException("EIP-7685 system call to 0x... failed: out of gas"),
                out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ASystemCallFailure_When_FixtureExpectsADifferentReason_Then_NotAccepted()
        {
            Assert.False(ExpectedRejectionMatcher.Satisfies(
                InvalidAccessList,
                new SystemCallFailedException("0x0000f90827f1c53a10cb7a02335b175320002935", "reverted"),
                out _));
        }


        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ACompoundExpectation_When_EitherAlternativeIsObserved_Then_Accepted()
        {
            const string compound = "BlockException.GAS_USED_OVERFLOW|TransactionException.GAS_ALLOWANCE_EXCEEDED";

            Assert.True(ExpectedRejectionMatcher.Satisfies(
                compound, Refused(TransactionError.GasAllowanceExceeded), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ACompoundExpectation_When_NeitherAlternativeIsObserved_Then_NotAccepted()
        {
            const string compound = "BlockException.GAS_USED_OVERFLOW|TransactionException.GAS_ALLOWANCE_EXCEEDED";

            Assert.False(ExpectedRejectionMatcher.Satisfies(
                compound, Refused(TransactionError.InsufficientBalance), out _));
        }


        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_AnUnmappedLabel_When_SomethingWasRejected_Then_NotAccepted()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                "TransactionException.SOME_FUTURE_LABEL", Refused(TransactionError.IntrinsicGasTooLow), out var detail);

            Assert.False(satisfied);
            Assert.Contains("UNMAPPED", detail);
            Assert.Contains("SOME_FUTURE_LABEL", detail);
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_ARuleWeDoNotImplement_When_SomethingWasRejected_Then_NotAcceptedAndSaidSo()
        {
            var satisfied = ExpectedRejectionMatcher.Satisfies(
                "TransactionException.GASLIMIT_PRICE_PRODUCT_OVERFLOW",
                BlockFailed(BlockValidityCheck.StateRoot),
                out var detail);

            Assert.False(satisfied);
            Assert.DoesNotContain("UNMAPPED", detail);
            Assert.Contains("no rule implemented", detail);
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_NoExpectException_When_Scored_Then_ItIsAProgrammingError()
        {
            Assert.Throws<ArgumentException>(() =>
                ExpectedRejectionMatcher.Satisfies("", Refused(TransactionError.IntrinsicGasTooLow), out _));
        }

        [Fact]
        [Trait("Category", "HarnessHonesty")]
        public void Given_TheAmsterdamCorpusVocabulary_Then_EveryLabelIsKnownToTheMatcher()
        {
            var corpus = new[]
            {
                "TransactionException.INTRINSIC_GAS_TOO_LOW",
                "TransactionException.INTRINSIC_GAS_BELOW_FLOOR_GAS_COST",
                "BlockException.INVALID_BLOCK_ACCESS_LIST",
                "TransactionException.GAS_ALLOWANCE_EXCEEDED",
                "BlockException.GAS_USED_OVERFLOW|TransactionException.GAS_ALLOWANCE_EXCEEDED",
                "BlockException.SYSTEM_CONTRACT_CALL_FAILED",
                "BlockException.BLOCK_ACCESS_LIST_GAS_LIMIT_EXCEEDED",
                "TransactionException.INITCODE_SIZE_EXCEEDED",
                "BlockException.INCORRECT_BLOCK_FORMAT",
                "TransactionException.INSUFFICIENT_ACCOUNT_FUNDS",
                "BlockException.INVALID_BAL_HASH|BlockException.INVALID_BLOCK_HASH",
                "BlockException.GAS_USED_OVERFLOW"
            };

            foreach (var expectException in corpus)
            {
                ExpectedRejectionMatcher.Satisfies(
                    expectException, Refused(TransactionError.AddressCollision), out var detail);

                Assert.DoesNotContain("UNMAPPED", detail);
            }
        }

        private static string EngineDecodeFailure(LegacyTransactionField field) =>
            "transaction 0 does not decode: " + new TransactionFieldFormatException(field, "empty where a 20-byte address is required").Message;

        [Theory]
        [InlineData("TransactionException.TYPE_3_TX_CONTRACT_CREATION")]
        [InlineData("TransactionException.TYPE_3_TX_CONTRACT_CREATION|TransactionException.RLP_STRUCTURES_ENCODING")]
        [InlineData("TransactionException.TYPE_4_TX_CONTRACT_CREATION")]
        [InlineData("TransactionException.TR_BLOBCREATE")]
        public void Given_EngineSaysRecipientMissingDecodeFailure_When_FixtureExpectsContractCreation_Then_Matched(string expectException)
        {
            var verdict = ExpectedRejectionMatcher.MatchEngineInvalidReason(
                expectException, EngineDecodeFailure(LegacyTransactionField.ReceiveAddress), out _);

            Assert.Equal(ExpectedRejectionMatcher.EngineReasonVerdict.Matched, verdict);
        }

        [Theory]
        [InlineData("TransactionException.TYPE_3_TX_CONTRACT_CREATION")]
        [InlineData("TransactionException.TYPE_4_TX_CONTRACT_CREATION")]
        [InlineData("TransactionException.TR_BLOBCREATE")]
        public void Given_EngineSaysAnotherFieldDecodeFailure_When_FixtureExpectsContractCreation_Then_Mismatch(string expectException)
        {
            var verdict = ExpectedRejectionMatcher.MatchEngineInvalidReason(
                expectException, EngineDecodeFailure(LegacyTransactionField.Nonce), out _);

            Assert.Equal(ExpectedRejectionMatcher.EngineReasonVerdict.Mismatch, verdict);
        }

        [Theory]
        [InlineData("TransactionException.INSUFFICIENT_ACCOUNT_FUNDS")]
        [InlineData("TransactionException.TYPE_3_TX_ZERO_BLOBS")]
        public void Given_EngineSaysRecipientMissingDecodeFailure_When_FixtureExpectsAnUnrelatedLabel_Then_Mismatch(string expectException)
        {
            var verdict = ExpectedRejectionMatcher.MatchEngineInvalidReason(
                expectException, EngineDecodeFailure(LegacyTransactionField.ReceiveAddress), out _);

            Assert.Equal(ExpectedRejectionMatcher.EngineReasonVerdict.Mismatch, verdict);
        }

        [Fact]
        public void Given_EngineSaysAnyTransactionDecodeFailure_When_FixtureExpectsRlpStructuresEncoding_Then_Matched()
        {
            var verdict = ExpectedRejectionMatcher.MatchEngineInvalidReason(
                "TransactionException.RLP_STRUCTURES_ENCODING", EngineDecodeFailure(LegacyTransactionField.Nonce), out _);

            Assert.Equal(ExpectedRejectionMatcher.EngineReasonVerdict.Matched, verdict);
        }

        [Theory]
        [InlineData("TransactionException.TYPE_3_TX_CONTRACT_CREATION", true)]
        [InlineData("TransactionException.TYPE_4_TX_CONTRACT_CREATION", true)]
        [InlineData("TransactionException.TR_BLOBCREATE", true)]
        [InlineData("TransactionException.INSUFFICIENT_ACCOUNT_FUNDS", false)]
        public void Given_RecipientMissingFault_When_Scored_Then_SameRowsAsTheEngineStatusPath(string expectException, bool expected)
        {
            var fault = new TransactionFieldFormatException(LegacyTransactionField.ReceiveAddress, "empty");

            Assert.Equal(expected, ExpectedRejectionMatcher.Satisfies(expectException, fault, out _));
        }

        [Fact]
        public void Given_AnotherFieldFault_When_FixtureExpectsContractCreation_Then_NotAccepted()
        {
            var fault = new TransactionFieldFormatException(LegacyTransactionField.Nonce, "empty");

            Assert.False(ExpectedRejectionMatcher.Satisfies(
                "TransactionException.TYPE_3_TX_CONTRACT_CREATION", fault, out _));
        }
    }
}
