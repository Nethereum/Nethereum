using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class BaseFeeBundleSelectionTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private static readonly BigInteger BaseFee = 20_000_000_000;

        [Fact]
        public async Task BelowBaseFeeOp_IsSkippedFromBundle_ButStaysPending()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry(
                "0x1111111111111111111111111111111111111111", "0xaa",
                maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 10_000_000_000));
            await mempool.AddAsync(CreateEntry(
                "0x2222222222222222222222222222222222222222", "0xbb",
                maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 30_000_000_000));

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor);

            await service.ExecuteBundleAsync(minBaseFee: BaseFee);

            var built = Assert.Single(executor.BuiltBundles);
            var bundledHash = Assert.Single(built.Entries).UserOpHash;
            Assert.Equal("0xbb", bundledHash);

            var skipped = await mempool.GetAsync("0xaa");
            Assert.NotNull(skipped);
            Assert.Equal(MempoolEntryState.Pending, skipped!.State);
        }

        [Fact]
        public async Task WithoutFloor_BothOps_AreBundled()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry(
                "0x1111111111111111111111111111111111111111", "0xaa",
                maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 10_000_000_000));
            await mempool.AddAsync(CreateEntry(
                "0x2222222222222222222222222222222222222222", "0xbb",
                maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 30_000_000_000));

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor);

            await service.ExecuteBundleAsync(minBaseFee: null);

            var built = Assert.Single(executor.BuiltBundles);
            Assert.Equal(2, built.Entries.Length);
        }

        [Fact]
        public void PackedGasFees_RoundTrip_MatchesProductionUnpack()
        {
            var op = new PackedUserOperation { GasFees = PackGasFees(1_000_000_000, 30_000_000_000) };
            var (priority, maxFee) = op.UnpackGasFees();
            Assert.Equal((BigInteger)1_000_000_000, priority);
            Assert.Equal((BigInteger)30_000_000_000, maxFee);
        }

        private static BundlerService CreateService(IUserOpMempool mempool, IBundleExecutor executor)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = "0x3333333333333333333333333333333333333333",
                AutoBundleIntervalMs = 0
            };

            var web3 = new Web3.Web3(new StubZeroNonceRpcClient());
            return new BundlerService(web3, config, mempool, validator: null, executor);
        }

        private static MempoolEntry CreateEntry(
            string sender, string hash, BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = maxPriorityFeePerGas,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = 0,
                    GasFees = PackGasFees(maxPriorityFeePerGas, maxFeePerGas)
                }
            };
        }

        private static byte[] PackGasFees(BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            var packed = new byte[32];
            var priorityBytes = maxPriorityFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            var maxBytes = maxFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            Array.Copy(priorityBytes, 0, packed, 16 - priorityBytes.Length, priorityBytes.Length);
            Array.Copy(maxBytes, 0, packed, 32 - maxBytes.Length, maxBytes.Length);
            return packed;
        }

        private sealed class CapturingBundleExecutor : IBundleExecutor
        {
            public List<Bundle> BuiltBundles { get; } = new();

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
            {
                var bundle = new Bundle
                {
                    Entries = entries,
                    EntryPoint = EntryPoint,
                    Beneficiary = "0x3333333333333333333333333333333333333333"
                };
                BuiltBundles.Add(bundle);
                return Task.FromResult(bundle);
            }

            public Task<string> SubmitAsync(Bundle bundle) =>
                Task.FromResult("0x" + new string('1', 64));

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) =>
                throw new NotSupportedException();

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) =>
                throw new NotSupportedException();
        }
    }
}
