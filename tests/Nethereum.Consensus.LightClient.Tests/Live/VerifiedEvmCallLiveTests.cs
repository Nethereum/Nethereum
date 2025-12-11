using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ChainStateVerification;
using Nethereum.ChainStateVerification.NodeData;
using Nethereum.Contracts;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [Collection("LiveTests")]
    public class VerifiedEvmCallLiveTests
    {
        private readonly ITestOutputHelper _output;

        public VerifiedEvmCallLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task VerifiedCall_ERC20_BalanceOf_ReturnsTokenBalance()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);
                Assert.NotNull(code);
                Assert.NotEmpty(code);
                _output.WriteLine($"WETH contract code size: {code.Length} bytes");

                var balanceOfFunction = new BalanceOfFunction { Owner = TestConstants.VitalikAddress };
                var callInput = balanceOfFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new BalanceOfOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"WETH Balance of {TestConstants.VitalikAddress}: {result.Balance} wei");
                _output.WriteLine($"WETH Balance: {Web3.Web3.Convert.FromWei(result.Balance)} WETH");

                Assert.True(result.Balance >= 0);
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_ERC20_TotalSupply_ReturnsSupply()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                var totalSupplyFunction = new TotalSupplyFunction();
                var callInput = totalSupplyFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new TotalSupplyOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"WETH Total Supply: {result.TotalSupply} wei");
                _output.WriteLine($"WETH Total Supply: {Web3.Web3.Convert.FromWei(result.TotalSupply)} WETH");

                Assert.True(result.TotalSupply > 0);
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_ERC20_Name_ReturnsName()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                var nameFunction = new NameFunction();
                var callInput = nameFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new NameOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"Token Name: {result.Name}");

                Assert.False(string.IsNullOrEmpty(result.Name));
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_ERC20_Symbol_ReturnsSymbol()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                var symbolFunction = new SymbolFunction();
                var callInput = symbolFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new SymbolOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"Token Symbol: {result.Symbol}");

                Assert.False(string.IsNullOrEmpty(result.Symbol));
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_ERC20_Decimals_ReturnsDecimals()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                var decimalsFunction = new DecimalsFunction();
                var callInput = decimalsFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new DecimalsOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"Token Decimals: {result.Decimals}");

                Assert.True(result.Decimals >= 0 && result.Decimals <= 18);
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_ERC721_BalanceOf_ReturnsNFTCount()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Finalized);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using finalized block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.BoredApeContract);
                Assert.NotNull(code);
                Assert.NotEmpty(code);
                _output.WriteLine($"BAYC contract code size: {code.Length} bytes");

                var balanceOfFunction = new BalanceOfFunction { Owner = TestConstants.VitalikAddress };
                var callInput = balanceOfFunction.CreateCallInput(TestConstants.BoredApeContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new BalanceOfOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"BAYC NFT Balance of {TestConstants.VitalikAddress}: {result.Balance}");

                Assert.True(result.Balance >= 0);
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        [Fact]
        public async Task VerifiedCall_OptimisticMode_UsesLatestHeader()
        {
            try
            {
                var verifiedState = await TestHelpers.CreateVerifiedStateServiceAsync(VerificationMode.Optimistic);
                var nodeDataService = new VerifiedNodeDataService(verifiedState);
                var executionStateService = new ExecutionStateService(nodeDataService);

                var header = verifiedState.GetCurrentHeader();
                _output.WriteLine($"Using optimistic block: {header.BlockNumber}");

                var code = await verifiedState.GetCodeAsync(TestConstants.WethContract);

                var balanceOfFunction = new BalanceOfFunction { Owner = TestConstants.VitalikAddress };
                var callInput = balanceOfFunction.CreateCallInput(TestConstants.WethContract);
                callInput.From = TestConstants.VitalikAddress;
                callInput.ChainId = new HexBigInteger(1);

                var programContext = new ProgramContext(callInput, executionStateService);
                var program = new Program(code, programContext);
                var evmSimulator = new EVMSimulator();
                await evmSimulator.ExecuteAsync(program);

                Assert.NotNull(program.ProgramResult);
                Assert.NotNull(program.ProgramResult.Result);

                var result = new BalanceOfOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex());
                _output.WriteLine($"WETH Balance (Optimistic): {result.Balance} wei");

                Assert.True(result.Balance >= 0);
            }
            catch (RpcResponseException ex) when (TestHelpers.IsPrunedHistoryError(ex))
            {
                _output.WriteLine($"Skipping test: RPC node does not have historical state. Error: {ex.Message}");
            }
            catch (Exception ex) when (TestHelpers.IsStateConsistencyError(ex))
            {
                _output.WriteLine($"Skipping test: State consistency error with optimistic block (timing issue). Error: {ex.Message}");
                _output.WriteLine("This can occur when the optimistic header is very close to the chain head.");
            }
            catch (Exception ex) when (TestHelpers.IsRateLimitError(ex))
            {
                _output.WriteLine($"Skipping test: Rate limited by RPC. Error: {ex.Message}");
            }
        }

        #region Function Definitions

        public partial class BalanceOfFunction : BalanceOfFunctionBase { }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunctionBase : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public virtual string Owner { get; set; }
        }

        public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

        [Function("totalSupply", "uint256")]
        public class TotalSupplyFunctionBase : FunctionMessage { }

        public partial class NameFunction : NameFunctionBase { }

        [Function("name", "string")]
        public class NameFunctionBase : FunctionMessage { }

        public partial class SymbolFunction : SymbolFunctionBase { }

        [Function("symbol", "string")]
        public class SymbolFunctionBase : FunctionMessage { }

        public partial class DecimalsFunction : DecimalsFunctionBase { }

        [Function("decimals", "uint8")]
        public class DecimalsFunctionBase : FunctionMessage { }

        public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

        [FunctionOutput]
        public class BalanceOfOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger Balance { get; set; }
        }

        public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

        [FunctionOutput]
        public class TotalSupplyOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger TotalSupply { get; set; }
        }

        public partial class NameOutputDTO : NameOutputDTOBase { }

        [FunctionOutput]
        public class NameOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("string", "", 1)]
            public virtual string Name { get; set; }
        }

        public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

        [FunctionOutput]
        public class SymbolOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("string", "", 1)]
            public virtual string Symbol { get; set; }
        }

        public partial class DecimalsOutputDTO : DecimalsOutputDTOBase { }

        [FunctionOutput]
        public class DecimalsOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint8", "", 1)]
            public virtual byte Decimals { get; set; }
        }

        #endregion
    }
}
