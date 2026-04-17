using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BinaryIncrementalStateRootTests
    {
        private readonly ITestOutputHelper _output;

        public BinaryIncrementalStateRootTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task FullBuild_MatchesWitnessPathCalculator_Blake3()
        {
            await AssertIncrementalMatchesWitnessPath(new Blake3HashProvider());
        }

        [Fact]
        public async Task FullBuild_MatchesWitnessPathCalculator_Poseidon()
        {
            await AssertIncrementalMatchesWitnessPath(new PoseidonPairHashProvider());
        }

        [Fact]
        public async Task FullBuild_MatchesWitnessPathCalculator_Sha256()
        {
            await AssertIncrementalMatchesWitnessPath(new Sha256HashProvider());
        }

        [Fact]
        public async Task IncrementalUpdate_ProducesSameRootAsFullRebuild()
        {
            var hashProvider = new Blake3HashProvider();
            var stateStore = new InMemoryStateStore();
            var address = "0x1000000000000000000000000000000000000000";

            await SaveAccountToStore(stateStore, address, new EvmUInt256(1000), 1, new byte[] { 0x60, 0x00 },
                new Dictionary<BigInteger, byte[]> { { 0, new byte[] { 0x42 } } });

            var calc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var root1 = await calc.ComputeStateRootAsync();
            Assert.NotEqual(new byte[32], root1);

            await stateStore.SaveStorageAsync(address, 1, new byte[] { 0xAB });
            await stateStore.ClearDirtyTrackingAsync();
            await stateStore.SaveStorageAsync(address, 1, new byte[] { 0xAB });

            var rootIncremental = await calc.ComputeStateRootAsync();

            var calc2 = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var rootFull = await calc2.ComputeFullStateRootAsync();

            _output.WriteLine($"Incremental: {rootIncremental.ToHex(true)}");
            _output.WriteLine($"Full:        {rootFull.ToHex(true)}");
            Assert.Equal(rootFull, rootIncremental);
        }

        [Fact]
        public async Task IncrementalUpdate_BalanceChange_DifferentRoot()
        {
            var hashProvider = new Blake3HashProvider();
            var stateStore = new InMemoryStateStore();
            var address = "0x1000000000000000000000000000000000000000";

            await SaveAccountToStore(stateStore, address, new EvmUInt256(1000), 0, new byte[0], null);

            var calc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var root1 = await calc.ComputeStateRootAsync();

            var account = await stateStore.GetAccountAsync(address);
            account.Balance = new EvmUInt256(2000);
            await stateStore.SaveAccountAsync(address, account);

            var root2 = await calc.ComputeStateRootAsync();

            Assert.NotEqual(root1, root2);
            _output.WriteLine($"Before balance change: {root1.ToHex(true)}");
            _output.WriteLine($"After balance change:  {root2.ToHex(true)}");
        }

        [Fact]
        public async Task EmptyState_ReturnsZeroRoot()
        {
            var stateStore = new InMemoryStateStore();
            var calc = new BinaryIncrementalStateRootCalculator(stateStore);
            var root = await calc.ComputeStateRootAsync();
            Assert.Equal(new byte[32], root);
        }

        [Fact]
        public async Task Reset_AllowsReinitialisation()
        {
            var stateStore = new InMemoryStateStore();
            var address = "0x1000000000000000000000000000000000000000";

            await SaveAccountToStore(stateStore, address, new EvmUInt256(1000), 0, new byte[0], null);

            var calc = new BinaryIncrementalStateRootCalculator(stateStore);
            var root1 = await calc.ComputeStateRootAsync();

            calc.Reset();
            var root2 = await calc.ComputeStateRootAsync();

            Assert.Equal(root1, root2);
        }

        private async Task AssertIncrementalMatchesWitnessPath(IHashProvider hashProvider)
        {
            var address = "0x1000000000000000000000000000000000000000";
            var balance = new EvmUInt256(1000000000000000000UL);
            ulong nonce = 5;
            var code = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 };
            var storage = new Dictionary<BigInteger, byte[]>
            {
                { 0, new byte[] { 0x42 } },
                { 1, new byte[] { 0x01, 0x02, 0x03 } },
                { 100, new byte[] { 0xFF } }
            };

            var stateStore = new InMemoryStateStore();
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            await stateStore.SaveCodeAsync(codeHash, code);
            await SaveAccountToStore(stateStore, address, balance, nonce, code, storage);

            var incrementalCalc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var incrementalRoot = await incrementalCalc.ComputeFullStateRootAsync();

            var witnessCalc = new BinaryStateRootCalculator(hashProvider);
            var executionState = new ExecutionStateService(
                new InMemoryStateReader(new Dictionary<string, EVM.BlockchainState.AccountState>()));
            var acct = new AccountExecutionState { Address = address };
            acct.Balance.SetInitialChainBalance(balance);
            acct.Nonce = (EvmUInt256)nonce;
            acct.Code = code;
            foreach (var kvp in storage)
                acct.Storage[EvmUInt256BigIntegerExtensions.FromBigInteger(kvp.Key)] = kvp.Value;
            executionState.AccountsState[address] = acct;

            var witnessRoot = witnessCalc.ComputeStateRoot(executionState);

            _output.WriteLine($"Incremental ({hashProvider.GetType().Name}): {incrementalRoot.ToHex(true)}");
            _output.WriteLine($"Witness     ({hashProvider.GetType().Name}): {witnessRoot.ToHex(true)}");

            Assert.Equal(witnessRoot, incrementalRoot);
        }

        private static async Task SaveAccountToStore(InMemoryStateStore store, string address,
            EvmUInt256 balance, ulong nonce, byte[] code, Dictionary<BigInteger, byte[]> storage)
        {
            var codeHash = code != null && code.Length > 0
                ? Sha3Keccack.Current.CalculateHash(code)
                : Sha3Keccack.Current.CalculateHash(new byte[0]);

            if (code != null && code.Length > 0)
                await store.SaveCodeAsync(codeHash, code);

            var account = new Account
            {
                Balance = balance,
                Nonce = (EvmUInt256)nonce,
                CodeHash = codeHash
            };
            await store.SaveAccountAsync(address, account);

            if (storage != null)
            {
                foreach (var kvp in storage)
                    await store.SaveStorageAsync(address, kvp.Key, kvp.Value);
            }

            await store.ClearDirtyTrackingAsync();
        }
    }
}
