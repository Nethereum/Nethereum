using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Documentation
{
    public class EvmSimulatorDocExampleTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string ContractAddress = "0x2222222222222222222222222222222222222222";

        [NethereumDocExample(DocSection.EvmSimulator, "quick-start", "Quick start: simulate a transaction against any IStateReader and read the result", Order = 1)]
        [Fact]
        public async Task QuickStart_SimulateATransactionAndReadTheResult()
        {
            IStateReader state = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = EvmUInt256.Parse("1000000000000000000") },
                [ContractAddress] = new AccountState { Code = "600760005500".HexToByteArray() }
            });

            var result = await new TransactionExecutor(DefaultHardforkConfigs.Cancun).ExecuteAsync(
                new TransactionExecutionContext
                {
                    Sender = SenderAddress,
                    To = ContractAddress,
                    Data = new byte[0],
                    GasLimit = 1_000_000,
                    GasPrice = 1,
                    ChainId = 1,
                    BlockNumber = 19_426_587,
                    Timestamp = MainnetChainActivations.CancunTimestamp,
                    Coinbase = SenderAddress,
                    ExecutionState = new ExecutionStateService(state)
                });

            Assert.True(result.Success, result.Error);
            Assert.True(result.GasUsed > 21_000);
            Assert.Empty(result.Logs);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "run-bytecode", "Run raw EVM bytecode and read the top of the stack", Order = 1)]
        [Fact]
        public async Task RunRawBytecodeAndReadTheStack()
        {
            var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);
            var program = new Program("6002600301".HexToByteArray());

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.Equal(
                "0000000000000000000000000000000000000000000000000000000000000005",
                program.StackPeek().ToHex());
        }

        [NethereumDocExample(DocSection.EvmSimulator, "run-bytecode", "Read the per-step trace: opcode, gas cost and gas remaining", Order = 2)]
        [Fact]
        public async Task ReadThePerStepTrace()
        {
            var context = new ProgramContext(
                new EvmCallContext { From = SenderAddress, To = ContractAddress, Data = new byte[0], Gas = 1_000_000 },
                new ExecutionStateService(new InMemoryStateReader(new Dictionary<string, AccountState>())));

            var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);
            var program = new Program("6002600301".HexToByteArray(), context);

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: true);

            var steps = program.Trace;
            Assert.Equal(Instruction.PUSH1, steps[0].Instruction.Instruction);
            Assert.Equal(3, steps[0].GasCost);
            Assert.Equal(Instruction.ADD, steps[2].Instruction.Instruction);
            Assert.Equal(9, program.TotalGasUsed);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "disassemble-bytecode", "Disassemble deployed bytecode into opcodes and immediates", Order = 1)]
        [Fact]
        public void DisassembleDeployedBytecode()
        {
            var instructions = ProgramInstructionsUtils.GetProgramInstructions("60806040523415");

            Assert.Equal("0000   60   PUSH1  0x80", instructions[0].ToDisassemblyLine());
            Assert.Equal(Instruction.MSTORE, instructions[2].Instruction);
            Assert.Equal(Instruction.CALLVALUE, instructions[3].Instruction);

            var listing = ProgramInstructionsUtils.DisassembleToString(instructions);
            Assert.Contains("MSTORE", listing);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "hardfork-config", "Resolve the active mainnet fork for a block and look its rules up in the registry", Order = 1)]
        [Fact]
        public void ResolveTheForkForABlockAndLookUpItsRules()
        {
            var fork = MainnetChainActivations.Instance.ResolveAt(
                blockNumber: 19_426_587, timestamp: MainnetChainActivations.CancunTimestamp);

            Assert.Equal(HardforkName.Cancun, fork);

            var config = DefaultMainnetHardforkRegistry.Instance.Get(fork);

            Assert.Equal(GasConstants.MAX_CODE_SIZE, config.MaxCodeSize);
            Assert.True(config.BaseFeeApplies);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "hardfork-config", "An unregistered chain id is refused rather than silently replayed under mainnet rules", Order = 2)]
        [Fact]
        public void AnUnregisteredChainIdIsRefused()
        {
            var registry = new ChainActivationsRegistry();

            Assert.Equal(
                HardforkName.Cancun,
                registry.ResolveAt(chainId: 1, blockNumber: 19_426_587, timestamp: MainnetChainActivations.CancunTimestamp));

            Assert.Throws<System.InvalidOperationException>(
                () => registry.ResolveAt(chainId: 424242, blockNumber: 1, timestamp: 1));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "precompiles", "A HardforkConfig preset carries no precompile registry until you attach one", Order = 1)]
        [Fact]
        public void AttachAPrecompileRegistryToAHardforkConfig()
        {
            Assert.Null(HardforkConfig.Cancun.Precompiles);

            var config = HardforkConfig.Cancun.WithPrecompiles(DefaultPrecompileRegistries.CancunBase());

            Assert.True(config.Precompiles.CanHandle(0x01));
            Assert.True(config.Precompiles.CanHandle(0x09));
            Assert.False(config.Precompiles.CanHandle(0x0b));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "precompiles", "Ask the registry what a precompile call would cost before executing it", Order = 2)]
        [Fact]
        public void QueryPrecompileGasAndExecuteAHandler()
        {
            var registry = DefaultPrecompileRegistries.OsakaBase();

            Assert.Equal(6900L, registry.GetGasCost(0x100, new byte[160]));

            var identity = registry.Get(0x04);
            Assert.Equal(0x04, identity.AddressNumeric);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, registry.Execute(0x04, new byte[] { 0x01, 0x02, 0x03 }));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "simulate-transaction", "Simulate a transaction against in-memory pre-state and read gas, logs and storage", Order = 1)]
        [Fact]
        public async Task SimulateATransactionAgainstInMemoryState()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = EvmUInt256.Parse("1000000000000000000") },
                [ContractAddress] = new AccountState { Code = "600760005500".HexToByteArray() }
            };
            var stateReader = new InMemoryStateReader(accounts);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = ContractAddress,
                Data = new byte[0],
                GasLimit = 1_000_000,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 19_426_587,
                Timestamp = MainnetChainActivations.CancunTimestamp,
                Coinbase = SenderAddress,
                ChainId = 1,
                ExecutionState = new ExecutionStateService(stateReader)
            };

            var executor = new TransactionExecutor(DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Cancun));
            var result = await executor.ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(TransactionError.None, result.ErrorCode);
            Assert.True(result.GasUsed > 21_000);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "simulate-transaction", "An in-EVM revert fails the transaction without an ErrorCode - ErrorCode reports validation failures only", Order = 2)]
        [Fact]
        public async Task AnInEvmRevertFailsTheTransactionWithoutSettingAnErrorCode()
        {
            var revertsImmediately = "60006000fd";

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = EvmUInt256.Parse("1000000000000000000") },
                [ContractAddress] = new AccountState { Code = revertsImmediately.HexToByteArray() }
            };

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = ContractAddress,
                Data = new byte[0],
                GasLimit = 1_000_000,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 19_426_587,
                Timestamp = MainnetChainActivations.CancunTimestamp,
                Coinbase = SenderAddress,
                ChainId = 1,
                ExecutionState = new ExecutionStateService(new InMemoryStateReader(accounts))
            };

            var result = await new TransactionExecutor(
                DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Cancun)).ExecuteAsync(ctx);

            Assert.False(result.Success);
            Assert.True(result.ProgramResult.IsRevert);
            Assert.Equal(TransactionError.None, result.ErrorCode);
            Assert.False(result.IsValidationError);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "Compose EIP-7685 execution requests and compute the block requests_hash", Order = 1)]
        [Fact]
        public void ComposeExecutionRequestsAndComputeTheCommitment()
        {
            Assert.Equal(
                ExecutionRequests.WithdrawalRequestType,
                ExecutionRequests.RequestTypeFor(SystemCallContracts.WithdrawalRequests));
            Assert.Equal(
                ExecutionRequests.ConsolidationRequestType,
                ExecutionRequests.RequestTypeFor(SystemCallContracts.ConsolidationRequests));

            var withdrawal = ExecutionRequests.Compose(
                ExecutionRequests.WithdrawalRequestType, new byte[] { 0xAA, 0xBB });

            Assert.Equal(0x01, withdrawal[0]);
            Assert.True(ExecutionRequests.CarriesData(withdrawal));

            var empty = ExecutionRequests.Compose(ExecutionRequests.DepositRequestType, new byte[0]);
            Assert.False(ExecutionRequests.CarriesData(empty));

            Assert.Equal(
                ExecutionRequests.ComputeRequestsHash(new[] { withdrawal }),
                ExecutionRequests.ComputeRequestsHash(new[] { empty, withdrawal }));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "Before Prague there is no requests_hash at all, which is not the hash of an empty list", Order = 2)]
        [Fact]
        public void BeforePragueThereIsNoRequestsCommitment()
        {
            Assert.False(ExecutionRequests.IsActive(HardforkName.Cancun));
            Assert.Null(ExecutionRequests.CommitmentFor(HardforkName.Cancun, new List<byte[]>()));

            Assert.True(ExecutionRequests.IsActive(HardforkName.Prague));
            Assert.NotNull(ExecutionRequests.CommitmentFor(HardforkName.Prague, new List<byte[]>()));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "Deposits open the requests list, and the predeploy set widens at Amsterdam", Order = 3)]
        [Fact]
        public void DepositsOpenTheListAndTheRequestPredeploySetWidensAtAmsterdam()
        {
            Assert.Empty(SystemCallContracts.RequestContractsFor(HardforkName.Cancun));
            Assert.Equal(2, SystemCallContracts.RequestContractsFor(HardforkName.Prague).Count);
            Assert.Equal(4, SystemCallContracts.RequestContractsFor(HardforkName.Amsterdam).Count);

            var requests = BlockExecutionRequests.OpenedWithDeposits(HardforkName.Prague, new List<Log>());
            requests.AddFrom(SystemCallContracts.WithdrawalRequests, new byte[] { 0x01 });

            Assert.NotNull(requests.Commitment());
        }

        [NethereumDocExample(DocSection.EvmSimulator, "system-calls", "A request predeploy failing or missing invalidates the block; the beacon-roots and history predeploys fail silently", Order = 1)]
        [Fact]
        public void OnlyRequestPredeployFailuresInvalidateTheBlock()
        {
            Assert.True(SystemCallFailurePolicy.FailureInvalidatesBlock(SystemCallContracts.WithdrawalRequests));
            Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.ConsolidationRequests));
            Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.BuilderDeposit));
            Assert.True(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.BuilderExit));

            Assert.False(SystemCallFailurePolicy.FailureInvalidatesBlock(SystemCallContracts.BeaconRoots));
            Assert.False(SystemCallFailurePolicy.AbsenceInvalidatesBlock(SystemCallContracts.HistoryStorage));
        }
    }
}
