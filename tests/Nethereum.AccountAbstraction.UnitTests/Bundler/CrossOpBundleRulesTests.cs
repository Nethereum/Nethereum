using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class CrossOpBundleRulesTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string SenderA = "0x1111111111111111111111111111111111111111";
        private const string SenderB = "0x2222222222222222222222222222222222222222";
        private const string ThirdContract = "0x4444444444444444444444444444444444444444";


        [Fact]
        public async Task OpAccessingAnotherSendersStorage_IsExcludedFromBundle_ButStaysPending()
        {
            var mempool = new InMemoryUserOpMempool();

            var opA = CreateEntry(SenderA, "0xaa", accessedStorage: new[] { SenderA });
            var opB = CreateEntry(SenderB, "0xbb", accessedStorage: new[] { SenderB, SenderA });
            await mempool.AddAsync(opA);
            await mempool.AddAsync(opB);

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor);

            await service.ExecuteBundleAsync(minBaseFee: null);

            var built = Assert.Single(executor.BuiltBundles);
            var bundledHash = Assert.Single(built.Entries).UserOpHash;
            Assert.Equal("0xaa", bundledHash);

            var skipped = await mempool.GetAsync("0xbb");
            Assert.NotNull(skipped);
            Assert.Equal(MempoolEntryState.Pending, skipped!.State);
        }

        [Fact]
        public async Task OpsTouchingOnlyOwnSenderStorage_AreBothBundled()
        {
            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(CreateEntry(SenderA, "0xaa", accessedStorage: new[] { SenderA, ThirdContract }));
            await mempool.AddAsync(CreateEntry(SenderB, "0xbb", accessedStorage: new[] { SenderB, ThirdContract }));

            var executor = new CapturingBundleExecutor();
            var service = CreateService(mempool, executor);

            await service.ExecuteBundleAsync(minBaseFee: null);

            var built = Assert.Single(executor.BuiltBundles);
            Assert.Equal(2, built.Entries.Length);
        }


        [Fact]
        public void IncomingPaymaster_ThatIsAKnownSender_IsRejected()
        {
            var pending = new[] { CreateEntry(DoubleRole, "0x01") };

            var message = MultipleRolesRule.Detect(
                sender: SenderB, paymaster: DoubleRole, factory: null, pending);

            Assert.NotNull(message);
            Assert.Contains("is used as a sender entity in another UserOperation currently in mempool", message);
        }

        [Fact]
        public void IncomingSender_ThatIsAKnownEntity_IsRejected()
        {
            var pending = new[] { CreateEntry(SenderB, "0x02", paymaster: DoubleRole) };

            var message = MultipleRolesRule.Detect(
                sender: DoubleRole, paymaster: null, factory: null, pending);

            Assert.NotNull(message);
            Assert.Contains("is used as a different entity in another UserOperation currently in mempool", message);
        }

        [Fact]
        public void IncomingFactory_ThatIsAKnownSender_IsRejected()
        {
            var pending = new[] { CreateEntry(DoubleRole, "0x01") };

            var message = MultipleRolesRule.Detect(
                sender: SenderB, paymaster: null, factory: DoubleRole, pending);

            Assert.NotNull(message);
            Assert.Contains("is used as a sender entity in another UserOperation currently in mempool", message);
        }

        [Fact]
        public void DistinctRoles_AreAccepted()
        {
            var pending = new[] { CreateEntry(SenderA, "0xaa", paymaster: ThirdContract) };

            var message = MultipleRolesRule.Detect(
                sender: SenderB, paymaster: null, factory: null, pending);

            Assert.Null(message);
        }

        private const string DoubleRole = "0x5555555555555555555555555555555555555555";

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
            string sender, string hash, string[]? accessedStorage = null, string? paymaster = null)
        {
            var entry = new MempoolEntry
            {
                UserOpHash = hash,
                EntryPoint = EntryPoint,
                Priority = 1_000_000_000,
                Paymaster = paymaster,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = 0,
                    GasFees = PackGasFees(1_000_000_000, 30_000_000_000)
                }
            };

            if (accessedStorage != null)
            {
                foreach (var address in accessedStorage)
                {
                    entry.AccessedStorageAddresses.Add(address);
                }
            }

            return entry;
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
