using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(DogfoodClientServerFixture.COLLECTION_NAME)]
    public class DogfoodClientServerTests
    {
        private readonly DogfoodClientServerFixture _fixture;

        public DogfoodClientServerTests(DogfoodClientServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Standalone RPC server: real HTTP round trip from typed client to bundler", Order = 6)]
        public async Task FullCycle_ThroughRealHttp_EstimatesSendsAndReceivesReceipt()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                _fixture.BundlerClient,
                _fixture.EntryPointService.ContractAddress);

            var receipt = await counter.CountRequestAndWaitForReceiptAsync();

            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            Assert.True(aaReceipt.UserOpSuccess,
                $"UserOperation should succeed over the wire. Revert: {aaReceipt.RevertReason}");

            var count = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        public async Task GetUserOperationByHash_OverWire_ParsesWrapperDto()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                _fixture.BundlerClient,
                _fixture.EntryPointService.ContractAddress);

            var receipt = await counter.CountRequestAndWaitForReceiptAsync();
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);

            var byHash = await _fixture.BundlerClient.GetUserOperationByHash.SendRequestAsync(aaReceipt.UserOpHash);

            Assert.NotNull(byHash);
            Assert.NotNull(byHash.UserOperation);
            Assert.True(accountAddress.Equals(byHash.UserOperation.Sender, StringComparison.OrdinalIgnoreCase));
            Assert.True(_fixture.EntryPointService.ContractAddress.Equals(byHash.EntryPoint, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(byHash.TransactionHash);
        }

        [Fact]
        public async Task GetUserOperationReceipt_OverWire_MatchesTruthfulShape()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                _fixture.BundlerClient,
                _fixture.EntryPointService.ContractAddress);

            var receipt = await counter.CountRequestAndWaitForReceiptAsync();
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);

            var wireReceipt = await _fixture.BundlerClient.GetUserOperationReceipt.SendRequestAsync(aaReceipt.UserOpHash);

            Assert.NotNull(wireReceipt);
            Assert.True(wireReceipt.Success);
            Assert.NotNull(wireReceipt.Logs);
            Assert.True(wireReceipt.Logs.Count > 0, "the receipt must carry the operation's log slice");
            Assert.True(wireReceipt.ActualGasUsed.Value > 0);

            Assert.NotNull(wireReceipt.Receipt);
            Assert.NotNull(wireReceipt.Receipt.Logs);
            Assert.True(wireReceipt.Receipt.Logs.Length > 0, "the full bundle tx receipt must carry logs");
            Assert.False(string.IsNullOrEmpty(wireReceipt.Receipt.LogsBloom));
            Assert.NotNull(wireReceipt.Receipt.TransactionIndex);
        }

        [Fact]
        public async Task SendUserOperation_WrongSignerKey_SurfacesTypedBundlerErrorOverWire()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);
            var wrongKey = EthECKey.GenerateKey();

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = nonce,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, wrongKey);
            var rpcUserOp = UserOperationConverter.ToRpcFormat(packedOp);

            var ex = await Assert.ThrowsAsync<RpcResponseException>(() =>
                _fixture.BundlerClient.SendUserOperation.SendRequestAsync(
                    rpcUserOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(Nethereum.AccountAbstraction.Bundler.BundlerErrorCodes.InvalidSignature, ex.RpcError.Code);
            Assert.Contains("AA24", ex.RpcError.Message);
        }

        [Fact]
        public async Task SupportedEntryPointsAndChainId_OverWire_ReturnDeployedValues()
        {
            var entryPoints = await _fixture.BundlerClient.SupportedEntryPoints.SendRequestAsync();
            Assert.Contains(entryPoints, e => e.Equals(_fixture.EntryPointService.ContractAddress, StringComparison.OrdinalIgnoreCase));

            var chainId = await _fixture.BundlerClient.ChainId.SendRequestAsync();
            Assert.Equal(_fixture.ChainId, chainId.Value);
        }
    }
}
