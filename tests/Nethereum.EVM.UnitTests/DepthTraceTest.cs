using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests
{
    public class DepthTraceTest
    {
        private readonly ITestOutputHelper _output;

        public DepthTraceTest(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Test_Inner_Call_Depth_Tracking()
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());

            var innerContractAddress = "0x0000000000000000000000000000000000000104";
            var innerContractCode = "0x600160005260206000f3".HexToByteArray();
            var innerAccount = executionState.CreateOrGetAccountExecutionState(innerContractAddress);
            innerAccount.Code = innerContractCode;
            innerAccount.Balance.SetInitialChainBalance(1000000000);

            var outerContractAddress = "0xcccccccccccccccccccccccccccccccccccccccc";
            var outerContractCode = "0x60206000600060006000630000010463001000f0f100".HexToByteArray();
            var outerAccount = executionState.CreateOrGetAccountExecutionState(outerContractAddress);
            outerAccount.Code = outerContractCode;
            outerAccount.Balance.SetInitialChainBalance(1000000000);

            var transaction = new TransactionInput
            {
                From = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b",
                To = outerContractAddress,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(10000000),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(10),
                Data = "0x",
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(1)
            };

            var programContext = new ProgramContext(transaction, executionState, null,
                blockNumber: 1, timestamp: 1000, coinbase: "0x0000000000000000000000000000000000000000", baseFee: 10);
            var program = new Program(outerContractCode, programContext);
            var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);

            _output.WriteLine($"Starting gas: {program.GasRemaining}");
            program = await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            _output.WriteLine($"Total trace steps: {program.Trace.Count}");
            
            int depth0Count = 0, depth1Count = 0;
            foreach (var trace in program.Trace)
            {
                var op = trace.Instruction?.Instruction?.ToString() ?? "UNKNOWN";
                _output.WriteLine($"Step {trace.VMTraceStep}: depth={trace.Depth}, op={op}, pc={trace.Instruction?.Step}");
                if (trace.Depth == 0) depth0Count++;
                if (trace.Depth == 1) depth1Count++;
            }
            
            _output.WriteLine($"\nDepth 0 steps: {depth0Count}, Depth 1 steps: {depth1Count}");
            Assert.True(depth1Count > 0, "Should have at least some depth=1 steps from inner call");
        }
    }
}
