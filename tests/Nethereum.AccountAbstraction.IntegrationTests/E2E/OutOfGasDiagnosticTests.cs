using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
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

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("ERC", "4337")]
    public class OutOfGasDiagnosticTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public OutOfGasDiagnosticTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<TestCounterService> CreateCounterWithFixedGasAsync(
            ulong salt, RpcUserOperationGasEstimate fixedEstimate)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);
            var factoryConfig = new FactoryConfig(_fixture.AccountFactoryService.ContractAddress, ownerAddress, salt);

            var bundler = new FixedEstimateBundlerService(
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                fixedEstimate);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            counter.ChangeContractHandlerToAA(
                accountAddress, accountKey, bundler,
                _fixture.EntryPointService.ContractAddress, factory: factoryConfig);

            return counter;
        }

        [Fact]
        public async Task InnerCallOutOfGas_IsDiagnosedAsLikelyOutOfGas_WithNoReason()
        {
            var counter = await CreateCounterWithFixedGasAsync(7701, new RpcUserOperationGasEstimate
            {
                CallGasLimit = new HexBigInteger(1),
                VerificationGasLimit = new HexBigInteger(500_000),
                PreVerificationGas = new HexBigInteger(100_000)
            });

            var receipt = (AATransactionReceipt)await counter.ContractHandler
                .SendRequestAndWaitForReceiptAsync(new CountFunction());

            Assert.False(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.FailedWithoutReason, receipt.FailureKind);
            Assert.True(receipt.IsLikelyOutOfGas);
            Assert.Contains("callGasLimit", receipt.FailureDiagnostic);
        }

        [Fact]
        public async Task ExplicitRevertWithReason_IsDiagnosedAsRevertedWithReason_NotOutOfGas()
        {
            var counter = await CreateCounterWithFixedGasAsync(7702, new RpcUserOperationGasEstimate
            {
                CallGasLimit = new HexBigInteger(200_000),
                VerificationGasLimit = new HexBigInteger(500_000),
                PreVerificationGas = new HexBigInteger(100_000)
            });

            var receipt = (AATransactionReceipt)await counter.ContractHandler
                .SendRequestAndWaitForReceiptAsync(new CountFailFunction());

            Assert.False(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.RevertedWithReason, receipt.FailureKind);
            Assert.False(receipt.IsLikelyOutOfGas);
            Assert.Contains("count failed", receipt.RevertReason);
        }
    }

    internal class FixedEstimateBundlerService : IAccountAbstractionBundlerService
    {
        private readonly IAccountAbstractionBundlerService _inner;

        public FixedEstimateBundlerService(
            IAccountAbstractionBundlerService inner, RpcUserOperationGasEstimate fixedEstimate)
        {
            _inner = inner;
            EstimateUserOperationGas = new FixedEstimate(fixedEstimate);
        }

        public IEthChainId ChainId => _inner.ChainId;
        public IEthEstimateUserOperationGas EstimateUserOperationGas { get; }
        public IEthGetUserOperationByHash GetUserOperationByHash => _inner.GetUserOperationByHash;
        public IEthGetUserOperationReceipt GetUserOperationReceipt => _inner.GetUserOperationReceipt;
        public IEthSendUserOperation SendUserOperation => _inner.SendUserOperation;
        public IEthSupportedEntryPoints SupportedEntryPoints => _inner.SupportedEntryPoints;
    }

    internal class FixedEstimate : IEthEstimateUserOperationGas
    {
        private readonly RpcUserOperationGasEstimate _estimate;

        public FixedEstimate(RpcUserOperationGasEstimate estimate)
        {
            _estimate = estimate;
        }

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, object id = null)
            => throw new NotImplementedException();

        public RpcRequest BuildRequest(RpcUserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
            => throw new NotImplementedException();

        public Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, object id = null)
            => Task.FromResult(_estimate);

        public Task<RpcUserOperationGasEstimate> SendRequestAsync(RpcUserOperation userOperation, string entryPoint, Dictionary<string, StateChange> stateOverrides, object id = null)
            => Task.FromResult(_estimate);
    }
}
