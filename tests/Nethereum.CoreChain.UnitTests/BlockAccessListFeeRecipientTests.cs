using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-7928: paying the transaction fee READS the fee recipient's account, so every block
    /// that executes at least one transaction lists the fee recipient — even when the priority
    /// fee is zero and nothing about the account changes.
    ///
    /// <para>Reference: EELS <c>forks/amsterdam/fork.py:1007</c> pays the tip with an
    /// unconditional <c>create_ether(tx_state, block_env.coinbase, transaction_fee)</c>, whose
    /// <c>modify_state</c> → <c>get_account</c> adds the address to
    /// <c>account_reads</c> (<c>state_tracker.py:182</c>); <c>account_reads</c> becomes a
    /// touched entry in <c>block_access_lists.py:692</c>. The corpus states both halves
    /// directly: <c>test_bal_coinbase_zero_tip</c> ("Ensure BAL includes coinbase even when
    /// priority fee is zero") and <c>test_bal_empty_block_no_coinbase</c> ("Coinbase must NOT be
    /// included - receives no fees"), both in
    /// <c>execution-specs/tests/amsterdam/eip7928_block_level_access_lists/test_block_access_lists.py</c>
    /// (lines 1520 and 1547).</para>
    ///
    /// <para>Our fee settlement materialised the recipient
    /// (<c>EVM.Core/TransactionExecutor.cs</c>, <c>CreateOrGetAccountExecutionState(ctx.Coinbase)</c>)
    /// without telling the access recorder, so a zero tip produced no balance change and
    /// therefore no entry at all. That single omission was the sole difference on 479 of the
    /// 501 Amsterdam host-corpus fixtures failing on <c>blockAccessListHash</c>.</para>
    /// </summary>
    public class BlockAccessListFeeRecipientTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string FeeRecipientAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";

        private const long BaseFee = 7;

        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static ISignedTransaction ValueTransfer(BigInteger gasPrice)
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, 0, gasPrice, 21_000, "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private static async Task<BlockExecutionResult> ExecuteAmsterdamBlockAsync(params TxEntry[] txs)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(FeeRecipientAddress, new Account { Balance = 1_000, Nonce = 0 });

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = 30_000_000,
                BaseFee = BaseFee,
                Hardfork = nameof(HardforkName.Amsterdam)
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                GasLimit = 30_000_000,
                BaseFee = BaseFee,
                Coinbase = FeeRecipientAddress,
                ParentHash = new byte[32]
            };

            var result = await engine.ExecuteAsync(
                header, txs, uncles: null, withdrawals: null, options: new BlockExecutionOptions());

            Assert.Null(result.Exception);
            return result;
        }

        private static AccountChanges FeeRecipientEntry(BlockExecutionResult result) =>
            result.BlockAccessList?.FirstOrDefault(
                a => string.Equals(a.Address, FeeRecipientAddress, StringComparison.OrdinalIgnoreCase));

        [Fact]
        public async Task Given_ZeroPriorityFee_When_BlockExecuted_Then_FeeRecipientIsListedAsTouched()
        {
            var result = await ExecuteAmsterdamBlockAsync(new TxEntry(ValueTransfer(gasPrice: BaseFee)));

            var feeRecipient = FeeRecipientEntry(result);

            Assert.NotNull(feeRecipient);
            Assert.Empty(feeRecipient.BalanceChanges);
            Assert.Empty(feeRecipient.NonceChanges);
            Assert.Empty(feeRecipient.CodeChanges);
            Assert.Empty(feeRecipient.StorageChanges);
            Assert.Empty(feeRecipient.StorageReads);
        }

        [Fact]
        public async Task Given_NoTransactions_When_BlockExecuted_Then_FeeRecipientIsAbsentFromTheAccessList()
        {
            var result = await ExecuteAmsterdamBlockAsync();

            Assert.Null(FeeRecipientEntry(result));
        }

        [Fact]
        public async Task Given_NonZeroPriorityFee_When_BlockExecuted_Then_FeeRecipientCarriesABalanceChange()
        {
            var result = await ExecuteAmsterdamBlockAsync(new TxEntry(ValueTransfer(gasPrice: BaseFee + 3)));

            var feeRecipient = FeeRecipientEntry(result);

            Assert.NotNull(feeRecipient);
            var change = Assert.Single(feeRecipient.BalanceChanges);
            Assert.Equal(new EvmUInt256(1_000 + (21_000 * 3)), change.PostBalance);
        }
    }
}
