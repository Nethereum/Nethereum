using Nethereum.EVM.Core.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    /// <summary>
    /// AMS-7928-09 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>.
    ///
    /// <para>EIP-7928 §Scope and Inclusion lists among the addresses the block
    /// access list MUST include: <i>"Precompiled contracts when called or
    /// accessed"</i>.</para>
    ///
    /// <para>EIP-7928 §Edge Cases (Normative), <i>Precompiled contracts</i>:
    /// <i>"Precompiles MUST be included when accessed. If a precompile receives
    /// value, it is recorded with a balance change. Otherwise, it is included
    /// with empty change lists."</i></para>
    ///
    /// <para><see cref="BlockAccessListCollector"/> has no precompile concept:
    /// a precompile reaches it as an ordinary address, through the same
    /// <c>RecordAccountRead</c> every other account uses. That is the whole
    /// claim under test, and it is why the risk here is DOUBLE recording rather
    /// than under-recording — a port that copies another client's explicit
    /// precompile hook would add a second entry on top of the generic one.
    /// Each case therefore asserts a count, not merely presence.</para>
    ///
    /// <para>Driven through <see cref="BlockExecutor"/> so the recorder is
    /// wired the way production wires it. Calling the collector directly would
    /// supply the very intermediate the rule is about and could not go red if
    /// the engine stopped recording.</para>
    /// </summary>
    public class PrecompileAccessListTests
    {
        private const string IdentityPrecompile = "0x0000000000000000000000000000000000000004";
        private const string ModExpPrecompile = "0x0000000000000000000000000000000000000005";
        private const string CallerContractAddress = "0x00000000000000000000000000000000000000c1";

        private static byte[] CallIdentityPrecompileCode(byte value) => ByteUtil.Merge(
            new byte[] { 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, value },
            new byte[] { 0x73 },
            IdentityPrecompile.HexToByteArray(),
            new byte[] { 0x5A, 0xF1, 0x50, 0x00 });

        private static BlockExecutionResult ExecuteBlock(
            byte[] callerCode, EvmUInt256 callerBalance, int transactionCount)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            return BlockExecutor.Execute(
                AmsterdamBlockWith(
                    CallsToTheCallerContract(transactionCount),
                    FundedSender(sender),
                    CallerContract(callerCode, callerBalance)),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static List<BlockWitnessTransaction> CallsToTheCallerContract(int count) =>
            Enumerable.Range(0, count)
                .Select(nonce => TestTransactionHelper.CreateSignedContractCall(
                    CallerContractAddress,
                    data: new byte[0],
                    value: EvmUInt256.Zero,
                    nonce: new EvmUInt256((ulong)nonce),
                    gasPrice: new EvmUInt256(1UL),
                    gasLimit: new EvmUInt256(1_000_000UL)))
                .ToList();

        private static WitnessAccount FundedSender(string address) =>
            new WitnessAccount
            {
                Address = address,
                Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                Nonce = 0,
                Code = new byte[0],
                Storage = new List<WitnessStorageSlot>()
            };

        private static WitnessAccount CallerContract(byte[] code, EvmUInt256 balance) =>
            new WitnessAccount
            {
                Address = CallerContractAddress,
                Balance = balance,
                Nonce = 1,
                Code = code,
                Storage = new List<WitnessStorageSlot>()
            };

        private static BlockWitnessData AmsterdamBlockWith(
            List<BlockWitnessTransaction> transactions, params WitnessAccount[] accounts) =>
            AmsterdamBlockCarrying(transactions, accounts).AddRequestPredeploys();

        private static BlockWitnessData AmsterdamBlockCarrying(
            List<BlockWitnessTransaction> transactions, params WitnessAccount[] accounts) =>
            new BlockWitnessData
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
                Transactions = transactions,
                Accounts = accounts.ToList()
            };

        private static List<AccountChanges> Entries(BlockExecutionResult result, string address) =>
            result.BlockAccessList
                .Where(a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase))
                .ToList();

        private static void AssertEveryTransactionSucceeded(BlockExecutionResult result)
        {
            foreach (var tx in result.TxResults)
                Assert.True(tx.Success, tx.Error);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APrecompileCalledWithoutValue_When_TheBlockAccessListIsBuilt_Then_ItIsPresentWithEmptyChangeLists()
        {
            var result = ExecuteBlock(CallIdentityPrecompileCode(0x00), EvmUInt256.Zero, transactionCount: 1);
            AssertEveryTransactionSucceeded(result);

            var precompile = Assert.Single(Entries(result, IdentityPrecompile));
            Assert.Empty(precompile.BalanceChanges);
            Assert.Empty(precompile.NonceChanges);
            Assert.Empty(precompile.CodeChanges);
            Assert.Empty(precompile.StorageChanges);
            Assert.Empty(precompile.StorageReads);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APrecompileThatReceivesValue_When_TheBlockAccessListIsBuilt_Then_ItsBalanceChangeIsRecorded()
        {
            var result = ExecuteBlock(CallIdentityPrecompileCode(0x01), new EvmUInt256(1UL), transactionCount: 1);
            AssertEveryTransactionSucceeded(result);

            var precompile = Assert.Single(Entries(result, IdentityPrecompile));
            Assert.Equal(new EvmUInt256(1UL), Assert.Single(precompile.BalanceChanges).PostBalance);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APrecompileCalledByTwoTransactionsInOneBlock_When_TheBlockAccessListIsBuilt_Then_ItAppearsExactlyOnce()
        {
            var result = ExecuteBlock(CallIdentityPrecompileCode(0x00), EvmUInt256.Zero, transactionCount: 2);
            AssertEveryTransactionSucceeded(result);

            Assert.Equal(2, result.TxResults.Count);
            Assert.Single(Entries(result, IdentityPrecompile));
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APrecompileNoTransactionCalls_When_TheBlockAccessListIsBuilt_Then_ItIsAbsent()
        {
            var result = ExecuteBlock(CallIdentityPrecompileCode(0x00), EvmUInt256.Zero, transactionCount: 1);
            AssertEveryTransactionSucceeded(result);

            Assert.Single(Entries(result, IdentityPrecompile));
            Assert.Empty(Entries(result, ModExpPrecompile));
        }
    }
}
