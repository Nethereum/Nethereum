using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    public abstract class EvmCapturedStateTestBase
    {
        protected const string CapturedStateFolder = "EVM/CapturedState";
        protected const string TracesFolder = "EVM/Traces";

        protected abstract EthereumClientIntegrationFixture GetFixture();

        protected async Task<(Program Program, CapturedExecutionState State)> RunTransaction(
            string transactionHash,
            Action<ExecutionStateService> configureState = null)
        {
            var stateFile = Path.Combine(CapturedStateFolder, $"{transactionHash}.json");

            if (File.Exists(stateFile))
            {
                return await ReplayFromCapturedState(stateFile, configureState);
            }
            else
            {
                return await CaptureAndSaveState(transactionHash, stateFile, configureState);
            }
        }

        protected async Task<(Program Program, CapturedExecutionState State)> CaptureAndSaveState(
            string transactionHash,
            string stateFile,
            Action<ExecutionStateService> configureState = null)
        {
            var web3 = GetFixture().GetInfuraWeb3(InfuraNetwork.Mainnet);
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(txn.BlockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(txn.To);

            var txnInput = txn.ConvertToTransactionInput();
            txnInput.ChainId = new HexBigInteger(1);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(new HexBigInteger(txn.BlockNumber.Value - 1)));
            var executionStateService = new ExecutionStateService(nodeDataService);

            if (configureState != null)
            {
                configureState(executionStateService);
            }

            var programContext = new ProgramContext(txnInput, executionStateService, null, null, (long)txn.BlockNumber.Value, (long)block.Timestamp.Value);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();

            try
            {
                program = await evmSimulator.ExecuteAsync(program, 0, 0, true);
            }
            catch (Exception ex)
            {
                program.ProgramResult.Exception = ex;
            }

            var capturedState = CapturedExecutionState.CaptureFromExecution(
                executionStateService,
                transactionHash,
                (long)txn.BlockNumber.Value,
                (long)block.Timestamp.Value,
                txnInput,
                code,
                program.ProgramResult.Logs.Count,
                program.Trace.Count,
                program.ProgramResult.IsRevert);

            Directory.CreateDirectory(Path.GetDirectoryName(stateFile));
            capturedState.SaveToFile(stateFile);
            Debug.WriteLine($"Captured state saved to: {stateFile}");

            return (program, capturedState);
        }

        protected async Task<(Program Program, CapturedExecutionState State)> ReplayFromCapturedState(
            string stateFile,
            Action<ExecutionStateService> configureState = null)
        {
            var capturedState = CapturedExecutionState.LoadFromFile(stateFile);
            var program = await ReplayFromCapturedState(capturedState, configureState);
            return (program, capturedState);
        }

        protected Task<Program> ReplayFromCapturedState(
            CapturedExecutionState capturedState,
            Action<ExecutionStateService> configureState = null)
        {
            var txnInput = capturedState.TransactionInput.ToTransactionInput();
            var code = capturedState.ContractCode;

            var nodeDataService = new InMemoryNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);
            capturedState.ConfigureExecutionState(executionStateService);

            if (configureState != null)
            {
                configureState(executionStateService);
            }

            var programContext = new ProgramContext(txnInput, executionStateService, null, null, capturedState.BlockNumber, capturedState.Timestamp);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();

            return evmSimulator.ExecuteAsync(program, 0, 0, true);
        }

        protected string FindTraceFile(string transactionHash)
        {
            var csvPath = Path.Combine(TracesFolder, $"{transactionHash}.csv");
            if (File.Exists(csvPath)) return csvPath;

            var jsonPath = Path.Combine(TracesFolder, $"{transactionHash}.json");
            if (File.Exists(jsonPath)) return jsonPath;

            var zipPath = Path.Combine(TracesFolder, $"{transactionHash}.zip");
            if (File.Exists(zipPath)) return zipPath;

            var csvEtherscanPath = Path.Combine("EVM/TracesCSVEtherscan", $"{transactionHash}.csv");
            if (File.Exists(csvEtherscanPath)) return csvEtherscanPath;

            return null;
        }

        protected async Task ValidateAgainstTraces(string transactionHash, Program program, bool requireTraceMatch = true)
        {
            var traceFile = FindTraceFile(transactionHash);
            if (traceFile == null) return;

            List<ExternalTrace> externalTraces;

            if (traceFile.EndsWith(".csv"))
            {
                var csvContent = File.ReadAllText(traceFile);
                externalTraces = ExternalTrace.ParseFromEtherscanCsv(csvContent);
            }
            else if (traceFile.EndsWith(".zip"))
            {
                var json = Unzip(traceFile);
                externalTraces = JsonConvert.DeserializeObject<List<ExternalTrace>>(json);
            }
            else
            {
                var json = File.ReadAllText(traceFile);
                externalTraces = JsonConvert.DeserializeObject<List<ExternalTrace>>(json);
            }

            var trace = program.Trace;

            if (externalTraces.Count != trace.Count)
            {
                var diagLines = new List<string>();
                diagLines.Add($"TRACE MISMATCH: Transaction {transactionHash} - Expected {externalTraces.Count} traces, got {trace.Count}");

                var minCount = Math.Min(externalTraces.Count, trace.Count);
                for (int i = 0; i < minCount; i++)
                {
                    var traceStep = trace[i];
                    var externalTrace = externalTraces[i];
                    var instructionName = traceStep.Instruction.Instruction.ToString();
                    if (instructionName == "KECCAK256" && externalTrace.Op == "SHA3") instructionName = "SHA3";

                    if (externalTrace.Depth - 1 != traceStep.Depth ||
                        externalTrace.Pc != traceStep.Instruction.Step ||
                        externalTrace.Op != instructionName)
                    {
                        diagLines.Add($"DIVERGENCE at step {i}: Expected PC={externalTrace.Pc} Op={externalTrace.Op} Depth={externalTrace.Depth}, Got PC={traceStep.Instruction.Step} Op={instructionName} Depth={traceStep.Depth + 1}");
                        break;
                    }
                }

                if (trace.Count < externalTraces.Count)
                {
                    var lastStep = trace.Count > 0 ? trace[trace.Count - 1] : null;
                    diagLines.Add($"LAST STEP: PC={lastStep?.Instruction.Step} Op={lastStep?.Instruction.Instruction} - Simulation ended early");
                }

                File.WriteAllLines("trace_diagnostic.txt", diagLines);

                if (requireTraceMatch)
                {
                    Assert.Equal(externalTraces.Count, trace.Count);
                }
                return;
            }

            for (int i = 0; i < trace.Count; i++)
            {
                var traceStep = trace[i];
                var externalTrace = externalTraces[i];

                Assert.Equal(externalTrace.Depth - 1, traceStep.Depth);
                Assert.Equal(externalTrace.Pc, traceStep.Instruction.Step);

                var instructionName = traceStep.Instruction.Instruction.ToString();
                if (instructionName == "KECCAK256" && externalTrace.Op == "SHA3") instructionName = "SHA3";
                Assert.Equal(externalTrace.Op, instructionName);
            }
        }

        protected void ValidateLogs(Program program, CapturedExecutionState state)
        {
            Assert.Equal(state.ExpectedIsRevert, program.ProgramResult.IsRevert);
            Assert.Equal(state.ExpectedLogCount, program.ProgramResult.Logs.Count);
        }

        public static string Unzip(string filePath)
        {
            var rawFileStream = File.OpenRead(filePath);
            byte[] zippedtoTextBuffer = new byte[rawFileStream.Length];
            rawFileStream.Read(zippedtoTextBuffer, 0, (int)rawFileStream.Length);
            return Unzip(zippedtoTextBuffer);
        }

        public static string Unzip(byte[] zippedBuffer)
        {
            using (var zippedStream = new MemoryStream(zippedBuffer))
            {
                using (var archive = new ZipArchive(zippedStream))
                {
                    var entry = archive.Entries.FirstOrDefault();
                    if (entry != null)
                    {
                        using (var unzippedEntryStream = entry.Open())
                        {
                            using (var ms = new MemoryStream())
                            {
                                unzippedEntryStream.CopyTo(ms);
                                var unzippedArray = ms.ToArray();
                                return Encoding.Default.GetString(unzippedArray);
                            }
                        }
                    }
                    return null;
                }
            }
        }
    }
}
