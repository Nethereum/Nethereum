using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests
{
    public class SignExtendDebugTest
    {
        private readonly ITestOutputHelper _output;

        public SignExtendDebugTest(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Debug_SignExtend_Inner_Call()
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());

            // Inner contract at 0x106 - same as signextend test Berlin_0_6_0
            var innerAddress = "0x0000000000000000000000000000000000000106";
            var innerCode = "0x60007fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff0b60005500".HexToByteArray();
            var innerAccount = executionState.CreateOrGetAccountExecutionState(innerAddress);
            innerAccount.Code = innerCode;
            innerAccount.Balance.SetInitialChainBalance(0xba1a9ce0ba1a9ce);

            _output.WriteLine($"Inner contract at {innerAddress}");
            _output.WriteLine($"Inner code bytes: {innerCode.Length}");
            _output.WriteLine($"Inner code hex: {innerCode.ToHex()}");

            // Parse inner instructions
            var innerInstructions = ProgramInstructionsUtils.GetProgramInstructions(innerCode);
            _output.WriteLine($"Inner instructions count: {innerInstructions.Count}");
            foreach (var instr in innerInstructions)
            {
                _output.WriteLine($"  Instruction {innerInstructions.IndexOf(instr)}: {instr.Instruction} at step {instr.Step}");
            }

            // Outer contract at 0xcccccccc... - same as signextend test
            var outerAddress = "0xcccccccccccccccccccccccccccccccccccccccc";
            var outerCode = "0x600060006000600060006004356101000162fffffff100".HexToByteArray();
            var outerAccount = executionState.CreateOrGetAccountExecutionState(outerAddress);
            outerAccount.Code = outerCode;
            outerAccount.Balance.SetInitialChainBalance(0xba1a9ce0ba1a9ce);

            _output.WriteLine($"\nOuter contract at {outerAddress}");
            _output.WriteLine($"Outer code bytes: {outerCode.Length}");

            // Other pre-state accounts (not needed for this call but present in test)
            var senderAddress = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
            var senderAccount = executionState.CreateOrGetAccountExecutionState(senderAddress);
            senderAccount.Balance.SetInitialChainBalance(0xba1a9ce0ba1a9ce);

            // Transaction matches Berlin_0_6_0
            var transaction = new TransactionInput
            {
                From = senderAddress,
                To = outerAddress,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger("0x1"),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger("0x4c4b400"), // 80,000,000
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger("0xa"),
                Data = "0x693c61390000000000000000000000000000000000000000000000000000000000000006",
                Nonce = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(1)
            };

            var programContext = new ProgramContext(transaction, executionState, null,
                blockNumber: 0, timestamp: 1000, coinbase: "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba", baseFee: 10);

            var program = new Program(outerCode, programContext);
            var simulator = new EVMSimulator();

            _output.WriteLine($"\nStarting execution with gas: {program.GasRemaining}");
            program = await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            _output.WriteLine($"\nTotal trace steps: {program.Trace.Count}");

            int depth0Count = 0, depth1Count = 0;
            foreach (var trace in program.Trace)
            {
                var op = trace.Instruction?.Instruction?.ToString() ?? "UNKNOWN";
                _output.WriteLine($"Step {trace.VMTraceStep}: depth={trace.Depth}, op={op}, pc={trace.Instruction?.Step}");
                if (trace.Depth == 0) depth0Count++;
                if (trace.Depth == 1) depth1Count++;
            }

            _output.WriteLine($"\nDepth 0 steps: {depth0Count}, Depth 1 steps: {depth1Count}");
            _output.WriteLine($"Expected: depth 0 = 12, depth 1 = 6 (total 18)");

            Assert.Equal(6, depth1Count);
        }
    }
}
