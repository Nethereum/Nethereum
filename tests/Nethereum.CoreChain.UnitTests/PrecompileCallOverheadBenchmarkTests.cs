using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
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
    public class PrecompileCallOverheadBenchmarkTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string ContractAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const long BlockGasLimit = 30_000_000;
        private const int WarmupIterations = 2_000;
        private const int MeasuredIterations = 40_000;
        private const string BnAddInput =
            "0000000000000000000000000000000000000000000000000000000000000001" +
            "0000000000000000000000000000000000000000000000000000000000000002" +
            "030644e72e131a029b85045b68181585d97816a916871ca8d3c208c16d87cfd3" +
            "15ed738c0e0a7c92e7845f96b2ae9c0a68a6a449e3538fc7ff3ebf7a5a18a2c4";
        private static readonly BigInteger ChainId = 1337;

        private readonly ITestOutputHelper _output;

        public PrecompileCallOverheadBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_a_loop_of_STATICCALLs_to_the_bn128_add_precompile_When_executed_Then_it_reports_time_and_allocation_per_call()
        {
            await ReportPerCall("bn128_add (0x06)", precompile: 0x06);
        }

        [Fact]
        public async Task Given_a_loop_of_STATICCALLs_to_the_identity_precompile_When_executed_Then_it_reports_time_and_allocation_per_call()
        {
            await ReportPerCall("identity (0x04)", precompile: 0x04);
        }

        private async Task ReportPerCall(string label, byte precompile)
        {
            var processor = await ProcessorWithLoopContract(precompile);
            var nonce = 0;
            await RunLoop(processor, nonce++, WarmupIterations);
            await RunLoop(processor, nonce++, WarmupIterations);

            GC.Collect();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var gasUsed = await RunLoop(processor, nonce, MeasuredIterations);
            stopwatch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            var microseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / MeasuredIterations;
            var megagasPerSecond = (double)gasUsed / stopwatch.Elapsed.TotalSeconds / 1_000_000.0;
            _output.WriteLine($"{label}: {microseconds:F2} us/call, {allocated / MeasuredIterations} B/call, {megagasPerSecond:F2} Mgas/s, gas {gasUsed}");
        }

        private static async Task<TransactionProcessor> ProcessorWithLoopContract(byte precompile)
        {
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = BigInteger.Pow(10, 20), Nonce = 0 });
            var code = LoopCode(precompile);
            var codeHash = new Sha3Keccack().CalculateHash(code);
            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(ContractAddress, new Account { Balance = 0, Nonce = 1, CodeHash = codeHash });

            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = BlockGasLimit, BaseFee = 0, Hardfork = "Prague" };
            return new TransactionProcessor(
                stateStore, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());
        }

        private static async Task<long> RunLoop(TransactionProcessor processor, int nonce, int iterations)
        {
            var transaction = TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, ContractAddress, 0, nonce, 1, BlockGasLimit,
                iterations.ToString("x64")));
            var result = await processor.ExecuteTransactionAsync(
                transaction, BlockContext(), txIndex: 0, cumulativeGasUsed: 0);
            Assert.True(result.Success, result.RevertReason);
            return (long)result.GasUsed;
        }

        private static BlockContext BlockContext() => new BlockContext
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            Coinbase = SenderAddress,
            GasLimit = BlockGasLimit,
            BaseFee = 0,
            ChainId = ChainId,
            Difficulty = 0,
            PrevRandao = new byte[32],
            ExcessBlobGas = 0
        };

        private static byte[] LoopCode(byte precompile)
        {
            var loop = "6040" + "6080" + "6080" + "6000" + "60" + precompile.ToString("x2") + "5a" + "fa" + "50"
                + "600190" + "03" + "80" + "600a" + "57" + "00";
            var setup = "6080" + "6021" + "6000" + "39" + "6000" + "35" + "5b";
            return (setup + loop + BnAddInput).HexToByteArray();
        }
    }
}
