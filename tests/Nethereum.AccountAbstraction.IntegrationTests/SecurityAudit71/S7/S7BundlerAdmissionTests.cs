using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7BundlerAdmissionTests
    {
        private const int MaxBundleGas = 15_000_000;

        private readonly DevChainBundlerFixture _fixture;

        public S7BundlerAdmissionTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

        private BundlerService NewBundler(IReputationService? reputation = null, IUserOpMempool? mempool = null)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxBundleGas = MaxBundleGas,
                MaxVerificationGas = 10_000_000,
                MaxMempoolSize = 5000,
                AutoBundleIntervalMs = 0,
                ReputationDecayIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = false,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };

            return new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, null, null, reputation);
        }

        private async Task<PackedUserOperation> SignedOpAsync(
            string sender, EthECKey key, BigInteger nonce, byte[]? initCode, byte[]? callData, BigInteger callGas)
        {
            var op = new UserOperation
            {
                Sender = sender,
                Nonce = nonce,
                InitCode = initCode ?? Array.Empty<byte>(),
                CallData = callData ?? Array.Empty<byte>(),
                CallGasLimit = callGas,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 0,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var sizing = UserOperationBuilder.PackUserOperation(op);
            sizing.Signature = new byte[65];
            var minPvg = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                sizing, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed);
            op.PreVerificationGas = minPvg + 20_000;
            op.Signature = new byte[65];

            return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(op, key);
        }

        private async Task<(string Sender, EthECKey Key, byte[] InitCode)> CounterfactualAccountAsync(decimal funding)
        {
            var key = EthECKey.GenerateKey();
            var owner = key.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();
            var sender = await _fixture.AccountFactoryService.GetAddressQueryAsync(owner, salt);
            await _fixture.FundAccountAsync(sender, funding);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(owner, salt);
            return (sender, key, initCode);
        }

        [Fact]
        [Trait("Rule", "S7-1")]
        [Trait("Erc", "ERC-4337 gas limits")]
        public async Task SendUserOperation_WhenTotalGasExceedsWhatAnyBundleCanCarry_IsRejectedAtAdmission()
        {
            using var bundler = NewBundler();
            var (account, key) = await _fixture.CreateFundedAccountAsync((ulong)Random.Shared.NextInt64(), 1m);
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await SignedOpAsync(account, key, nonce, null, null, callGas: MaxBundleGas + 5_000_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));

            Assert.Empty(await bundler.GetPendingUserOperationsAsync());
        }

        [Fact]
        [Trait("Rule", "S7-2")]
        [Trait("Erc", "ERC-4337 input limits")]
        public async Task SendUserOperation_WhenCallDataExceedsTheNodeTransactionSizeLimit_IsRejectedAtAdmission()
        {
            using var bundler = NewBundler();
            var (account, key) = await _fixture.CreateFundedAccountAsync((ulong)Random.Shared.NextInt64(), 1m);
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var callData = new byte[200 * 1024];
            Array.Fill(callData, (byte)0xAB);
            var op = await SignedOpAsync(account, key, nonce, null, callData, callGas: 100_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));

            Assert.Empty(await bundler.GetPendingUserOperationsAsync());
        }

        [Fact]
        [Trait("Rule", "S7-6")]
        [Trait("Erc", "ERC-4337 receipts")]
        public async Task GetUserOperationReceipt_WithCaseVariantsOfOneHash_ReturnsOneCachedReceipt()
        {
            using var bundler = NewBundler();
            var (account, key) = await _fixture.CreateFundedAccountAsync((ulong)Random.Shared.NextInt64(), 1m);
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await SignedOpAsync(account, key, nonce, null, null, callGas: 100_000);
            var hash = await bundler.SendUserOperationAsync(op, EntryPoint);
            var bundle = await bundler.ExecuteBundleAsync();
            Assert.True(bundle != null && bundle.Success, bundle?.Error);

            var lower = await bundler.GetUserOperationReceiptAsync(hash);
            var upper = await bundler.GetUserOperationReceiptAsync("0x" + hash.Substring(2).ToUpperInvariant());

            Assert.NotNull(lower);
            Assert.Same(lower, upper);
        }

        [Fact]
        [Trait("Rule", "S7-7")]
        [Trait("Erc", "ERC-7562 reputation")]
        [Trait("SpecBehaviour", "ERC-7562-opsSeen")]
        public async Task OpsSeen_WhenAnOpIsPrunedWithoutInclusion_IsNotGivenBackToTheFactory_BecauseErc7562OnlyDecrementsOnReplacementOrFailure()
        {
            var reputation = new InMemoryReputationService();
            var mempool = new InMemoryUserOpMempool(100, TimeSpan.Zero);
            using var bundler = NewBundler(reputation, mempool);
            var (sender, key, initCode) = await CounterfactualAccountAsync(1m);
            var op = await SignedOpAsync(sender, key, BigInteger.Zero, initCode, null, callGas: 100_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);
            var factory = _fixture.AccountFactoryService.ContractHandler.ContractAddress;
            Assert.Equal(1, (await bundler.GetReputationAsync(factory)).OpsSeen);

            await Task.Delay(25);
            var pruned = await mempool.PruneAsync();

            Assert.Equal(1, pruned);
            Assert.Equal(1, (await bundler.GetReputationAsync(factory)).OpsSeen);
        }

        [Fact]
        [Trait("Rule", "S7-8")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task HonestFactory_WhenAttackerParksOpsThatNeverGetBundled_StillServesAnHonestUser()
        {
            var reputation = new InMemoryReputationService();
            using var bundler = NewBundler(reputation);
            var attackerOps = 130;
            for (var i = 0; i < attackerOps; i++)
            {
                var (sender, key, initCode) = await CounterfactualAccountAsync(1m);
                var op = await SignedOpAsync(sender, key, BigInteger.Zero, initCode, null, callGas: MaxBundleGas + 5_000_000);
                try
                {
                    await bundler.SendUserOperationAsync(op, EntryPoint);
                }
                catch (BundlerRpcException)
                {
                    break;
                }
            }

            var (honest, honestKey, honestInit) = await CounterfactualAccountAsync(1m);
            var honestOp = await SignedOpAsync(honest, honestKey, BigInteger.Zero, honestInit, null, callGas: 100_000);

            var hash = await bundler.SendUserOperationAsync(honestOp, EntryPoint);

            Assert.False(string.IsNullOrEmpty(hash));
        }

        [Fact]
        [Trait("Rule", "S7-9")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task SendUserOperation_WhenOneUnstakedFactoryAlreadyHasTenOpsInTheMempool_TheNextIsRejected()
        {
            var reputation = new InMemoryReputationService();
            using var bundler = NewBundler(reputation);
            for (var i = 0; i < 10; i++)
            {
                var (sender, key, initCode) = await CounterfactualAccountAsync(1m);
                var op = await SignedOpAsync(sender, key, BigInteger.Zero, initCode, null, callGas: 100_000);
                await bundler.SendUserOperationAsync(op, EntryPoint);
            }

            var (extraSender, extraKey, extraInit) = await CounterfactualAccountAsync(1m);
            var extra = await SignedOpAsync(extraSender, extraKey, BigInteger.Zero, extraInit, null, callGas: 100_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(extra, EntryPoint));
        }
    }
}
