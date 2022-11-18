using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class VMJsonTests
    {

        public class AccountStorage
        {
            [JsonProperty("code")]
            public string Code { get; set; }

            [JsonProperty("balance")]
            public string Balance { get; set; }

            [JsonProperty("storage")]
            public Dictionary<string, string> Storage { get; set; }

            [JsonProperty("nonce")]
            public string Nonce { get; set; }
        }

        public class Berlin
        {
            [JsonProperty("expect")]
            public string Expect { get; set; }

            [JsonProperty("trace")]
            public List<Trace> Trace { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("transaction")]
            public Transaction Transaction { get; set; }
        }

        public class Env
        {
            [JsonProperty("currentBaseFee")]
            public string CurrentBaseFee { get; set; }

            [JsonProperty("previousHash")]
            public string PreviousHash { get; set; }

            [JsonProperty("currentTimestamp")]
            public string CurrentTimestamp { get; set; }

            [JsonProperty("currentCoinbase")]
            public string CurrentCoinbase { get; set; }

            [JsonProperty("currentNumber")]
            public string CurrentNumber { get; set; }

            [JsonProperty("currentDifficulty")]
            public string CurrentDifficulty { get; set; }

            [JsonProperty("currentGasLimit")]
            public string CurrentGasLimit { get; set; }
        }

        public class Pre
        {
            public Dictionary<string, AccountStorage> AccountsStorage { get; set; }
        }

        public class TestFeature
        {
            public Dictionary<string, TestScenario> TestScenarios { get; set; }
        }

        public class TestScenario
        {
            [JsonProperty("pre")]
            public Dictionary<string, AccountStorage> PreAccountsStorage { get; set; }

            [JsonProperty("tests")]
            public Tests Tests { get; set; }

            [JsonProperty("env")]
            public Env Env { get; set; }
        }



        public class Tests
        {
            [JsonProperty("Berlin")]
            public List<Berlin> Berlin { get; set; }
        }

        public class Trace
        {
            [JsonProperty("op")]
            public int Op { get; set; }

            [JsonProperty("stack")]
            public List<string> Stack { get; set; }

            [JsonProperty("pc")]
            public int Pc { get; set; }

            [JsonProperty("depth")]
            public int Depth { get; set; }

            [JsonProperty("stackSize")]
            public int StackSize { get; set; }

            [JsonProperty("gas")]
            public long Gas { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }

            [JsonProperty("return")]
            public string Return { get; set; }
        }

        public class Transaction
        {
            [JsonProperty("gasLimit")]
            public string GasLimit { get; set; }

            [JsonProperty("input")]
            public string Input { get; set; }

            [JsonProperty("sender")]
            public string Sender { get; set; }

            [JsonProperty("to")]
            public string To { get; set; }

            [JsonProperty("nonce")]
            public string Nonce { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }

            [JsonProperty("gasPrice")]
            public string GasPrice { get; set; }
        }


        public async Task RunTestsFromFolder(string folder, string[] ignoredTests = null, string[] includedTests = null)
        {
            var files = Directory.GetFiles(folder, "*.json");

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (ignoredTests != null && ignoredTests.Contains(fileName)) { continue; }
                if (includedTests != null && !includedTests.Contains(fileName)) { continue; }

                var json = System.IO.File.ReadAllText(file);
                var testScenarios = JsonConvert.DeserializeObject<Dictionary<string, TestScenario>>(json);

                foreach (var testScenarioItem in testScenarios)
                {

                    var scenarioName = testScenarioItem.Key;
                    var scenario = testScenarioItem.Value;
                    foreach (var test in scenario.Tests.Berlin)
                    {
                        //Excluding errors and gas 
                        //if(test.Id == "Berlin_0_15_0") { continue; }
                        //if (test.Id != "Berlin_0_11_0") { continue; }

                        if (test.Trace.Exists(x => !string.IsNullOrEmpty(x.Error) || x.Op == 90)) { continue; }
                        Debug.WriteLine(scenarioName);
                        Debug.WriteLine(test.Id);
                        var executionState = new ExecutionStateService(null);
                        foreach (var scenarioAccountStorageItem in scenario.PreAccountsStorage)
                        {
                            var scenarioAccountStorage = scenarioAccountStorageItem.Value;
                            var accountExecutionState = executionState.CreateOrGetAccountExecutionState(scenarioAccountStorageItem.Key);
                            accountExecutionState.Code = scenarioAccountStorage.Code.HexToByteArray();
                            accountExecutionState.Balance.SetInitialChainBalance(scenarioAccountStorage.Balance.HexToBigInteger(false));
                            accountExecutionState.Nonce = scenarioAccountStorage.Nonce.HexToBigInteger(false);
                            foreach (var storageItem in scenarioAccountStorage.Storage)
                            {
                                accountExecutionState.UpsertStorageValue(storageItem.Key.HexToBigInteger(false), storageItem.Value.HexToByteArray());
                            }
                        }

                        var env = scenario.Env;

                        var transaction = new TransactionInput();
                        transaction.From = test.Transaction.Sender;
                        transaction.To = test.Transaction.To;
                        transaction.Value = new Hex.HexTypes.HexBigInteger(test.Transaction.Value);
                        transaction.Nonce = new Hex.HexTypes.HexBigInteger(test.Transaction.Nonce);
                        transaction.Gas = new Hex.HexTypes.HexBigInteger(test.Transaction.GasLimit);
                        transaction.GasPrice = new Hex.HexTypes.HexBigInteger(test.Transaction.GasPrice);
                        transaction.Data = test.Transaction.Input;



                        var programContext = new ProgramContext(transaction, executionState, null, blockNumber: (long)env.CurrentNumber.HexToBigInteger(false) - 1,
                            timestamp: (long)env.CurrentTimestamp.HexToBigInteger(false), coinbase: env.CurrentCoinbase, baseFee: (long)env.CurrentBaseFee.HexToBigInteger(false));
                        programContext.Difficulty = env.CurrentDifficulty.HexToBigInteger(false);
                        programContext.GasLimit = env.CurrentGasLimit.HexToBigInteger(false);

                        var byteCode = await executionState.GetCodeAsync(test.Transaction.To);
                        var program = new Program(byteCode, programContext);
                        var evmSimulator = new EVMSimulator();
                        var trace = await evmSimulator.ExecuteAsync(program);

                        //contains the result
                        //Assert.True(trace.Count + 1 == test.Trace.Count);

                        try
                        {
                            Debug.WriteLine(scenarioName);
                            Debug.WriteLine(test.Id);

                            for (int i = 0; i < trace.Count; i++)
                            {

                                Debug.WriteLine("Validating test step");
                                Debug.WriteLine(trace[i].VMTraceStep);
                                Debug.WriteLine(trace[i].Instruction.Instruction.ToString());
                                Debug.WriteLine(trace[i].Instruction.Value.ToString());
                                var traceStep = trace[i];
                                var traceTestStep = test.Trace[i];
                                //forced stops are not traced
                                if (traceTestStep.Op == 0 && (int)traceStep.Instruction.Instruction.Value == 0)
                                {
                                    if (traceTestStep.Depth > traceStep.Depth)
                                    {
                                        traceTestStep = test.Trace[i + 1];
                                    }
                                }
                                Assert.Equal(traceStep.Depth, traceTestStep.Depth);
                                Assert.Equal((int)traceStep.Instruction.Instruction.Value, traceTestStep.Op);
                                Assert.Equal(traceStep.Stack.Count, traceTestStep.StackSize);
                                var reverseStack = traceTestStep.Stack.ToArray().Reverse().ToArray();

                                //Assert.Equal(traceStep.Stack.Count, reverseStack.Length);

                                for (int x = 0; x < reverseStack.Length; x++)
                                {
                                    var stackElementTrace = traceStep.Stack[x];
                                    var stackElementTraceTest = reverseStack[x];
                                    Assert.Equal(stackElementTrace.ToHexCompact(),
                                        stackElementTraceTest.ToHexCompact());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            var x = ex;
                            throw ex;
                        }

                        var resultHex = string.Empty;
                        if (program.ProgramResult.Result != null)
                        {
                            resultHex = program.ProgramResult.Result.ToHexCompact();
                        }

                        Assert.Equal(resultHex, test.Trace.Last().Return.ToHexCompact());
                    }
                }
            }
        }

        [Fact]
        public async Task TestvmBitwiseLogicOperation()
        {
            await RunTestsFromFolder("Tests/VMTests/vmBitwiseLogicOperation", null, null);
        }

        //This have gas so cannot be used yet.
        //[Fact]
        //public async Task TestvmIOandFlowOperations()
        //{
        //    var excluded = new string[] { "codecopy", "jump", "jumpi"};
        //    await RunTestsFromFolder("Tests/VMTests/vmIOandFlowOperations", excluded);
        //}


        //[Fact]
        //public async Task TestvmTests()
        //{
        //    var excluded = new string[] { "calldatacopy" };
        //    await RunTestsFromFolder("Tests/VMTests/vmTests", excluded);
        //}


        [Fact]
        public async Task TestvmArithmeticTest()
        {
            //to check arith, sdiv, sub, signextend
            await RunTestsFromFolder("Tests/VMTests/vmArithmeticTest",
            new string[] {
                "signextend", "arith", "twoOps" });

        }

    }
}