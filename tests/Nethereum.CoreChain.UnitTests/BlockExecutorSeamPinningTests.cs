using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutorSeamPinningTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string CoinbaseAddress = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
        private const string FreshRecipientAddress = "0x00000000000000000000000000000000000ba1a5";
        private const long BlockGasLimit = 30_000_000;

        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static ISignedTransaction ValueTransfer(string to = RecipientAddress) =>
            TransactionFactory.CreateTransaction(Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, to, 100, 0, 0, 21_000, ""));

        private sealed class Stack
        {
            public InMemoryStateStore StateStore = null!;
            public BlockExecutor Engine = null!;
            public BlockHeader Header = null!;
        }

        private static async Task<Stack> BuildAsync(HardforkName fork, byte[]? parentBeaconBlockRoot = null)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(RecipientAddress, new Account { Balance = 1_000, Nonce = 0 });
            await stateStore.SaveAccountAsync(CoinbaseAddress, new Account { Balance = 1_000, Nonce = 0 });

            var config = new ChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                BaseFee = 0,
                Hardfork = fork.ToString()
            };
            var trieNodeStore = new InMemoryContentNodeStore();

            return new Stack
            {
                StateStore = stateStore,
                Engine = new BlockExecutor(
                    stateStore,
                    new InMemoryBlockStore(),
                    new FixedChainActivations(fork),
                    chainConfigFactory: _ => config,
                    hardforkConfigFactory: _ => config.GetHardforkConfig(),
                    stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                    rewardPolicy: NoRewardPolicy.Instance,
                    trieNodeStore: trieNodeStore),
                Header = new BlockHeader
                {
                    BlockNumber = 1,
                    Timestamp = 1_700_000_000,
                    GasLimit = BlockGasLimit,
                    BaseFee = 0,
                    Coinbase = CoinbaseAddress,
                    ParentHash = new byte[32],
                    ParentBeaconBlockRoot = parentBeaconBlockRoot
                }
            };
        }

        private static async Task<BlockExecutionResult> ExecuteAsync(
            Stack stack, BlockExecutionOptions options, params TxEntry[] txs)
        {
            var result = await stack.Engine.ExecuteAsync(
                stack.Header, txs, uncles: null, withdrawals: null, options: options);
            Assert.Null(result.Exception);
            return result;
        }


        [Fact]
        public async Task Given_APreAmsterdamFork_When_ABlockIsExecuted_Then_NoBlockAccessListIsBuilt()
        {
            var stack = await BuildAsync(HardforkName.Prague);

            var result = await ExecuteAsync(stack, new BlockExecutionOptions(), new TxEntry(ValueTransfer()));

            Assert.Null(result.BlockAccessList);
            Assert.False(result.BlockAccessListGasLimitExceeded);
        }

        [Fact]
        public async Task Given_AnAmsterdamFork_When_ABlockIsExecuted_Then_ABlockAccessListIsBuilt()
        {
            var stack = await BuildAsync(HardforkName.Amsterdam);

            var result = await ExecuteAsync(stack, new BlockExecutionOptions(), new TxEntry(ValueTransfer()));

            Assert.NotNull(result.BlockAccessList);
            Assert.NotEmpty(result.BlockAccessList);
        }


        [Fact]
        public async Task Given_ABlockWithNoReceipts_When_Executed_Then_TheBlockBloomIsNullNotAllZero()
        {
            var stack = await BuildAsync(HardforkName.Prague);

            var result = await ExecuteAsync(stack, new BlockExecutionOptions());

            Assert.Empty(result.Receipts);
            Assert.Null(result.BlockBloom);
        }

        [Fact]
        public async Task Given_ABlockWithOneReceipt_When_Executed_Then_TheBlockBloomIsTheCombinedBloom()
        {
            var stack = await BuildAsync(HardforkName.Prague);

            var result = await ExecuteAsync(stack, new BlockExecutionOptions(), new TxEntry(ValueTransfer()));

            var receipt = Assert.Single(result.Receipts).Receipt;
            Assert.NotNull(receipt);
            Assert.NotNull(result.BlockBloom);
            Assert.Equal(256, result.BlockBloom!.Length);
            Assert.Equal(receipt!.Bloom, result.BlockBloom);
        }


        private static async Task<BlockWitnessData> CaptureWitnessAsync(HardforkName fork)
        {
            var stack = await BuildAsync(fork, parentBeaconBlockRoot: new byte[32] { 7, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            var result = await ExecuteAsync(
                stack, new BlockExecutionOptions { CaptureWitness = true }, new TxEntry(ValueTransfer()));

            Assert.NotNull(result.WitnessBytes);
            return BinaryBlockWitness.Deserialize(result.WitnessBytes);
        }

        private static bool WitnessCarries(BlockWitnessData witness, string address) =>
            witness.Accounts.Any(a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        [Fact]
        public async Task Given_AWitnessCapturingBlockAtCancun_When_Executed_Then_TheBeaconRootsContractIsPresentInTheWitness()
        {
            var witness = await CaptureWitnessAsync(HardforkName.Cancun);

            Assert.True(WitnessCarries(witness, Eip4788Constants.BeaconRootsAddress),
                "the EIP-4788 predeploy must enter the witness, or the guest's replay silently no-ops on it");
        }

        [Fact]
        public async Task Given_AWitnessCapturingBlockBeforeCancun_When_Executed_Then_TheBeaconRootsContractIsAbsentFromTheWitness()
        {
            var witness = await CaptureWitnessAsync(HardforkName.Shanghai);

            Assert.False(WitnessCarries(witness, Eip4788Constants.BeaconRootsAddress));
        }


        private static async Task<BigInteger> BalanceOfAsync(InMemoryStateStore store, string address)
        {
            var account = await store.GetAccountAsync(address);
            return account == null
                ? BigInteger.Zero
                : new BigInteger(account.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
        }

        [Fact]
        public async Task Given_ReadOnlyExecution_When_ABlockIsExecuted_Then_NoWriteReachesTheUnderlyingStateStore()
        {
            var stack = await BuildAsync(HardforkName.Prague);
            Assert.Null(await stack.StateStore.GetAccountAsync(FreshRecipientAddress));

            var result = await ExecuteAsync(
                stack,
                new BlockExecutionOptions { ReadOnly = true, CaptureWitness = true },
                new TxEntry(ValueTransfer(FreshRecipientAddress)));

            Assert.Single(result.Receipts);
            Assert.Null(await stack.StateStore.GetAccountAsync(FreshRecipientAddress));
            Assert.Null(result.PostStateRoot);
        }

        [Fact]
        public async Task Given_NormalExecution_When_ABlockIsExecuted_Then_TheUnderlyingStateStoreIsUpdated()
        {
            var stack = await BuildAsync(HardforkName.Prague);
            Assert.Null(await stack.StateStore.GetAccountAsync(FreshRecipientAddress));

            var result = await ExecuteAsync(
                stack, new BlockExecutionOptions(), new TxEntry(ValueTransfer(FreshRecipientAddress)));

            Assert.Single(result.Receipts);
            Assert.Equal(100, await BalanceOfAsync(stack.StateStore, FreshRecipientAddress));
            Assert.NotNull(result.PostStateRoot);
        }
    }
}
