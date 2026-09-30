using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    internal static class SystemCallBlockHarness
    {
        public static readonly byte[] MarkerCode = "600160005500".HexToByteArray();

        public static BlockHeader Header(long blockNumber, long timestamp, byte[] parentBeaconBlockRoot = null) =>
            new BlockHeader
            {
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                GasLimit = 30_000_000,
                Coinbase = "0x0000000000000000000000000000000000000001",
                ParentHash = new byte[32],
                ParentBeaconBlockRoot = parentBeaconBlockRoot
            };

        public static Task DeployMarkerAsync(InMemoryStateStore stateStore, string address) =>
            DeployAsync(stateStore, address, MarkerCode);

        public static async Task DeployAsync(InMemoryStateStore stateStore, string address, byte[] code)
        {
            var codeHash = new Sha3Keccack().CalculateHash(code);
            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address,
                new Account { Balance = EvmUInt256.Zero, Nonce = 0, CodeHash = codeHash });
        }

        public static async Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> ExecuteAsync(
            HardforkName fork, BlockHeader header, Func<InMemoryStateStore, Task> seed = null,
            IReadOnlyList<TxEntry> txs = null)
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, fork);
            if (seed != null) await seed(stateStore);

            var blockStore = new InMemoryBlockStore();
            var config = new ChainConfig
            {
                ChainId = 1,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                Hardfork = fork.ToString()
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var result = await engine.ExecuteAsync(
                header,
                txs: txs ?? Array.Empty<TxEntry>(),
                uncles: null,
                withdrawals: null,
                options: new BlockExecutionOptions());

            return (result, stateStore);
        }

        public static async Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> ExecuteAcceptedAsync(
            HardforkName fork, BlockHeader header, Func<InMemoryStateStore, Task> seed = null)
        {
            var executed = await ExecuteAsync(fork, header, seed);
            Assert.Null(executed.Result.Exception);
            return executed;
        }

        public static async Task<Exception> ExecuteGuestVerdictAsync(
            HardforkName fork, long timestamp, IList<WitnessAccount> accounts,
            bool skipsRequestSystemCalls = false)
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = timestamp,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000001",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ParentBeaconBlockRoot = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = fork },
                Transactions = new List<BlockWitnessTransaction>(),
                Accounts = new List<WitnessAccount>(accounts),
                SkipsRequestSystemCalls = skipsRequestSystemCalls
            };

            try
            {
                await Nethereum.EVM.Execution.BlockExecutor.ExecuteAsync(
                    block, RlpBlockEncodingProvider.Instance, DefaultMainnetHardforkRegistry.Instance);
                return null;
            }
            catch (Exception refusal)
            {
                return refusal;
            }
        }

        public static List<WitnessAccount> WitnessCarryingEveryActivatedPredeploy(
            HardforkName fork, string addressUnderTest, byte[] codeUnderTest)
        {
            var accounts = new List<WitnessAccount>();
            foreach (var predeploy in SystemContractPredeploys.For(fork))
            {
                var isUnderTest = predeploy.Address.IsTheSameAddress(addressUnderTest);
                accounts.Add(WitnessAccountWithCode(
                    predeploy.Address, isUnderTest ? codeUnderTest : predeploy.RuntimeCode));
            }

            return accounts;
        }

        public static WitnessAccount WitnessAccountWithCode(string address, byte[] code) =>
            new WitnessAccount
            {
                Address = address,
                Balance = EvmUInt256.Zero,
                Nonce = 1,
                Code = code,
                Storage = new List<WitnessStorageSlot>()
            };
    }
}
