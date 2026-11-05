using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.UnitTests
{
    [Trait("Category", "Benchmark")]
    [Trait("Category", "LargeFixture")]
    public class PerTransactionCostBenchmarkTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string ContractAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const string BalanceTarget = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        private const string DelegateAddress = "0x976EA74026E726554dB657fA54763abd0C3a0aa9";
        private const long BlockGasLimit = 3_000_000_000;
        private const long GasPrice = 1_000_000_000;
        private const int Transactions = 400;
        private const int Warmups = 12;
        private const int Measured = 9;
        private static readonly BigInteger ChainId = 1337;

        private static readonly LegacyTransactionSigner LegacySigner = new();
        private static readonly Transaction7702Signer SetCodeSigner = new();
        private static readonly Authorisation7702Signer AuthorisationSigner = new();

        private readonly ITestOutputHelper _output;

        public PerTransactionCostBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private sealed record Scenario(
            string Name, HardforkName Fork, Func<InMemoryStateStore, Task> Seed, Func<List<ISignedTransaction>> Build);

        [Fact]
        public async Task Given_a_block_of_ether_transfers_to_existing_accounts_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(EtherTransfers("existing accounts", HardforkName.Prague, seeded: true));
        }

        [Fact]
        public async Task Given_a_block_of_ether_transfers_to_new_accounts_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(EtherTransfers("new accounts", HardforkName.Prague, seeded: false));
        }

        [Fact]
        public async Task Given_a_block_of_ether_transfers_to_existing_accounts_under_Amsterdam_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(EtherTransfers("existing accounts", HardforkName.Amsterdam, seeded: true));
        }

        [Fact]
        public async Task Given_a_block_of_ether_transfers_to_precompiles_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            var build = () => Enumerable.Range(0, Transactions)
                .Select(i => Legacy(i, PrecompileAddress(1 + i % 4), 1, 100_000, ""))
                .ToList();
            await Report(new Scenario("ether to precompile", HardforkName.Prague, _ => Task.CompletedTask, build));
        }

        [Fact]
        public async Task Given_a_block_of_EIP7702_authorisation_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(new Scenario("eip7702 auth tx", HardforkName.Prague, _ => Task.CompletedTask, SetCodeTransactions));
        }

        [Fact]
        public async Task Given_a_block_of_cold_SLOAD_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("SLOAD cold", "6000" + "54" + "50" + "00"));
        }

        [Fact]
        public async Task Given_a_block_of_warm_SLOAD_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("SLOAD warm", "6000" + "54" + "50" + "6000" + "54" + "50" + "00"));
        }

        [Fact]
        public async Task Given_a_block_of_cold_SSTORE_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("SSTORE cold", "6001" + "6000" + "55" + "00"));
        }

        [Fact]
        public async Task Given_a_block_of_warm_SSTORE_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("SSTORE warm", "6001" + "6000" + "55" + "6002" + "6000" + "55" + "00"));
        }

        [Fact]
        public async Task Given_a_block_of_BALANCE_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("BALANCE", "73" + BalanceTarget.Substring(2) + "31" + "50" + "00"));
        }

        [Fact]
        public async Task Given_a_block_of_SELFBALANCE_transactions_When_executed_Then_it_reports_time_and_allocation_per_transaction()
        {
            await Report(ContractCalls("SELFBALANCE", "47" + "50" + "00"));
        }

        private static Scenario EtherTransfers(string label, HardforkName fork, bool seeded) =>
            new Scenario(
                "ether transfer to " + label, fork,
                async state =>
                {
                    if (!seeded) return;
                    for (int i = 0; i < Transactions; i++)
                        await state.SaveAccountAsync(Recipient(i), new Account { Balance = 1, Nonce = 0 });
                },
                () => Enumerable.Range(0, Transactions).Select(i => Legacy(i, Recipient(i), 1, 21_000, "")).ToList());

        private static Scenario ContractCalls(string name, string codeHex) =>
            new Scenario(
                name, HardforkName.Prague,
                async state =>
                {
                    var code = codeHex.HexToByteArray();
                    var codeHash = new Sha3Keccack().CalculateHash(code);
                    await state.SaveCodeAsync(codeHash, code);
                    await state.SaveAccountAsync(ContractAddress, new Account { Balance = 1, Nonce = 1, CodeHash = codeHash });
                    await state.SaveAccountAsync(BalanceTarget, new Account { Balance = 5, Nonce = 0 });
                },
                () => Enumerable.Range(0, Transactions).Select(i => Legacy(i, ContractAddress, 0, 100_000, "")).ToList());

        private static string Recipient(int index) => "0x" + (0x100000 + index).ToString("x40");

        private static string PrecompileAddress(int index) => "0x" + index.ToString("x40");

        private static ISignedTransaction Legacy(int nonce, string to, long value, long gasLimit, string data) =>
            TransactionFactory.CreateTransaction(LegacySigner.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, to, value, nonce, GasPrice, gasLimit, data));

        private static EthECKey AuthorityKey(int index)
        {
            var raw = new byte[32];
            var value = index + 1;
            raw[28] = (byte)(value >> 24); raw[29] = (byte)(value >> 16); raw[30] = (byte)(value >> 8); raw[31] = (byte)value;
            return new EthECKey(raw, true);
        }

        private static List<ISignedTransaction> SetCodeTransactions()
        {
            var transactions = new List<ISignedTransaction>();
            for (int i = 0; i < Transactions; i++)
            {
                var authority = AuthorityKey(i);
                var authorisation = AuthorisationSigner.SignAuthorisation(
                    authority, new Authorisation7702 { ChainId = ChainId, Address = DelegateAddress, Nonce = 0 });
                var tx = new Transaction7702(
                    ChainId, i, GasPrice, GasPrice, 100_000, Recipient(i), 0, "", null, new List<Authorisation7702Signed> { authorisation });
                transactions.Add(TransactionFactory.CreateTransaction(SetCodeSigner.SignTransaction(PrivateKey, tx)));
            }
            return transactions;
        }

        private async Task Report(Scenario scenario)
        {
            var transactions = scenario.Build();
            var block = await Measure(scenario, transactions, BlockRun);
            var loop = await Measure(scenario, transactions, TransactionLoopRun);
            _output.WriteLine(
                $"{scenario.Name,-34} {scenario.Fork,-10} tx-loop {loop.Microseconds,7:F2} us/tx (gc pause {loop.PauseMicroseconds,5:F2}) {loop.Bytes,6} B/tx   whole-block {block.Microseconds,7:F2} us/tx (gc pause {block.PauseMicroseconds,5:F2}) {block.Bytes,6} B/tx   gas/tx {loop.Gas}");
        }

        private readonly record struct Sample(double Microseconds, long Bytes, long Gas, double PauseMicroseconds);

        private delegate Task<long> Run(Scenario scenario, InMemoryChainStoreBundle bundle, List<ISignedTransaction> transactions);

        private static async Task<Sample> Measure(Scenario scenario, List<ISignedTransaction> transactions, Run run)
        {
            var times = new List<double>();
            var pauses = new List<double>();
            long allocated = 0;
            long gas = 0;
            for (int pass = 0; pass < Warmups + Measured; pass++)
            {
                using var bundle = InMemoryChainStoreBundle.Open();
                await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, scenario.Fork);
                await bundle.State.SaveAccountAsync(SenderAddress, new Account { Balance = BigInteger.Pow(10, 30), Nonce = 0 });
                await scenario.Seed((InMemoryStateStore)bundle.State);
                GC.Collect();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var pauseBefore = GC.GetTotalPauseDuration();
                var stopwatch = Stopwatch.StartNew();
                gas = await run(scenario, bundle, transactions);
                stopwatch.Stop();
                var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                var pause = GC.GetTotalPauseDuration() - pauseBefore;
                if (pass < Warmups) continue;
                times.Add(stopwatch.Elapsed.TotalMilliseconds * 1000.0 / transactions.Count);
                pauses.Add(pause.TotalMilliseconds * 1000.0 / transactions.Count);
                allocated = (allocatedAfter - allocatedBefore) / transactions.Count;
            }
            times.Sort();
            pauses.Sort();
            return new Sample(times[0], allocated, gas / transactions.Count, pauses[pauses.Count / 2]);
        }

        private static ChainConfig ConfigFor(Scenario scenario) => new ChainConfig
        {
            ChainId = ChainId,
            BlockGasLimit = BlockGasLimit,
            BaseFee = 7,
            Hardfork = scenario.Fork.ToString()
        };

        private static async Task<long> BlockRun(Scenario scenario, InMemoryChainStoreBundle bundle, List<ISignedTransaction> transactions)
        {
            var config = ConfigFor(scenario);
            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(scenario.Fork), _ => config, _ => config.GetHardforkConfig(),
                new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });
            var header = ShapedHeaders.Create(scenario.Fork, 1, 1_700_000_000, BlockGasLimit, 0, 7);
            var entries = transactions.Select(t => new TxEntry(t, SenderAddress)).ToList();
            var result = await stack.Executor.ExecuteAsync(
                header, entries, uncles: null, withdrawals: null, new BlockExecutionOptions(), CancellationToken.None);
            Assert.Null(result.Exception);
            Assert.Equal(transactions.Count, result.Receipts.Count);
            Assert.All(result.Receipts, receipt => Assert.True(receipt.Success, receipt.RevertReason));
            return (long)result.GasUsed;
        }

        private static async Task<long> TransactionLoopRun(Scenario scenario, InMemoryChainStoreBundle bundle, List<ISignedTransaction> transactions)
        {
            var config = ConfigFor(scenario);
            var processor = new TransactionProcessor(
                bundle.State, bundle.Blocks, config, new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
            var balRecorder = scenario.Fork >= HardforkName.Amsterdam ? new BlockAccessListRecorder(bundle.State) : null;
            var blockContext = new BlockContext
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                Coinbase = "0x0000000000000000000000000000000000009999",
                GasLimit = BlockGasLimit,
                BaseFee = 7,
                ChainId = ChainId,
                Difficulty = 0,
                PrevRandao = new byte[32],
                ExcessBlobGas = 0
            };
            long cumulative = 0;
            for (int i = 0; i < transactions.Count; i++)
            {
                var result = await processor.ExecuteTransactionAsync(
                    transactions[i], blockContext, i, cumulative, SenderAddress, null, false, balRecorder, (ulong)(i + 1));
                Assert.True(result.Success, result.RevertReason);
                cumulative = (long)result.CumulativeGasUsed;
            }
            return cumulative;
        }
    }
}
