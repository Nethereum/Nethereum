using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.Paymasters;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Xunit;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using RpcUserOperationGasEstimate = Nethereum.RPC.AccountAbstraction.DTOs.UserOperationGasEstimate;
using VerifyingPaymasterDeployment = Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition.VerifyingPaymasterDeployment;
using VerifyingPaymasterDepositFunction = Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition.DepositFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("ERC", "4337")]
    public class AAEstimationHardeningTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public AAEstimationHardeningTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(TestCounterService counterService, string accountAddress, EthECKey accountKey, FactoryConfig factoryConfig)>
            CreateAccountAndCounterAsync(ulong salt, IAccountAbstractionBundlerService bundlerService)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);
            var factoryConfig = new FactoryConfig(_fixture.AccountFactoryService.ContractAddress, ownerAddress, salt);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            return (counter, accountAddress, accountKey, factoryConfig);
        }

        private async Task<string> DeployFundedPaymasterAsync()
        {
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new TestPaymasterAcceptAllDeployment { EntryPoint = _fixture.EntryPointService.ContractAddress });

            await paymasterService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(10) });

            return paymasterService.ContractAddress;
        }

        [Fact]
        public async Task IncompleteBundlerEstimate_ThrowsInsteadOfSigningZeroGasOp()
        {
            var incompleteBundler = new EstimateOverridingBundlerService(
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                estimate => new RpcUserOperationGasEstimate
                {
                    CallGasLimit = null,
                    VerificationGasLimit = estimate.VerificationGasLimit,
                    PreVerificationGas = estimate.PreVerificationGas
                });

            var (counter, _, _, _) = await CreateAccountAndCounterAsync(4201, incompleteBundler);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => counter.CountRequestAndWaitForReceiptAsync());
            Assert.Contains("callGasLimit", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EstimateRequest_WithoutPaymaster_OmitsPaymasterFieldsAndCarriesFees()
        {
            var recordingBundler = new EstimateRecordingBundlerService(
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));

            var (counter, _, _, _) = await CreateAccountAndCounterAsync(4202, recordingBundler);

            var receipt = await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);

            var request = recordingBundler.LastEstimateRequest;
            Assert.NotNull(request);
            Assert.Null(request.Paymaster);
            Assert.Null(request.PaymasterVerificationGasLimit);
            Assert.Null(request.PaymasterPostOpGasLimit);
            Assert.Null(request.PaymasterData);
            Assert.True(request.MaxFeePerGas != null && request.MaxFeePerGas.Value > 0,
                "estimate request must carry real fees (L2 preVerificationGas depends on them)");
        }

        [Fact]
        public async Task PaymasterGasLimits_FromBundlerEstimate_AreAdopted()
        {
            var paymasterVerificationGas = new BigInteger(0x11170);
            var paymasterPostOpGas = new BigInteger(0x5208);

            var bundlerWithPaymasterEstimate = new EstimateOverridingBundlerService(
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                estimate =>
                {
                    estimate.PaymasterVerificationGasLimit = new HexBigInteger(paymasterVerificationGas);
                    estimate.PaymasterPostOpGasLimit = new HexBigInteger(paymasterPostOpGas);
                    return estimate;
                });

            var paymasterAddress = await DeployFundedPaymasterAsync();

            var (counter, _, accountKey, _) = await CreateAccountAndCounterAsync(4203, bundlerWithPaymasterEstimate);
            var handler = (AAContractHandler)counter.ContractHandler;
            handler.WithPaymaster(paymasterAddress, new byte[] { 0xaa });

            var packedOp = await handler.CreateUserOperationAsync(new CountFunction());

            Assert.NotNull(packedOp.PaymasterAndData);
            Assert.True(packedOp.PaymasterAndData.Length >= 52);
            var verificationGas = new BigInteger(packedOp.PaymasterAndData.Skip(20).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var postOpGas = new BigInteger(packedOp.PaymasterAndData.Skip(36).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.Equal(paymasterVerificationGas, verificationGas);
            Assert.Equal(paymasterPostOpGas, postOpGas);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Paymaster op with real bundler estimate returns non-zero limits and executes", Order = 19)]
        [NethereumDocExample(DocSection.AccountAbstraction, "batching-and-paymasters", "WithPaymaster with static data sponsors a real bundler op", Order = 14)]
        public async Task PaymasterOp_RealBundlerEstimate_ReturnsNonZeroLimitsAndExecutes()
        {
            var paymasterAddress = await DeployFundedPaymasterAsync();

            RpcUserOperationGasEstimate estimate = null;
            var capturingBundler = new EstimateOverridingBundlerService(
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                e => { estimate = e; return e; });

            var (counter, _, _, _) = await CreateAccountAndCounterAsync(4207, capturingBundler);
            var handler = (AAContractHandler)counter.ContractHandler;
            handler.WithPaymaster(paymasterAddress, new byte[] { 0xaa });

            var receipt = await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess,
                "a paymaster op estimated by the real bundler must execute successfully");

            Assert.NotNull(estimate);
            Assert.True(estimate.PaymasterVerificationGasLimit != null && estimate.PaymasterVerificationGasLimit.Value > 0,
                "bundler estimate must return a usable paymasterVerificationGasLimit");
            Assert.True(estimate.PaymasterPostOpGasLimit != null && estimate.PaymasterPostOpGasLimit.Value > 0,
                "bundler estimate must return a usable paymasterPostOpGasLimit");
        }

        [Fact]
        public async Task PaymasterDataProvider_ReceivesCompletedUserOperation()
        {
            UserOperation observedOp = null;

            var paymasterAddress = await DeployFundedPaymasterAsync();

            var (counter, _, accountKey, _) = await CreateAccountAndCounterAsync(
                4204, new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));
            var handler = (AAContractHandler)counter.ContractHandler;
            handler.WithPaymaster(new PaymasterConfig(paymasterAddress,
                userOp =>
                {
                    observedOp = new UserOperation
                    {
                        Nonce = userOp.Nonce,
                        CallGasLimit = userOp.CallGasLimit,
                        VerificationGasLimit = userOp.VerificationGasLimit,
                        PreVerificationGas = userOp.PreVerificationGas,
                        MaxFeePerGas = userOp.MaxFeePerGas
                    };
                    return Task.FromResult(new byte[] { 0xbb });
                }));

            await handler.CreateUserOperationAsync(new CountFunction());

            Assert.NotNull(observedOp);
            Assert.NotNull(observedOp.Nonce);
            Assert.True(observedOp.CallGasLimit.HasValue && observedOp.CallGasLimit > 0,
                "paymaster data provider must see the final callGasLimit (it may sign over it)");
            Assert.True(observedOp.VerificationGasLimit.HasValue && observedOp.VerificationGasLimit > 0);
            Assert.True(observedOp.PreVerificationGas.HasValue && observedOp.PreVerificationGas > 0);
            Assert.True(observedOp.MaxFeePerGas.HasValue && observedOp.MaxFeePerGas > 0);
        }

        private async Task<byte[]> SignVerifyingPaymasterDataAsync(
            VerifyingPaymasterService paymaster, UserOperation userOp, EthECKey signingKey)
        {
            var validUntil = (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            const ulong validAfter = 0;

            var packedForHash = UserOperationBuilder.PackUserOperation(userOp);
            packedForHash.InitCode ??= Array.Empty<byte>();
            packedForHash.CallData ??= Array.Empty<byte>();
            packedForHash.PaymasterAndData ??= Array.Empty<byte>();
            packedForHash.Signature ??= Array.Empty<byte>();

            var hash = await paymaster.GetHashQueryAsync(packedForHash, validUntil, validAfter);
            var signature = new EthereumMessageSigner().Sign(hash, signingKey).HexToByteArray();

            return VerifyingPaymasterDataBuilder.Build(validUntil, validAfter, signature);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Verifying paymaster with a PaymasterConfig data provider sponsors a zero-balance account", Order = 20)]
        [NethereumDocExample(DocSection.AccountAbstraction, "batching-and-paymasters", "PaymasterConfig data provider signs a verifying paymaster for a zero-balance account", Order = 15)]
        public async Task Given_a_funded_verifying_paymaster_When_a_zero_balance_account_sends_via_DataProvider_Then_estimation_succeeds_and_the_op_lands_sponsored()
        {
            var signerKey = EthECKey.GenerateKey();

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new VerifyingPaymasterDeployment
                {
                    EntryPoint = _fixture.EntryPointService.ContractAddress,
                    Owner = _fixture.OperatorAccount.Address,
                    Signer = signerKey.GetPublicAddress()
                });

            await paymaster.DepositRequestAndWaitForReceiptAsync(
                new VerifyingPaymasterDepositFunction { AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m) });

            var (counter, accountAddress, _, _) = await CreateAccountAndCounterAsync(
                4208, new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));
            await _fixture.FundAccountAsync(accountAddress, 0m);
            Assert.Equal(BigInteger.Zero, await _fixture.GetBalanceAsync(accountAddress));

            var handler = (AAContractHandler)counter.ContractHandler;

            handler.WithPaymaster(new PaymasterConfig(
                paymaster.ContractAddress,
                userOp => SignVerifyingPaymasterDataAsync(paymaster, userOp, signerKey)));

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
            Assert.True(receipt.UserOpSuccess, $"sponsored op should execute; error: {receipt.RevertReason}");

            Assert.Equal(BigInteger.Zero, await _fixture.GetBalanceAsync(accountAddress));
        }

        [Fact]
        public async Task Given_a_verifying_paymaster_with_insufficient_deposit_When_estimating_Then_the_real_bundler_reports_AA31()
        {
            var signerKey = EthECKey.GenerateKey();

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new VerifyingPaymasterDeployment
                {
                    EntryPoint = _fixture.EntryPointService.ContractAddress,
                    Owner = _fixture.OperatorAccount.Address,
                    Signer = signerKey.GetPublicAddress()
                });

            var (counter, _, _, _) = await CreateAccountAndCounterAsync(
                4209, new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));
            var handler = (AAContractHandler)counter.ContractHandler;
            handler.WithPaymaster(new PaymasterConfig(
                paymaster.ContractAddress,
                userOp => SignVerifyingPaymasterDataAsync(paymaster, userOp, signerKey)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.CreateUserOperationAsync(new CountFunction()));
            Assert.Contains("AA31", ex.Message);
        }

        [Fact]
        public async Task Given_a_verifying_paymaster_When_the_DataProvider_signs_with_the_wrong_key_Then_the_bundle_fails_with_AA34()
        {
            var signerKey = EthECKey.GenerateKey();
            var wrongKey = EthECKey.GenerateKey();

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new VerifyingPaymasterDeployment
                {
                    EntryPoint = _fixture.EntryPointService.ContractAddress,
                    Owner = _fixture.OperatorAccount.Address,
                    Signer = signerKey.GetPublicAddress()
                });

            await paymaster.DepositRequestAndWaitForReceiptAsync(
                new VerifyingPaymasterDepositFunction { AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m) });

            var (counter, _, _, _) = await CreateAccountAndCounterAsync(
                4210, new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));
            var handler = (AAContractHandler)counter.ContractHandler;

            handler.WithPaymaster(new PaymasterConfig(
                paymaster.ContractAddress,
                userOp => SignVerifyingPaymasterDataAsync(paymaster, userOp, wrongKey)));

            var packedOp = await handler.CreateUserOperationAsync(new CountFunction());

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("AA34", result.FailedOpReason ?? result.Error ?? "");
        }

        [Fact]
        public async Task SignTransactionAsync_IsNotSupported_PointsToUserOperationApi()
        {
            var (counter, _, _, _) = await CreateAccountAndCounterAsync(
                4205, new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID));
            var handler = (AAContractHandler)counter.ContractHandler;

            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => handler.SignTransactionAsync(new CountFunction()));
            Assert.Contains("CreateUserOperationAsync", ex.Message);
        }

        [Fact]
        public void UnsupportedEntryPointHashVersion_FailsLoudlyAtConstruction()
        {
            var bundler = new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID);

            var v07 = Assert.Throws<NotSupportedException>(() => new AAContractHandler(
                "0x0000000000000000000000000000000000000001",
                "0x0000000000000000000000000000000000000002",
                EthECKey.GenerateKey(),
                bundler,
                EntryPointAddresses.V07,
                _fixture.Web3));
            Assert.Contains("v0.7", v07.Message);

            Assert.Throws<NotSupportedException>(() => new AAContractHandler(
                "0x0000000000000000000000000000000000000001",
                "0x0000000000000000000000000000000000000002",
                EthECKey.GenerateKey(),
                bundler,
                EntryPointAddresses.V06,
                _fixture.Web3));
        }

        [Fact]
        public void ToRpcFormat_PaymasterFieldsOnlyWhenPaymasterSet()
        {
            var withoutPaymaster = UserOperationConverter.ToRpcFormat(new UserOperation
            {
                Sender = "0x0000000000000000000000000000000000000002",
                CallData = new byte[] { 0x01 }
            });
            Assert.Null(withoutPaymaster.Paymaster);
            Assert.Null(withoutPaymaster.PaymasterVerificationGasLimit);
            Assert.Null(withoutPaymaster.PaymasterPostOpGasLimit);

            var withPaymaster = UserOperationConverter.ToRpcFormat(new UserOperation
            {
                Sender = "0x0000000000000000000000000000000000000002",
                CallData = new byte[] { 0x01 },
                Paymaster = "0x0000000000000000000000000000000000000003",
                PaymasterVerificationGasLimit = 100,
                PaymasterPostOpGasLimit = 50
            });
            Assert.NotNull(withPaymaster.PaymasterVerificationGasLimit);
            Assert.NotNull(withPaymaster.PaymasterPostOpGasLimit);
        }
    }

    internal class EstimateOverridingBundlerService : IAccountAbstractionBundlerService
    {
        private readonly IAccountAbstractionBundlerService _inner;

        public EstimateOverridingBundlerService(
            IAccountAbstractionBundlerService inner,
            Func<RpcUserOperationGasEstimate, RpcUserOperationGasEstimate> transform)
        {
            _inner = inner;
            EstimateUserOperationGas = new TransformingEstimate(inner.EstimateUserOperationGas, transform, null);
        }

        public IEthChainId ChainId => _inner.ChainId;
        public IEthEstimateUserOperationGas EstimateUserOperationGas { get; }
        public IEthGetUserOperationByHash GetUserOperationByHash => _inner.GetUserOperationByHash;
        public IEthGetUserOperationReceipt GetUserOperationReceipt => _inner.GetUserOperationReceipt;
        public IEthSendUserOperation SendUserOperation => _inner.SendUserOperation;
        public IEthSupportedEntryPoints SupportedEntryPoints => _inner.SupportedEntryPoints;
    }

    internal class EstimateRecordingBundlerService : IAccountAbstractionBundlerService
    {
        private readonly IAccountAbstractionBundlerService _inner;
        private readonly TransformingEstimate _estimate;

        public EstimateRecordingBundlerService(IAccountAbstractionBundlerService inner)
        {
            _inner = inner;
            _estimate = new TransformingEstimate(inner.EstimateUserOperationGas, e => e, this);
        }

        public RpcUserOperation LastEstimateRequest { get; internal set; }

        public IEthChainId ChainId => _inner.ChainId;
        public IEthEstimateUserOperationGas EstimateUserOperationGas => _estimate;
        public IEthGetUserOperationByHash GetUserOperationByHash => _inner.GetUserOperationByHash;
        public IEthGetUserOperationReceipt GetUserOperationReceipt => _inner.GetUserOperationReceipt;
        public IEthSendUserOperation SendUserOperation => _inner.SendUserOperation;
        public IEthSupportedEntryPoints SupportedEntryPoints => _inner.SupportedEntryPoints;
    }

    internal class TransformingEstimate : IEthEstimateUserOperationGas
    {
        private readonly IEthEstimateUserOperationGas _inner;
        private readonly Func<RpcUserOperationGasEstimate, RpcUserOperationGasEstimate> _transform;
        private readonly EstimateRecordingBundlerService _recorder;

        public TransformingEstimate(
            IEthEstimateUserOperationGas inner,
            Func<RpcUserOperationGasEstimate, RpcUserOperationGasEstimate> transform,
            EstimateRecordingBundlerService recorder)
        {
            _inner = inner;
            _transform = transform;
            _recorder = recorder;
        }

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, object id = null)
            => _inner.BuildRequest(userOperation, entryPoint, id);

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, System.Collections.Generic.Dictionary<string, StateChange> stateOverrides, object id = null)
            => _inner.BuildRequest(userOperation, entryPoint, stateOverrides, id);

        public async Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, object id = null)
        {
            if (_recorder != null) _recorder.LastEstimateRequest = userOperation;
            return _transform(await _inner.SendRequestAsync(userOperation, entryPoint, id));
        }

        public Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, System.Collections.Generic.Dictionary<string, StateChange> stateOverrides, object id = null)
            => SendRequestAsync(userOperation, entryPoint, id);
    }
}
