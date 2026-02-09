using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "GasDiagnostic")]
    public class GasEstimationDiagnosticTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public GasEstimationDiagnosticTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private async Task<(string accountAddress, EthECKey accountKey, FactoryConfig factoryConfig)> CreateAccountWithFactoryAsync(ulong salt, decimal ethAmount = 5m)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, ethAmount);

            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                salt);

            return (accountAddress, accountKey, factoryConfig);
        }

        private async Task<(string tokenAddress, ERC20ContractService erc20Service)> DeployERC20TokenAsync()
        {
            var tokenDeployment = new Nethereum.StandardTokenEIP20.ContractDefinition.EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Diagnostic Token",
                TokenSymbol = "DIAG",
                DecimalUnits = 18
            };

            var web3 = (Web3.Web3)_fixture.Web3;
            var receipt = await web3.Eth.GetContractDeploymentHandler<Nethereum.StandardTokenEIP20.ContractDefinition.EIP20Deployment>()
                .SendRequestAndWaitForReceiptAsync(tokenDeployment);

            return (receipt.ContractAddress, web3.Eth.ERC20.GetContractService(receipt.ContractAddress));
        }

        [Fact]
        public async Task Diagnostic_CompareEvmVsNodeEstimation_ERC20Transfer()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(9001, 10m);
            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync();

            await erc20Service.TransferRequestAndWaitForReceiptAsync(accountAddress, Web3.Web3.Convert.ToWei(500));

            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(nodeDataService, DevChainBundlerFixture.CHAIN_ID, HardforkConfig.Default);

            var evmGasEstimator = new UserOperationGasEstimator(evmEstimator, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);
            var nodeGasEstimator = new UserOperationGasEstimator(_fixture.Web3, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);

            var transferFunc = new Nethereum.StandardTokenEIP20.ContractDefinition.TransferFunction
            {
                To = "0x" + new string('9', 40),
                Value = Web3.Web3.Convert.ToWei(50)
            };

            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = tokenAddress,
                Value = 0,
                Data = transferFunc.GetCallData()
            };

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(accountKey.GetPublicAddress(), factoryConfig.Salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var evmEstimate = await evmGasEstimator.EstimateGasAsync(userOp);
            var nodeEstimate = await nodeGasEstimator.EstimateGasAsync(userOp);

            _output.WriteLine("=== ERC20 Transfer with InitCode (Account Deployment) ===");
            _output.WriteLine("");
            _output.WriteLine("| Metric                    | EVM Estimator | Node RPC | Difference | % Diff |");
            _output.WriteLine("|---------------------------|---------------|----------|------------|--------|");
            _output.WriteLine($"| PreVerificationGas        | {evmEstimate.PreVerificationGas,13} | {nodeEstimate.PreVerificationGas,8} | {evmEstimate.PreVerificationGas - nodeEstimate.PreVerificationGas,10} | {GetPercentDiff(evmEstimate.PreVerificationGas, nodeEstimate.PreVerificationGas),5:F1}% |");
            _output.WriteLine($"| VerificationGasLimit      | {evmEstimate.VerificationGasLimit,13} | {nodeEstimate.VerificationGasLimit,8} | {evmEstimate.VerificationGasLimit - nodeEstimate.VerificationGasLimit,10} | {GetPercentDiff(evmEstimate.VerificationGasLimit, nodeEstimate.VerificationGasLimit),5:F1}% |");
            _output.WriteLine($"| CallGasLimit              | {evmEstimate.CallGasLimit,13} | {nodeEstimate.CallGasLimit,8} | {evmEstimate.CallGasLimit - nodeEstimate.CallGasLimit,10} | {GetPercentDiff(evmEstimate.CallGasLimit, nodeEstimate.CallGasLimit),5:F1}% |");
            _output.WriteLine($"| PaymasterVerificationGas  | {evmEstimate.PaymasterVerificationGasLimit,13} | {nodeEstimate.PaymasterVerificationGasLimit,8} | {evmEstimate.PaymasterVerificationGasLimit - nodeEstimate.PaymasterVerificationGasLimit,10} | {GetPercentDiff(evmEstimate.PaymasterVerificationGasLimit, nodeEstimate.PaymasterVerificationGasLimit),5:F1}% |");
            _output.WriteLine($"| PaymasterPostOpGas        | {evmEstimate.PaymasterPostOpGasLimit,13} | {nodeEstimate.PaymasterPostOpGasLimit,8} | {evmEstimate.PaymasterPostOpGasLimit - nodeEstimate.PaymasterPostOpGasLimit,10} | {GetPercentDiff(evmEstimate.PaymasterPostOpGasLimit, nodeEstimate.PaymasterPostOpGasLimit),5:F1}% |");

            var evmTotal = evmEstimate.PreVerificationGas + evmEstimate.VerificationGasLimit + evmEstimate.CallGasLimit;
            var nodeTotal = nodeEstimate.PreVerificationGas + nodeEstimate.VerificationGasLimit + nodeEstimate.CallGasLimit;
            _output.WriteLine($"| **TOTAL**                 | {evmTotal,13} | {nodeTotal,8} | {evmTotal - nodeTotal,10} | {GetPercentDiff(evmTotal, nodeTotal),5:F1}% |");
            _output.WriteLine("");

            Assert.True(evmEstimate.VerificationGasLimit > 0);
            Assert.True(evmEstimate.CallGasLimit > 0);
        }

        [Fact]
        public async Task Diagnostic_CompareEvmVsNodeEstimation_SimpleCounter()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(9002, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(_fixture.Web3, new TestCounterDeployment());

            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(nodeDataService, DevChainBundlerFixture.CHAIN_ID, HardforkConfig.Default);

            var evmGasEstimator = new UserOperationGasEstimator(evmEstimator, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);
            var nodeGasEstimator = new UserOperationGasEstimator(_fixture.Web3, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);

            var countFunc = new CountFunction();
            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = testCounter.ContractAddress,
                Value = 0,
                Data = countFunc.GetCallData()
            };

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(accountKey.GetPublicAddress(), factoryConfig.Salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var evmEstimate = await evmGasEstimator.EstimateGasAsync(userOp);
            var nodeEstimate = await nodeGasEstimator.EstimateGasAsync(userOp);

            _output.WriteLine("=== Simple Counter with InitCode (Account Deployment) ===");
            _output.WriteLine("");
            _output.WriteLine("| Metric                    | EVM Estimator | Node RPC | Difference | % Diff |");
            _output.WriteLine("|---------------------------|---------------|----------|------------|--------|");
            _output.WriteLine($"| PreVerificationGas        | {evmEstimate.PreVerificationGas,13} | {nodeEstimate.PreVerificationGas,8} | {evmEstimate.PreVerificationGas - nodeEstimate.PreVerificationGas,10} | {GetPercentDiff(evmEstimate.PreVerificationGas, nodeEstimate.PreVerificationGas),5:F1}% |");
            _output.WriteLine($"| VerificationGasLimit      | {evmEstimate.VerificationGasLimit,13} | {nodeEstimate.VerificationGasLimit,8} | {evmEstimate.VerificationGasLimit - nodeEstimate.VerificationGasLimit,10} | {GetPercentDiff(evmEstimate.VerificationGasLimit, nodeEstimate.VerificationGasLimit),5:F1}% |");
            _output.WriteLine($"| CallGasLimit              | {evmEstimate.CallGasLimit,13} | {nodeEstimate.CallGasLimit,8} | {evmEstimate.CallGasLimit - nodeEstimate.CallGasLimit,10} | {GetPercentDiff(evmEstimate.CallGasLimit, nodeEstimate.CallGasLimit),5:F1}% |");

            var evmTotal = evmEstimate.PreVerificationGas + evmEstimate.VerificationGasLimit + evmEstimate.CallGasLimit;
            var nodeTotal = nodeEstimate.PreVerificationGas + nodeEstimate.VerificationGasLimit + nodeEstimate.CallGasLimit;
            _output.WriteLine($"| **TOTAL**                 | {evmTotal,13} | {nodeTotal,8} | {evmTotal - nodeTotal,10} | {GetPercentDiff(evmTotal, nodeTotal),5:F1}% |");
            _output.WriteLine("");

            Assert.True(evmEstimate.VerificationGasLimit > 0);
        }

        [Fact]
        public async Task Diagnostic_CompareEvmVsNodeEstimation_ExistingAccount()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 9103;
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(_fixture.Web3, new TestCounterDeployment());

            var countFunc = new CountFunction();
            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = testCounter.ContractAddress,
                Value = 0,
                Data = countFunc.GetCallData()
            };

            var deployUserOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                VerificationGasLimit = 500_000,
                CallGasLimit = 100_000,
                PreVerificationGas = 50_000
            };
            var packedDeploy = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployUserOp, accountKey);
            using var deployBundler = _fixture.CreateNewBundlerService();
            await deployBundler.SendUserOperationAsync(packedDeploy, _fixture.EntryPointService.ContractAddress);
            var deployResult = await deployBundler.ExecuteBundleAsync();
            Assert.True(deployResult?.Success == true, $"Account deployment should succeed: {deployResult?.Error}");

            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(nodeDataService, DevChainBundlerFixture.CHAIN_ID, HardforkConfig.Default);

            var evmGasEstimator = new UserOperationGasEstimator(evmEstimator, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);
            var nodeGasEstimator = new UserOperationGasEstimator(_fixture.Web3, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var evmEstimate = await evmGasEstimator.EstimateGasAsync(userOp);
            var nodeEstimate = await nodeGasEstimator.EstimateGasAsync(userOp);

            _output.WriteLine("=== Simple Counter WITHOUT InitCode (Existing Account) ===");
            _output.WriteLine("");
            _output.WriteLine("| Metric                    | EVM Estimator | Node RPC | Difference | % Diff |");
            _output.WriteLine("|---------------------------|---------------|----------|------------|--------|");
            _output.WriteLine($"| PreVerificationGas        | {evmEstimate.PreVerificationGas,13} | {nodeEstimate.PreVerificationGas,8} | {evmEstimate.PreVerificationGas - nodeEstimate.PreVerificationGas,10} | {GetPercentDiff(evmEstimate.PreVerificationGas, nodeEstimate.PreVerificationGas),5:F1}% |");
            _output.WriteLine($"| VerificationGasLimit      | {evmEstimate.VerificationGasLimit,13} | {nodeEstimate.VerificationGasLimit,8} | {evmEstimate.VerificationGasLimit - nodeEstimate.VerificationGasLimit,10} | {GetPercentDiff(evmEstimate.VerificationGasLimit, nodeEstimate.VerificationGasLimit),5:F1}% |");
            _output.WriteLine($"| CallGasLimit              | {evmEstimate.CallGasLimit,13} | {nodeEstimate.CallGasLimit,8} | {evmEstimate.CallGasLimit - nodeEstimate.CallGasLimit,10} | {GetPercentDiff(evmEstimate.CallGasLimit, nodeEstimate.CallGasLimit),5:F1}% |");

            var evmTotal = evmEstimate.PreVerificationGas + evmEstimate.VerificationGasLimit + evmEstimate.CallGasLimit;
            var nodeTotal = nodeEstimate.PreVerificationGas + nodeEstimate.VerificationGasLimit + nodeEstimate.CallGasLimit;
            _output.WriteLine($"| **TOTAL**                 | {evmTotal,13} | {nodeTotal,8} | {evmTotal - nodeTotal,10} | {GetPercentDiff(evmTotal, nodeTotal),5:F1}% |");
            _output.WriteLine("");

            Assert.True(evmEstimate.VerificationGasLimit > 0);
        }

        [Fact]
        public async Task Diagnostic_EstimateVsActualGasUsed()
        {
            ulong salt = (ulong)Random.Shared.NextInt64();
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployUserOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                VerificationGasLimit = 500_000,
                CallGasLimit = 50_000,
                PreVerificationGas = 50_000
            };
            var packedDeploy = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployUserOp, accountKey);
            using var deployBundler = _fixture.CreateNewBundlerService();
            await deployBundler.SendUserOperationAsync(packedDeploy, _fixture.EntryPointService.ContractAddress);
            var deployResult = await deployBundler.ExecuteBundleAsync();
            Assert.True(deployResult?.Success == true, $"Account deployment should succeed: {deployResult?.Error}");

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(_fixture.Web3, new TestCounterDeployment());

            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(nodeDataService, DevChainBundlerFixture.CHAIN_ID, HardforkConfig.Default);
            var evmGasEstimator = new UserOperationGasEstimator(evmEstimator, _fixture.EntryPointService.ContractAddress, _fixture.OperatorAccount.Address);

            var countFunc = new CountFunction();
            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = testCounter.ContractAddress,
                Value = 0,
                Data = countFunc.GetCallData()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await evmGasEstimator.EstimateGasAsync(userOp);
            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas;
            userOp.CallGasLimit = (long)estimate.CallGasLimit;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            var estimatedTotal = estimate.PreVerificationGas + estimate.VerificationGasLimit + estimate.CallGasLimit;
            var actualGasUsed = result?.GasUsed ?? 0;
            var overhead = estimatedTotal - actualGasUsed;
            var overheadPercent = actualGasUsed > 0 ? (double)overhead / (double)actualGasUsed * 100 : 0;
            var efficiencyPercent = actualGasUsed > 0 ? (double)actualGasUsed / (double)estimatedTotal * 100 : 0;

            _output.WriteLine("=== EVM Estimation vs Actual Gas Used (Deployed Account) ===");
            _output.WriteLine("");
            _output.WriteLine("| Metric                    | Value         |");
            _output.WriteLine("|---------------------------|---------------|");
            _output.WriteLine($"| PreVerificationGas (est)  | {estimate.PreVerificationGas,13} |");
            _output.WriteLine($"| VerificationGasLimit (est)| {estimate.VerificationGasLimit,13} |");
            _output.WriteLine($"| CallGasLimit (est)        | {estimate.CallGasLimit,13} |");
            _output.WriteLine($"| **Total Estimated**       | {estimatedTotal,13} |");
            _output.WriteLine($"| **Actual Gas Used**       | {actualGasUsed,13} |");
            _output.WriteLine($"| **Overhead (unused)**     | {overhead,13} |");
            _output.WriteLine($"| **Efficiency**            | {efficiencyPercent,12:F1}% |");
            _output.WriteLine($"| **Overhead % of actual**  | {overheadPercent,12:F1}% |");
            _output.WriteLine("");
            _output.WriteLine($"Execution Success: {result?.Success}");

            Assert.True(result?.Success == true, $"Execution should succeed: {result?.Error}");
            Assert.True(actualGasUsed <= estimatedTotal, $"Actual gas ({actualGasUsed}) should be <= estimated ({estimatedTotal})");
            Assert.True(actualGasUsed > 0, "Actual gas should be > 0");
        }

        private double GetPercentDiff(BigInteger a, BigInteger b)
        {
            if (b == 0) return a == 0 ? 0 : 100;
            return (double)(a - b) / (double)b * 100;
        }
    }
}
