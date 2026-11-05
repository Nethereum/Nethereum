using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevChain.UnitTests
{
    [Trait("Category", "Benchmark")]
    [Trait("Category", "LargeFixture")]
    public class DevChainSenderRecoveryBenchmarkTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        private const int Transfers = 1000;
        private const int Repetitions = 5;

        private readonly ITestOutputHelper _output;

        public DevChainSenderRecoveryBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static async Task<double> MineTransfersAsync(List<ISignedTransaction> transactions)
        {
            var config = DevChainConfig.Default;
            using var bundle = InMemoryChainStoreBundle.Open();
            var verifier = new TransactionVerificationAndRecoveryImp();
            var processor = new TransactionProcessor(bundle.State, bundle.Blocks, config, verifier, config.GetHardforkConfig());
            using var manager = new BlockManager(
                bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, processor, verifier, config);
            await manager.InitializeAsync();
            await bundle.State.SaveAccountAsync(SenderAddress, new Account { Balance = BigInteger.Pow(10, 24), Nonce = 0 });
            foreach (var transaction in transactions) manager.AddPendingTransaction(transaction);

            var watch = Stopwatch.StartNew();
            await manager.MineBlockAsync();
            return watch.Elapsed.TotalMilliseconds;
        }

        [Fact]
        public async Task Given_ABlockOfEtherTransfers_When_TheDevChainMinesIt_Then_TheMineTimeIsReported()
        {
            var config = DevChainConfig.Default;
            var transactions = Enumerable.Range(0, Transfers)
                .Select(nonce => TransactionFactory.CreateTransaction(
                    new LegacyTransactionSigner().SignTransaction(
                        PrivateKey.HexToByteArray(), config.ChainId, RecipientAddress, 100, nonce, 2_000_000_000, 21_000, "")))
                .ToList();

            var timings = new List<double>();
            for (var i = 0; i < Repetitions; i++) timings.Add(await MineTransfersAsync(transactions));
            timings.Sort();

            _output.WriteLine(
                $"DevChain composition: {Transfers} transfers, mine min {timings[0]:F1} ms, median {timings[Repetitions / 2]:F1} ms");
        }
    }
}
