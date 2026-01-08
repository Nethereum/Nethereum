using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.XUnitEthereumClients;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmCapturedStateTests : EvmCapturedStateTestBase
    {
        private readonly EthereumClientIntegrationFixture _fixture;

        public EvmCapturedStateTests(EthereumClientIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        protected override EthereumClientIntegrationFixture GetFixture() => _fixture;

        [Fact]
        public async void ShouldSimulateOpenSeaTransaction()
        {
            var (program, state) = await RunTransaction("0x955db73fb9b2c3dc733e2adcecabef4f7bce3f9de1895bd73643b4a656e43d85");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateOpenSeaTransferHelperTransaction()
        {
            var (program, state) = await RunTransaction("0x2ab5b72b40d8d004d40258e7a8296d512a0d805c1f73603ddba4069a80e40946");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateCurveRemoveLiquidityTransaction()
        {
            var (program, state) = await RunTransaction("0x763774a4a954d0deccf9d054ed8164cef1e6762a45cdc30457b5c2770c833300");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateUniswapV3Transaction()
        {
            var (program, state) = await RunTransaction(
                "0x6669284f4072af03600f95bc4c1ed3499e1658dab87615cfd03775fea13a82b7",
                ConfigureStateUniswapV3);
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateUniswapCsvTransaction_1()
        {
            var (program, state) = await RunTransaction("0x266e42391db7fc9e57d04a93f604a173799d7cebd9fa4fe498b5afa66981f442");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateUniswapCsvTransaction_2()
        {
            var (program, state) = await RunTransaction("0x7296d8357df16d571a764791889012d3b76728abab80ff3b1c0d888ad08cf909");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateCurveCsvTransaction()
        {
            var (program, state) = await RunTransaction("0xdfa2d8dec68f309b583772f30f5ff20b9043393e4b1740144c6a3972e2c8ca5f");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        [Fact]
        public async void ShouldSimulateRevertTransaction()
        {
            var (program, state) = await RunTransaction("0xf3d2a323110370a4dc72c04c738bf9b45d14b03603ed70372128a3966c54fca6");
            Assert.True(program.ProgramResult.IsRevert);
            Assert.NotNull(program.ProgramResult.GetRevertMessage());
        }

        [Fact(Skip = "Captured state incomplete - Chainlink aggregator 0xfdfd9c85ad200c506cf9e21f1fd8dd01932fbb23 has empty storage. Needs archive node to recapture.")]
        public async void ShouldSimulateAaveBorrowTransaction()
        {
            var (program, state) = await RunTransaction(
                "0xf5cd117cd777e3548871a0679e81f36625c3829cb57058da231a8188ceda3e97",
                ConfigureStateAaveBorrow);

            if (program.ProgramResult.IsRevert)
            {
                var revertMsg = program.ProgramResult.GetRevertMessage();
                File.WriteAllText("revert_message.txt", $"Revert: {revertMsg}\nTrace count: {program.Trace.Count}");
            }

            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        private void ConfigureStateAaveBorrow(ExecutionStateService executionStateService)
        {
            // Fix Chainlink aggregator proxy storage - slot 0x3a contains timestamp
            // Block timestamp: 1767787463 = 0x695E4BC7 (NOT 0x695E45C7!)
            // The captured state has stale timestamp value, needs to match block timestamp
            var chainlinkProxy = executionStateService.CreateOrGetAccountExecutionState("0x6df1C1E379bC5a00a7b4C6e67A203333772f45A8");
            chainlinkProxy.UpsertStorageValue(
                BigInteger.Parse("58"), // slot 0x3a = 58 decimal
                "0000000000000000000000000000000000000000000000000000000695e4bc7".HexToByteArray());

            // Fix Aave Pool storage - reserve data lastUpdateTimestamp
            // The DELEGATECALL to 0xFeD9871... uses Aave Pool's storage
            // Storage key ca6decca...4f = 91561416...751 in decimal
            // Current value has timestamp 0x695e4ba3, needs to be 0x695e4bc7 (block timestamp)
            var aavePool = executionStateService.CreateOrGetAccountExecutionState("0x87870bca3f3fd6335c3f4ce8392d69350b4fa4e2");
            aavePool.UpsertStorageValue(
                BigInteger.Parse("91561416010232123629925681555576948579931216438032375475172572895684270236751"),
                "000000000000000000000800695e4bc70000000000000000000000025c17ca56".HexToByteArray());
        }

        [Fact]
        public async void DebugAaveBorrowFindDivergence()
        {
            var txHash = "0xf5cd117cd777e3548871a0679e81f36625c3829cb57058da231a8188ceda3e97";
            var (program, state) = await RunTransaction(txHash, ConfigureStateAaveBorrow);

            // Debug: Print the timestamp from captured state
            File.WriteAllText("timestamp_debug.txt",
                $"Captured Timestamp: {state.Timestamp}\n" +
                $"Captured Timestamp Hex: 0x{state.Timestamp:X}\n" +
                $"Block Number: {state.BlockNumber}");

            var traceFile = FindTraceFile(txHash);
            var csvContent = File.ReadAllText(traceFile);
            var externalTraces = ExternalTrace.ParseFromEtherscanCsv(csvContent);

            var output = new System.Text.StringBuilder();
            output.AppendLine($"Our trace count: {program.Trace.Count}");
            output.AppendLine($"External trace count: {externalTraces.Count}");

            var minCount = Math.Min(program.Trace.Count, externalTraces.Count);
            int divergenceStep = -1;

            for (int i = 0; i < minCount; i++)
            {
                var ourTrace = program.Trace[i];
                var extTrace = externalTraces[i];
                var ourOp = ourTrace.Instruction?.Instruction.ToString();
                var extOp = extTrace.Op;

                // Normalize opcode names (SHA3 and KECCAK256 are the same opcode 0x20)
                if (ourOp == "KECCAK256") ourOp = "SHA3";
                if (extOp == "KECCAK256") extOp = "SHA3";

                if (extTrace.Pc != ourTrace.Instruction?.Step ||
                    extOp != ourOp ||
                    extTrace.Depth - 1 != ourTrace.Depth)
                {
                    divergenceStep = i;
                    output.AppendLine($"\n=== DIVERGENCE at step {i} ===");
                    output.AppendLine($"External: PC={extTrace.Pc} Op={extTrace.Op} Depth={extTrace.Depth}");
                    output.AppendLine($"Ours:     PC={ourTrace.Instruction?.Step} Op={ourOp} Depth={ourTrace.Depth + 1}");
                    output.AppendLine($"Contract: {ourTrace.CodeAddress}");
                    break;
                }
            }

            if (divergenceStep > 0)
            {
                output.AppendLine($"\n=== Steps before divergence ({divergenceStep - 20} to {divergenceStep + 5}) ===");
                for (int i = Math.Max(0, divergenceStep - 20); i < Math.Min(program.Trace.Count, divergenceStep + 5); i++)
                {
                    var t = program.Trace[i];
                    var marker = i == divergenceStep ? ">>> " : "    ";
                    output.AppendLine($"{marker}[{i}] {t.CodeAddress?.Substring(0, 10)}... D:{t.Depth} PC:{t.Instruction?.Step} {t.Instruction?.Instruction}");
                }

                output.AppendLine($"\n=== SLOAD before divergence (step {divergenceStep - 500} to {divergenceStep}) ===");
                for (int i = Math.Max(0, divergenceStep - 500); i < divergenceStep; i++)
                {
                    var t = program.Trace[i];
                    if (t.Instruction?.Instruction.ToString() == "SLOAD" && t.Stack?.Count > 0)
                    {
                        var nextTrace = i + 1 < program.Trace.Count ? program.Trace[i + 1] : null;
                        var loadedValue = nextTrace?.Stack?.Count > 0 ? nextTrace.Stack[0] : "N/A";
                        output.AppendLine($"Step {i}: SLOAD at {t.CodeAddress} Key={t.Stack[0]} => {loadedValue}");
                    }
                }

                output.AppendLine($"\n=== STATICCALL before divergence (step {divergenceStep - 500} to {divergenceStep}) ===");
                for (int i = Math.Max(0, divergenceStep - 500); i < divergenceStep; i++)
                {
                    var t = program.Trace[i];
                    var op = t.Instruction?.Instruction.ToString();
                    if ((op == "STATICCALL" || op == "CALL" || op == "DELEGATECALL") && t.Stack?.Count >= 2)
                    {
                        output.AppendLine($"Step {i}: {op} at D:{t.Depth} to {t.Stack[1]}");
                    }
                }

                output.AppendLine($"\n=== Contract addresses in execution (near divergence) ===");
                var contracts = new HashSet<string>();
                for (int i = Math.Max(0, divergenceStep - 100); i < Math.Min(program.Trace.Count, divergenceStep + 10); i++)
                {
                    var t = program.Trace[i];
                    if (!string.IsNullOrEmpty(t.CodeAddress))
                        contracts.Add($"D:{t.Depth} {t.CodeAddress}");
                }
                foreach (var c in contracts.OrderBy(x => x))
                    output.AppendLine(c);

                output.AppendLine($"\n=== Stack at divergence point ===");
                var divTrace = program.Trace[divergenceStep];
                if (divTrace.Stack != null)
                {
                    for (int s = 0; s < Math.Min(5, divTrace.Stack.Count); s++)
                        output.AppendLine($"  Stack[{s}]: {divTrace.Stack[s]}");
                }

                output.AppendLine($"\n=== STATICCALL results (step {divergenceStep - 200} to {divergenceStep}) ===");
                for (int i = Math.Max(0, divergenceStep - 200); i < divergenceStep; i++)
                {
                    var t = program.Trace[i];
                    var op = t.Instruction?.Instruction.ToString();
                    if (op == "RETURNDATASIZE" || op == "RETURNDATACOPY")
                    {
                        var nextTrace = i + 1 < program.Trace.Count ? program.Trace[i + 1] : null;
                        var stackTop = nextTrace?.Stack?.Count > 0 ? nextTrace.Stack[0] : "N/A";
                        output.AppendLine($"Step {i}: {op} at D:{t.Depth} {t.CodeAddress?.Substring(0, 10)}... => {stackTop}");
                    }
                }

                output.AppendLine($"\n=== Stack at key operations around divergence ===");
                for (int i = Math.Max(0, divergenceStep - 15); i <= divergenceStep; i++)
                {
                    var t = program.Trace[i];
                    var op = t.Instruction?.Instruction.ToString();
                    var stackStr = "";
                    if (t.Stack != null && t.Stack.Count > 0)
                    {
                        stackStr = string.Join(", ", t.Stack.Take(3).Select(s => s.Length > 20 ? s.Substring(s.Length - 16) : s));
                    }
                    output.AppendLine($"[{i}] PC:{t.Instruction?.Step} {op,-12} Stack: {stackStr}");
                }
            }

            File.WriteAllText("aave_divergence.txt", output.ToString());
            Assert.True(divergenceStep < 0, $"Divergence at step {divergenceStep}. See aave_divergence.txt");
        }

        [Fact(Skip = "Captured state incomplete - needs archive node to recapture")]
        public async void ShouldSimulate1inchSwapTransaction()
        {
            var (program, state) = await RunTransaction("0x8e936eb26ac762733f6910daea3bcb248d38112a5afec7c1ec8b5fede2f0e254");
            ValidateLogs(program, state);
            await ValidateAgainstTraces(state.TransactionHash, program);
        }

        private void ConfigureStateUniswapV3(ExecutionStateService executionStateService)
        {
            var x = executionStateService.CreateOrGetAccountExecutionState("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
            x.UpsertStorageValue(BigInteger.Parse("8221335686466422652986625159663000664425034433962318634917087004497611930108"), "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x88e6A0c2dDD26FEEb64F039a2c41296FcB3f5640");
            x.UpsertStorageValue(BigInteger.Parse("0"), "00010002d002d0021b032561000000000000751e61ce7e5521bd06be0572e804".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x88e6A0c2dDD26FEEb64F039a2c41296FcB3f5640");
            x.UpsertStorageValue(BigInteger.Parse("1"), "00000000000000000000000000000000000069e9ce583c34c9378d2bfbdca7c2".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x88e6A0c2dDD26FEEb64F039a2c41296FcB3f5640");
            x.UpsertStorageValue(BigInteger.Parse("547"), "010000000000000001e969dfa10e758077407af4270008d2d9511b47637cccc7".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x88e6A0c2dDD26FEEb64F039a2c41296FcB3f5640");
            x.UpsertStorageValue(BigInteger.Parse("548"), "010000000000000001e9695b4b6ce4c3f8acf8aacd0008d2073d1693637c8a13".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");
            x.UpsertStorageValue("390f6178407c9b8e95802b8659e6df8e34c1e3d4f8d6a49e6132bbcdd937b63a".HexToBigInteger(false), "0000000000000000000000000000000000000000000012badbfadd11621bee72".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
            x.UpsertStorageValue("1f21a62c4538bacf2aabeca410f0fe63151869f172e03c0e00357ba26a341eff".HexToBigInteger(false), "00000000000000000000000000000000000000000000000000001a59f6af31ce".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
            x.UpsertStorageValue("5677c3185d8c751c0e35cf53d6ef0339fe159883cdb7332be8c08b4bb14d8639".HexToBigInteger(false), "000000000000000000000000000000000000000000000000000000030a5d5601".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xa6Cc3C2531FdaA6Ae1A3CA84c2855806728693e8");
            x.UpsertStorageValue("0".HexToBigInteger(false), "00010000b400b40088ff35910000000000000000132f8182f124602fc6a05e3c".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xa6Cc3C2531FdaA6Ae1A3CA84c2855806728693e8");
            x.UpsertStorageValue("0000000000000000000000000000000000000000000000000000000000000002".HexToBigInteger(false), "0000000000000000000000000000000002896a12b23323df7ebf261fc3162a37".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x514910771AF9Ca656af840dff83E8264EcF986CA");
            x.UpsertStorageValue("9422ae262bd5bbe8254768a185116d59ff7f53e5e813d9c0ea3840cf28c230a0".HexToBigInteger(false), "00000000000000000000000000000000000000000000efbdc3fc3ec569174298".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0x514910771AF9Ca656af840dff83E8264EcF986CA");
            x.UpsertStorageValue("a577b5d40dbe170a6ef57d4bc918b3a35dc863863e8ba8f47b22280c3bb0e1b0".HexToBigInteger(false), "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xa6Cc3C2531FdaA6Ae1A3CA84c2855806728693e8");
            x.UpsertStorageValue("0000000000000000000000000000000000000000000000000000000000000090".HexToBigInteger(false), "01000049a500000000000020cd5beea6872254ac05fffdb885edd196637cccd3".HexToByteArray());

            x = executionStateService.CreateOrGetAccountExecutionState("0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");
            x.UpsertStorageValue("2152d1f752d5b88a3178a813eda1508fbde034f11b826cc92dea66732e3d19a1".HexToBigInteger(false), "0000000000000000000000000000000000000000000001fa8af73951ebec5044".HexToByteArray());
        }
    }
}
