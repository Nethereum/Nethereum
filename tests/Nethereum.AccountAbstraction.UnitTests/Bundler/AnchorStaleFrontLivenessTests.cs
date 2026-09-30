using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class AnchorStaleFrontLivenessTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Beneficiary = "0x3333333333333333333333333333333333333333";
        private const string Sender = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task StaleFrontBehindOnChainNonce_IsRemoved_SoNextAttemptSurfacesSuccessor()
        {
            var mempool = new InMemoryUserOpMempool();

            await mempool.AddAsync(CreateEntry("0xstale", nonce: 5));
            await mempool.AddAsync(CreateEntry("0xvalid", nonce: 7));

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor, onChainNonce: 7);

            var firstResult = await service.ExecuteBundleAsync();

            Assert.Null(firstResult);
            Assert.Empty(executor.BuiltBundles);
            Assert.Null(await mempool.GetAsync("0xstale"));
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync("0xvalid"))!.State);

            var secondResult = await service.ExecuteBundleAsync();

            Assert.NotNull(secondResult);
            Assert.True(secondResult!.Success);
            var built = Assert.Single(executor.BuiltBundles);
            var bundledHash = Assert.Single(built.Entries).UserOpHash;
            Assert.Equal("0xvalid", bundledHash);
        }

        private static BundlerService CreateService(IUserOpMempool mempool, IBundleExecutor executor, BigInteger onChainNonce)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = Beneficiary,
                AutoBundleIntervalMs = 0
            };

            var web3 = new Web3.Web3(new StubConfigurableNonceRpcClient(onChainNonce));
            return new BundlerService(web3, config, mempool, validator: null, executor);
        }

        private static MempoolEntry CreateEntry(string hash, int nonce)
        {
            return new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = 1_000_000_000,
                UserOperation = new PackedUserOperation
                {
                    Sender = Sender,
                    Nonce = nonce,
                    GasFees = PackGasFees(1_000_000_000, 30_000_000_000)
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
                var bundle = new Bundle { Entries = entries, EntryPoint = EntryPoint, Beneficiary = Beneficiary };
                BuiltBundles.Add(bundle);
                return Task.FromResult(bundle);
            }

            public Task<string> SubmitAsync(Bundle bundle) => Task.FromResult("0x" + new string('1', 64));

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                Task.FromResult(new BundleExecutionResult { Success = true });

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();
            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }
    }
}
