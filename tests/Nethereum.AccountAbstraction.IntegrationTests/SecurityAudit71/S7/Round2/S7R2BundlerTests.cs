using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round2
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R2BundlerTests
    {
        private const int MaxBundleGas = 15_000_000;

        private readonly DevChainBundlerFixture _fixture;

        public S7R2BundlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

        private BundlerConfig NewConfig(int maxMempoolSize = 5000) => new()
        {
            SupportedEntryPoints = new[] { EntryPoint },
            BeneficiaryAddress = _fixture.BundlerAccount.Address,
            MaxBundleSize = 10,
            MaxBundleGas = MaxBundleGas,
            MaxMempoolSize = maxMempoolSize,
            MaxVerificationGas = 10_000_000,
            AutoBundleIntervalMs = 0,
            ReputationDecayIntervalMs = 0,
            StrictValidation = false,
            SimulateValidation = false,
            UnsafeMode = false,
            ChainId = DevChainBundlerFixture.CHAIN_ID
        };

        private BundlerService NewBundler(IReputationService? reputation = null, BundlerConfig? config = null, IBundleExecutor? executor = null)
        {
            return new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), config ?? NewConfig(), null, null, executor, reputation);
        }

        private async Task<(string Address, EthECKey Key)> AccountAsync()
        {
            return await _fixture.CreateFundedAccountAsync((ulong)Random.Shared.NextInt64(), 1m);
        }

        private async Task<string> DeployPaymasterAsync(decimal depositEth)
        {
            var deployment = new TestPaymasterAcceptAllDeployment { EntryPoint = EntryPoint };
            var service = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Nethereum.Web3.Web3)_fixture.Web3, deployment);
            var address = service.ContractHandler.ContractAddress;
            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction { Account = address, AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(depositEth) });
            return address;
        }

        private async Task<PackedUserOperation> OpAsync(
            string sender,
            EthECKey key,
            BigInteger nonce,
            BigInteger priorityFee,
            BigInteger maxFee,
            string? paymaster = null,
            BigInteger? paymasterVerificationGas = null,
            int callDataLength = 0,
            BigInteger? callGas = null,
            BigInteger? verificationGas = null)
        {
            var op = new UserOperation
            {
                Sender = sender,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = callDataLength == 0 ? Array.Empty<byte>() : Enumerable.Repeat((byte)0xAB, callDataLength).ToArray(),
                CallGasLimit = callGas ?? 100_000,
                VerificationGasLimit = verificationGas ?? 500_000,
                PreVerificationGas = 0,
                MaxFeePerGas = maxFee,
                MaxPriorityFeePerGas = priorityFee
            };

            if (paymaster != null)
            {
                op.Paymaster = paymaster;
                op.PaymasterVerificationGasLimit = paymasterVerificationGas ?? 50_000;
                op.PaymasterPostOpGasLimit = 50_000;
                op.PaymasterData = Array.Empty<byte>();
            }

            var sizing = UserOperationBuilder.PackUserOperation(op);
            sizing.Signature = new byte[65];
            var minPvg = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                sizing, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed);
            op.PreVerificationGas = minPvg + 20_000;
            op.Signature = new byte[65];

            return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(op, key);
        }

        [Fact]
        [Trait("Rule", "S7-R2-1")]
        [Trait("Erc", "ERC-7562 UREP-020")]
        public async Task SendUserOperation_WhenAReplacementMovesAnOpOntoAnUnstakedPaymasterAlreadyAtTenOps_IsRejected()
        {
            var paymaster = await DeployPaymasterAsync(2m);
            using var bundler = NewBundler(new InMemoryReputationService());
            for (var i = 0; i < 10; i++)
            {
                var (account, key) = await AccountAsync();
                var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
                await bundler.SendUserOperationAsync(
                    await OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000, paymaster), EntryPoint);
            }

            var (extraAccount, extraKey) = await AccountAsync();
            var extraNonce = await _fixture.EntryPointService.GetNonceQueryAsync(extraAccount, BigInteger.Zero);
            await Assert.ThrowsAsync<BundlerRpcException>(async () =>
                await bundler.SendUserOperationAsync(
                    await OpAsync(extraAccount, extraKey, extraNonce, 1_000_000_000, 2_000_000_000, paymaster), EntryPoint));

            await bundler.SendUserOperationAsync(
                await OpAsync(extraAccount, extraKey, extraNonce, 1_000_000_000, 2_000_000_000), EntryPoint);
            var replacement = await OpAsync(extraAccount, extraKey, extraNonce, 2_000_000_000, 3_000_000_000, paymaster);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(replacement, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R2-4")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task SendUserOperation_WhenAHigherPayingOpEvictsWhileABundleIsInFlight_TheBundledOpsStayKnownToTheMempool()
        {
            var config = NewConfig(maxMempoolSize: 2);
            var web3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var gated = new GatedExecutor(new BundleExecutor(web3, config));
            using var bundler = new BundlerService(web3, config, null, null, gated, new InMemoryReputationService());
            var hashes = new List<string>();
            foreach (var _ in new[] { 0, 1 })
            {
                var (account, key) = await AccountAsync();
                var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
                hashes.Add(await bundler.SendUserOperationAsync(
                    await OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000), EntryPoint));
            }

            var execution = Task.Run(() => bundler.ExecuteBundleAsync());
            await gated.SubmitEntered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var (richAccount, richKey) = await AccountAsync();
            var richNonce = await _fixture.EntryPointService.GetNonceQueryAsync(richAccount, BigInteger.Zero);
            var richOp = await OpAsync(richAccount, richKey, richNonce, 5_000_000_000, 6_000_000_000);
            Exception? sendFailure = null;
            try
            {
                await bundler.SendUserOperationAsync(richOp, EntryPoint);
            }
            catch (BundlerRpcException ex)
            {
                sendFailure = ex;
            }
            gated.Release.SetResult();
            var result = await execution;

            Assert.True(result != null && result.Success, result?.Error);
            Assert.Null(sendFailure);
            foreach (var hash in hashes)
            {
                var known = await bundler.GetUserOperationByHashAsync(hash);
                Assert.True(known != null && known.TransactionHash != null,
                    $"op {hash} was executed on chain in the in-flight bundle but the mempool no longer knows it");
            }
        }

        [Fact]
        [Trait("Rule", "S7-R2-9")]
        [Trait("Erc", "ERC-7562 LIM-010")]
        public async Task SendUserOperation_WithTheDefaultConfigAndAnOpAboveTheErc7562MaxUserOpSize_IsRejected()
        {
            using var bundler = NewBundler();
            var (account, key) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000, callDataLength: 10_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R2-10")]
        [Trait("Erc", "ERC-7562 LIM-060")]
        public async Task SendUserOperation_WhenVerificationGasLimitAboveMaxVerificationGas_IsRejected_Control()
        {
            using var bundler = NewBundler();
            var (account, key) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000, verificationGas: 12_000_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R2-10")]
        [Trait("Erc", "ERC-7562 LIM-060")]
        public async Task SendUserOperation_WhenPaymasterVerificationGasLimitAboveMaxVerificationGas_IsRejected()
        {
            var paymaster = await DeployPaymasterAsync(2m);
            var config = NewConfig();
            Assert.Equal(500_000, new BundlerConfig().MaxVerificationGas);
            using var bundler = NewBundler(config: config);
            var (account, key) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000, paymaster, paymasterVerificationGas: 12_000_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R2-10")]
        [Trait("Erc", "ERC-7562 LIM-060")]
        public async Task SendUserOperation_WhenPaymasterVerificationGasLimitEqualsTheDefaultMaxVerificationGas_IsRejected()
        {
            var paymaster = await DeployPaymasterAsync(2m);
            var config = NewConfig();
            config.MaxVerificationGas = new BundlerConfig().MaxVerificationGas;
            using var bundler = NewBundler(config: config);
            var (account, key) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await OpAsync(
                account, key, nonce, 1_000_000_000, 2_000_000_000, paymaster,
                paymasterVerificationGas: config.MaxVerificationGas, verificationGas: 400_000);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(op, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R2-4")]
        [Trait("Erc", "ERC-7562 GREP-050")]
        public async Task SendUserOperation_WhenAHigherPayingOpEvictsAFactoryOp_TheFactoryGetsItsOpsSeenBack()
        {
            var reputation = new InMemoryReputationService();
            using var bundler = new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), NewConfig(maxMempoolSize: 1), null, null, null, reputation);
            var key = EthECKey.GenerateKey();
            var owner = key.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();
            var sender = await _fixture.AccountFactoryService.GetAddressQueryAsync(owner, salt);
            await _fixture.FundAccountAsync(sender, 1m);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(owner, salt);
            var factory = _fixture.AccountFactoryService.ContractHandler.ContractAddress;
            var counterfactual = new UserOperation
            {
                Sender = sender,
                Nonce = BigInteger.Zero,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 0,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var sizing = UserOperationBuilder.PackUserOperation(counterfactual);
            sizing.Signature = new byte[65];
            counterfactual.PreVerificationGas = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                sizing, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed) + 20_000;
            counterfactual.Signature = new byte[65];
            await bundler.SendUserOperationAsync(
                await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(counterfactual, key), EntryPoint);
            Assert.Equal(1, (await bundler.GetReputationAsync(factory)).OpsSeen);
            var (account, accountKey) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);

            await bundler.SendUserOperationAsync(
                await OpAsync(account, accountKey, nonce, 5_000_000_000, 6_000_000_000), EntryPoint);

            Assert.Equal(0, (await bundler.GetReputationAsync(factory)).OpsSeen);
        }

        private sealed class GatedExecutor : IBundleExecutor
        {
            private readonly IBundleExecutor _inner;

            public GatedExecutor(IBundleExecutor inner)
            {
                _inner = inner;
            }

            public TaskCompletionSource SubmitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) => _inner.BuildBundleAsync(entries);

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => _inner.ExecuteAsync(bundle);

            public async Task<string> SubmitAsync(Bundle bundle)
            {
                SubmitEntered.TrySetResult();
                await Release.Task;
                return await _inner.SubmitAsync(bundle);
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                _inner.WaitForBundleReceiptAsync(bundle, transactionHash);

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => _inner.EstimateBundleGasAsync(bundle);
        }
    }

    public class S7R2ConfigDefaultTests
    {
        [Fact]
        [Trait("Rule", "S7-R2-11")]
        [Trait("Erc", "ERC-7562 validation scope rules")]
        public void BundlerConfig_WhenConstructedDirectly_EnforcesErc7562ValidationByDefault()
        {
            var config = new BundlerConfig();

            Assert.True(config.EnableERC7562Validation);
        }

        [Fact]
        [Trait("Rule", "S7-R2-11")]
        [Trait("Erc", "ERC-7562 validation scope rules")]
        public void InProcessBundlerHost_StartBundlerWithoutArguments_EnforcesErc7562ValidationByDefault()
        {
            var method = typeof(Nethereum.AccountAbstraction.Bundler.InProcess.InProcessBundlerHost)
                .GetMethod("StartBundler")!;
            var parameter = method.GetParameters().Single(p => p.Name == "enableErc7562Validation");

            Assert.Equal(true, parameter.DefaultValue);
        }
    }
}
