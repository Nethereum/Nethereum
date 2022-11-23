using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    public class ExternalTrace
    {
        [JsonProperty("pc")]
        public int Pc { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("gas")]
        public int Gas { get; set; }

        [JsonProperty("gasCost")]
        public int GasCost { get; set; }

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("stack")]
        public List<string> Stack { get; set; }

        [JsonProperty("memory")]
        public List<string> Memory { get; set; }
    }

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmChainTransactionLogsAndTracesTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EvmChainTransactionLogsAndTracesTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldRetrieveUniswapV2TransactionFromChainAndSimulateIt()
        {
            //Uniswap
            await ShouldRetrieveTransactionFromChainSimulateItAndValidateLogs("0xb9f4e6e5c90329a43da70ced8e8974c3fa34e67e32283bfa82778296fa79dd98");

        }

        [Fact]
        public async void ShouldRetrieveOpenSeaTransferHelperTransactionFromChainAndSimulateIt()
        {
            //Open transfer helper
            await ShouldRetrieveTransactionFromChainSimulateItAndValidateLogs("0x2ab5b72b40d8d004d40258e7a8296d512a0d805c1f73603ddba4069a80e40946");

        }

        [Fact]
        public async void ShouldRetrieveCurveRemoveLiquidiyTransactionFromChainAndSimulateIt()
        {
            //Curve remove liquidity
            await ShouldRetrieveTransactionFromChainSimulateItAndValidateLogs("0x763774a4a954d0deccf9d054ed8164cef1e6762a45cdc30457b5c2770c833300");

        }

        [Fact]
        public async void ShouldRetrieveUniswapV3TransactionFromChainAndSimulateIt()
        {
            //Uniswap v3 multicall
            await ShouldRetrieveTransactionFromChainSimulateItAndValidateLogs("0x6669284f4072af03600f95bc4c1ed3499e1658dab87615cfd03775fea13a82b7", ConfigureStateUniswapV3);
        }
        //

        [Fact]
        public async void ShouldRetrieveUniswapV2TransactionFromChainAndValidateTraces()
        {
            //Uniswap // Gas "7f281"
            await RetrieveTransactionFromChainAndCompareToExternalTraces("0xb9f4e6e5c90329a43da70ced8e8974c3fa34e67e32283bfa82778296fa79dd98", "EVM/Traces/0xb9f4e6e5c90329a43da70ced8e8974c3fa34e67e32283bfa82778296fa79dd98.json", "7f281");

        }

        [Fact]
        public async void ShouldRetrieveUniswapV3TransactionFromChainAndValidateTraces()
        {
            //Uniswap v3 multicall
            await RetrieveTransactionFromChainAndCompareToExternalTraces("0x6669284f4072af03600f95bc4c1ed3499e1658dab87615cfd03775fea13a82b7", "EVM/Traces/0x6669284f4072af03600f95bc4c1ed3499e1658dab87615cfd03775fea13a82b7.json", "54532", ConfigureStateUniswapV3);

        }

        public void ConfigureStateUniswapV3(ExecutionStateService executionStateService)
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

        [Fact]
        public async void ShouldRetrieveOpenSeaTransferHelperTransactionFromChainAndValidateTraces()
        {
            //Open transfer helper
            await RetrieveTransactionFromChainAndCompareToExternalTraces("0x2ab5b72b40d8d004d40258e7a8296d512a0d805c1f73603ddba4069a80e40946", "EVM/Traces/0x2ab5b72b40d8d004d40258e7a8296d512a0d805c1f73603ddba4069a80e40946.json", "19e88");

        }

        [Fact]
        public async void ShouldRetrieveCurveRemoveLiquidiyTransactionFromChainAndValidateTraces()
        {
            //Curve remove liquidity
            //Gas "53cc5"
            await RetrieveTransactionFromChainAndCompareToExternalTraces("0x763774a4a954d0deccf9d054ed8164cef1e6762a45cdc30457b5c2770c833300", "EVM/Traces/0x763774a4a954d0deccf9d054ed8164cef1e6762a45cdc30457b5c2770c833300.zip", "53cc5");

        }

        public async Task ShouldRetrieveTransactionFromChainSimulateItAndValidateLogs(string transactionHash, Action<ExecutionStateService> configureState = null)
        {
            //Scenario of complex uniswap clone to run end to end a previous transaction and see logs etc
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
            var txnReceipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(txn.BlockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(txn.To); // runtime code;
            Program program = await ExecuteProgramAsync(web3, txn, block, code, configureState);

            Assert.Equal(txnReceipt.Failed(), program.ProgramResult.IsRevert);
            if (!txnReceipt.Failed())
            {
                Assert.True(program.ProgramResult.Logs.Count == txnReceipt.Logs.Count);
                if (program.ProgramResult.Logs.Count > 0)
                {
                    var receiptLogs = txnReceipt.Logs.ConvertToFilterLog();

                    for (int i = 0; i < program.ProgramResult.Logs.Count; i++)
                    {
                        var simulatorLog = program.ProgramResult.Logs[i];
                        var receiptLog = receiptLogs[i];
                        Assert.True(simulatorLog.Address.IsTheSameHex(receiptLog.Address));
                        Assert.True(simulatorLog.Data.IsTheSameHex(receiptLog.Data));
                        Assert.True(simulatorLog.Topics.Length == receiptLog.Topics.Length);
                        for (int x = 0; x < simulatorLog.Topics.Length; x++)
                        {
                            simulatorLog.Topics[x].ToString().IsTheSameHex(receiptLog.Topics[x].ToString());
                        }
                    }
                }
            }

        }

        public async Task RetrieveTransactionFromChainAndCompareToExternalTraces(string transactionHash, string externalTracePath, string gasValue, Action<ExecutionStateService> configureState = null)
        {
            var json = "";
            if(Path.GetExtension(externalTracePath) == ".zip")
            {
                json = Unzip(externalTracePath);
            }
            else
            {
               json = System.IO.File.ReadAllText(externalTracePath);
            }
            
            var externalTraces = JsonConvert.DeserializeObject<List<ExternalTrace>>(json);

            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var txn = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(txn.BlockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(txn.To); // runtime code;

            Program program = await ExecuteProgramAsync(web3, txn, block, code, configureState);
            if (program.ProgramResult.Exception != null)
            {
                Debug.WriteLine("Program failure, validating traces");
                Debug.WriteLine(program.ProgramResult.Exception.ToString());
            }

            var trace = program.Trace;
            for (int i = 0; i < trace.Count; i++)
            {

                Debug.WriteLine("Validating test step");
                Debug.WriteLine(trace[i].VMTraceStep);
                Debug.WriteLine(trace[i].Instruction.Instruction.ToString());
                Debug.WriteLine(trace[i].Instruction.Value.ToString());
                var traceStep = trace[i];
                var externalTrace = externalTraces[i];

                Assert.Equal(traceStep.Depth, externalTrace.Depth - 1);
                var instructionName = traceStep.Instruction.Instruction.ToString();
                if (instructionName == "KECCAK256" && externalTrace.Op == "SHA3") instructionName = "SHA3";
                Assert.Equal(instructionName, externalTrace.Op);
                Assert.Equal(traceStep.Stack.Count, externalTrace.Stack.Count);

                var reverseStack = externalTrace.Stack.ToArray().Reverse().ToArray();

                for (int x = 0; x < reverseStack.Length; x++)
                {
                    var stackElementTrace = traceStep.Stack[x];
                    var stackElementTraceTest = reverseStack[x];

                    if (string.IsNullOrEmpty(gasValue) || stackElementTrace.ToHexCompact() != gasValue) /// default gas lets ignore it GAS gets called and added to the stack
                    {

                        Assert.Equal(stackElementTrace.ToHexCompact(),
                            stackElementTraceTest.ToHexCompact());
                    }
                }

                var indexOfEmptyMemory = externalTrace.Memory.Count;
                if (string.IsNullOrEmpty(traceStep.Memory))
                {
                    indexOfEmptyMemory = 0;
                }
                else
                {
                    if (externalTrace.Memory.Count > traceStep.MemoryAsArray.Count)
                    {
                        indexOfEmptyMemory = traceStep.MemoryAsArray.Count;
                    }
                }


                for (int x = 0; x < traceStep.MemoryAsArray.Count; x++)
                {
                    var stackElementTrace = traceStep.MemoryAsArray[x];

                    if (stackElementTrace.Length != 64)
                    {
                        stackElementTrace = stackElementTrace.PadRight(64, '0');
                    }
                    var stackElementTraceTest = externalTrace.Memory[x];
                    Assert.Equal(stackElementTrace.ToHexCompact(),
                        stackElementTraceTest.ToHexCompact());
                }

                for (int x = indexOfEmptyMemory; x < externalTrace.Memory.Count; x++)
                {
                    var stackElementTraceTest = externalTrace.Memory[x];
                    Assert.True(string.IsNullOrEmpty(stackElementTraceTest.ToHexCompact()));
                }
            }



        }

        public static async Task<Program> ExecuteProgramAsync(Web3.Web3 web3, Transaction txn, BlockWithTransactionHashes block, string code, Action<ExecutionStateService> configureState = null)
        {
            //var instructions = ProgramInstructionsUtils.GetProgramInstructions(code);
            var txnInput = txn.ConvertToTransactionInput();
            txnInput.ChainId = new HexBigInteger(1);
            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(new HexBigInteger(txn.BlockNumber.Value - 1)));

            var executionStateService = new ExecutionStateService(nodeDataService);
            if (configureState != null)
            {
                configureState(executionStateService);
            }

            var programContext = new ProgramContext(txnInput, executionStateService, null, (long)txn.BlockNumber.Value, (long)block.Timestamp.Value);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();

            try
            {
                program = await evmSimulator.ExecuteAsync(program, 0, 0, true);
                return program;

            }
            catch (Exception ex)
            {
                program.ProgramResult.Exception = ex;
                return program;
            }
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
