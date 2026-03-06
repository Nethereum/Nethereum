using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.AppChain.Genesis;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.WorldFactory;
using Nethereum.Mud.Contracts.WorldFactory.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class MudWorldDeploymentTests : IAsyncLifetime
    {
        private DevChainNode? _devChain;
        private Web3.Web3? _web3;
        private WorldFactoryDeployService _worldFactoryService = new WorldFactoryDeployService();
        private WorldFactoryContractAddresses? _factoryAddresses;
        private string _worldAddress = "";
        private string _create2ProxyAddress = "";
        private readonly ITestOutputHelper _output;

        private const string DeployerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _deployerAddress;
        private const string Salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

        public MudWorldDeploymentTests(ITestOutputHelper output)
        {
            _output = output;
            var deployerKey = new Nethereum.Signer.EthECKey(DeployerPrivateKey);
            _deployerAddress = deployerKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 100_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("1000000000000000000000000")
            };
            _devChain = new DevChainNode(config);
            await _devChain.StartAsync(new[] { _deployerAddress });

            // Pre-deploy the CREATE2 factory at genesis
            await _devChain.SetCodeAsync(
                Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS,
                Create2FactoryGenesisBuilder.CREATE2_FACTORY_BYTECODE.HexToByteArray());
            _create2ProxyAddress = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;

            var account = new Account(DeployerPrivateKey, 1337);
            var rpcClient = new DevChainRpcClient(_devChain, 1337);
            _web3 = new Web3.Web3(account, rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;
        }

        public Task DisposeAsync()
        {
            _devChain?.Dispose();
            _devChain = null;
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Deploy_Create2ProxyWorks()
        {
            var create2Service = _web3!.Eth.Create2DeterministicDeploymentProxyService;

            // Verify the pre-deployed CREATE2 factory is available
            Assert.NotNull(_create2ProxyAddress);
            Assert.StartsWith("0x", _create2ProxyAddress);
            Assert.Equal(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS.ToLowerInvariant(), _create2ProxyAddress.ToLowerInvariant());

            var hasProxy = await create2Service.HasProxyBeenDeployedAsync(_create2ProxyAddress);
            Assert.True(hasProxy);
        }

        [Fact]
        public async Task Deploy_WorldFactoryDeployed()
        {
            await DeployCreate2ProxyAsync();

            _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, _create2ProxyAddress, Salt);

            Assert.NotNull(_factoryAddresses);
            Assert.NotNull(_factoryAddresses.WorldFactoryAddress);
            Assert.StartsWith("0x", _factoryAddresses.WorldFactoryAddress);

            var code = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.WorldFactoryAddress);
            Assert.NotEmpty(code);
        }

        [Fact]
        public async Task Deploy_SystemDependenciesDeployed()
        {
            await DeployCreate2ProxyAsync();

            _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, _create2ProxyAddress, Salt);

            Assert.NotNull(_factoryAddresses.AccessManagementSystemAddress);
            Assert.NotNull(_factoryAddresses.BalanceTransferSystemAddress);
            Assert.NotNull(_factoryAddresses.BatchCallSystemAddress);
            Assert.NotNull(_factoryAddresses.RegistrationSystemAddress);
            Assert.NotNull(_factoryAddresses.InitModuleAddress);

            var accessMgmtCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.AccessManagementSystemAddress);
            Assert.NotEmpty(accessMgmtCode);

            var balanceCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.BalanceTransferSystemAddress);
            Assert.NotEmpty(balanceCode);

            var batchCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.BatchCallSystemAddress);
            Assert.NotEmpty(batchCode);

            var regCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.RegistrationSystemAddress);
            Assert.NotEmpty(regCode);

            var initCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.InitModuleAddress);
            Assert.NotEmpty(initCode);
        }

        [Fact]
        public async Task World_Created()
        {
            await DeployFullWorldAsync();

            Assert.NotNull(_worldAddress);
            Assert.StartsWith("0x", _worldAddress);

            var code = await _web3!.Eth.GetCode.SendRequestAsync(_worldAddress);
            Assert.NotEmpty(code);
        }

        [Fact]
        public async Task World_HasCorrectStoreVersion()
        {
            await DeployFullWorldAsync();

            var worldService = new WorldService(_web3!, _worldAddress);
            var storeVersion = await worldService.StoreVersionQueryAsync();

            Assert.NotNull(storeVersion);
            Assert.True(storeVersion.Length > 0);
        }

        [Fact]
        public async Task World_CreatorIsAdmin()
        {
            await DeployFullWorldAsync();

            var worldService = new WorldService(_web3!, _worldAddress);
            var creator = await worldService.CreatorQueryAsync();

            // MUD World uses msg.sender for creator in constructor. When deployed via WorldFactory,
            // msg.sender is the WorldFactory contract, not the original deployer (tx.origin).
            Assert.Equal(_factoryAddresses!.WorldFactoryAddress.ToLowerInvariant(), creator.ToLowerInvariant());
        }

        [Fact]
        public async Task World_DeployerCanCallWorld()
        {
            await DeployFullWorldAsync();

            var worldService = new WorldService(_web3!, _worldAddress);

            var creator = await worldService.CreatorQueryAsync();

            Assert.NotNull(creator);
        }

        [Fact]
        public async Task WorldFactory_MultipleWorldsCanBeCreated()
        {
            await DeployCreate2ProxyAsync();
            _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, _create2ProxyAddress, Salt);

            var salt1 = "0x0000000000000000000000000000000000000000000000000000000000000001";
            var salt2 = "0x0000000000000000000000000000000000000000000000000000000000000002";

            var worldEvent1 = await _worldFactoryService.DeployWorldAsync(_web3!, salt1, _factoryAddresses);
            var worldEvent2 = await _worldFactoryService.DeployWorldAsync(_web3!, salt2, _factoryAddresses);

            Assert.NotEqual(worldEvent1.NewContract, worldEvent2.NewContract);

            var code1 = await _web3!.Eth.GetCode.SendRequestAsync(worldEvent1.NewContract);
            var code2 = await _web3!.Eth.GetCode.SendRequestAsync(worldEvent2.NewContract);

            Assert.NotEmpty(code1);
            Assert.NotEmpty(code2);
        }

        [Fact]
        public async Task WorldFactory_DeterministicAddresses()
        {
            await DeployCreate2ProxyAsync();
            _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, _create2ProxyAddress, Salt);

            var salt1 = "0x0000000000000000000000000000000000000000000000000000000000000003";

            var worldEvent1 = await _worldFactoryService.DeployWorldAsync(_web3!, salt1, _factoryAddresses);

            await Assert.ThrowsAsync<Nethereum.Contracts.SmartContractCustomErrorRevertException>(async () =>
            {
                await _worldFactoryService.DeployWorldAsync(_web3!, salt1, _factoryAddresses);
            });
        }

        [Fact(Skip = "Memory intensive - traces millions of opcodes")]
        public async Task Debug_DeployWorld_ReceiptDetails()
        {
            await DeployCreate2ProxyAsync();
            _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, _create2ProxyAddress, Salt);

            _output.WriteLine($"WorldFactory Address: {_factoryAddresses.WorldFactoryAddress}");
            _output.WriteLine($"InitModule Address: {_factoryAddresses.InitModuleAddress}");
            _output.WriteLine($"AccessManagement Address: {_factoryAddresses.AccessManagementSystemAddress}");
            _output.WriteLine($"BalanceTransfer Address: {_factoryAddresses.BalanceTransferSystemAddress}");
            _output.WriteLine($"BatchCall Address: {_factoryAddresses.BatchCallSystemAddress}");
            _output.WriteLine($"Registration Address: {_factoryAddresses.RegistrationSystemAddress}");

            var worldFactoryCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.WorldFactoryAddress);
            _output.WriteLine($"WorldFactory Code Length: {worldFactoryCode?.Length ?? 0}");

            var initModuleCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.InitModuleAddress);
            _output.WriteLine($"InitModule Code Length: {initModuleCode?.Length ?? 0}");

            var accessMgmtCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.AccessManagementSystemAddress);
            _output.WriteLine($"AccessManagement Code Length: {accessMgmtCode?.Length ?? 0}");

            var balanceCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.BalanceTransferSystemAddress);
            _output.WriteLine($"BalanceTransfer Code Length: {balanceCode?.Length ?? 0}");

            var batchCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.BatchCallSystemAddress);
            _output.WriteLine($"BatchCall Code Length: {batchCode?.Length ?? 0}");

            var regCode = await _web3!.Eth.GetCode.SendRequestAsync(_factoryAddresses.RegistrationSystemAddress);
            _output.WriteLine($"Registration Code Length: {regCode?.Length ?? 0}");

            var worldFactoryService = new WorldFactoryService(_web3!, _factoryAddresses.WorldFactoryAddress);

            var storedInitModule = await worldFactoryService.InitModuleQueryAsync();
            _output.WriteLine($"WorldFactory stored InitModule: {storedInitModule}");

            var worldSalt = "0x0000000000000000000000000000000000000000000000000000000000000099";

            try
            {
                var deployFunc = new Nethereum.Mud.Contracts.WorldFactory.ContractDefinition.DeployWorldFunction { Salt = worldSalt.HexToByteArray() };
                var callData = deployFunc.GetCallData().ToHex(true);
                _output.WriteLine($"deployWorld callData: {callData}");
                _output.WriteLine($"deployWorld function selector (first 10 chars): {callData.Substring(0, 10)}");

                var callResult = await _web3!.Eth.Transactions.Call.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
                {
                    From = _deployerAddress,
                    To = _factoryAddresses.WorldFactoryAddress,
                    Data = callData
                });
                _output.WriteLine($"deployWorld call result: {callResult}");

                // Add EVM tracing to understand what's happening
                var traceInput = new Nethereum.RPC.Eth.DTOs.CallInput
                {
                    From = _deployerAddress,
                    To = _factoryAddresses.WorldFactoryAddress,
                    Data = callData,
                    Gas = new Nethereum.Hex.HexTypes.HexBigInteger(30_000_000)
                };
                var trace = await _devChain!.TraceCallAsync(traceInput);
                _output.WriteLine($"Trace Gas: {trace.Gas}");
                _output.WriteLine($"Trace Failed: {trace.Failed}");
                _output.WriteLine($"Trace ReturnValue: {trace.ReturnValue}");
                _output.WriteLine($"Trace StructLogs Count: {trace.StructLogs?.Count ?? 0}");

                // Show first 20 and last 20 opcodes
                if (trace.StructLogs != null && trace.StructLogs.Count > 0)
                {
                    _output.WriteLine($"First 30 opcodes:");
                    for (int i = 0; i < Math.Min(30, trace.StructLogs.Count); i++)
                    {
                        var log = trace.StructLogs[i];
                        _output.WriteLine($"  [{log.Pc}] {log.Op} (depth={log.Depth})");
                    }

                    // Show CREATE/CALL transitions with stack details
                    _output.WriteLine($"Looking for CREATE/CALL opcodes:");
                    for (int i = 0; i < trace.StructLogs.Count; i++)
                    {
                        var log = trace.StructLogs[i];
                        if (log.Op == "CREATE" || log.Op == "CREATE2" || log.Op == "CALL" ||
                            log.Op == "DELEGATECALL" || log.Op == "STATICCALL" ||
                            log.Op == "RETURN" || log.Op == "STOP" || log.Op == "REVERT")
                        {
                            _output.WriteLine($"  [{i}] PC={log.Pc} {log.Op} (depth={log.Depth})");
                            // For CREATE2, stack should have: value, offset, length, salt (from bottom to top)
                            if (log.Stack != null && log.Stack.Count > 0)
                            {
                                _output.WriteLine($"    Stack (top to bottom, first 6):");
                                for (int j = 0; j < Math.Min(6, log.Stack.Count); j++)
                                {
                                    var stackVal = log.Stack[j];
                                    var hexVal = stackVal?.ToString() ?? "null";
                                    // Parse to see actual value
                                    if (hexVal.StartsWith("0x") && hexVal.Length <= 18)
                                    {
                                        try { _output.WriteLine($"      [{j}] {hexVal} = {Convert.ToInt64(hexVal, 16)}"); }
                                        catch { _output.WriteLine($"      [{j}] {hexVal}"); }
                                    }
                                    else
                                    {
                                        _output.WriteLine($"      [{j}] {(hexVal.Length > 20 ? hexVal.Substring(0, 20) + "..." : hexVal)}");
                                    }
                                }
                            }
                        }
                    }

                    if (trace.StructLogs.Count > 30)
                    {
                        _output.WriteLine($"...");
                        _output.WriteLine($"Last 30 opcodes:");
                        int start = Math.Max(0, trace.StructLogs.Count - 30);
                        for (int i = start; i < trace.StructLogs.Count; i++)
                        {
                            var log = trace.StructLogs[i];
                            _output.WriteLine($"  [{log.Pc}] {log.Op} (depth={log.Depth})");
                        }
                    }

                    // Check memory-related opcodes before CREATE2
                    _output.WriteLine("=== Memory-related opcodes ===");
                    for (int i = 0; i < trace.StructLogs.Count && i < 318; i++)
                    {
                        var log = trace.StructLogs[i];
                        if (log.Op == "CODECOPY" || log.Op == "EXTCODECOPY" ||
                            log.Op == "MSTORE" || log.Op == "MSTORE8" || log.Op == "MCOPY" ||
                            log.Op == "CALLDATACOPY" || log.Op == "CREATE2")
                        {
                            _output.WriteLine($"  [{i}] PC={log.Pc} {log.Op} MemSize={log.MemSize}");
                        }
                    }

                    // Check memory at CREATE2 opcode
                    _output.WriteLine("=== Checking CREATE2 Memory ===");
                    for (int i = 0; i < trace.StructLogs.Count; i++)
                    {
                        var log = trace.StructLogs[i];
                        if (log.Op == "CREATE2")
                        {
                            _output.WriteLine($"CREATE2 at trace index {i}:");
                            _output.WriteLine($"  MemSize: {log.MemSize}");
                            int requiredBytes = 224 + 21343;
                            _output.WriteLine($"  Required bytes (offset+length): {requiredBytes}");
                            _output.WriteLine($"  Memory sufficient: {log.MemSize >= requiredBytes}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"deployWorld call error: {ex.Message}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Check World bytecode that would be used for CREATE2
            _output.WriteLine($"Checking World bytecode...");
            var worldBytecode = Nethereum.Mud.Contracts.World.ContractDefinition.WorldDeploymentBase.BYTECODE;
            _output.WriteLine($"World BYTECODE length: {worldBytecode?.Length ?? 0}");

            // Parse World bytecode to see how many instructions
            if (!string.IsNullOrEmpty(worldBytecode))
            {
                var worldByteArray = worldBytecode.HexToByteArray();
                _output.WriteLine($"World bytecode first 20 bytes: {worldByteArray.Take(20).ToArray().ToHex()}");
                _output.WriteLine($"World bytecode bytes 120-130: {worldByteArray.Skip(120).Take(10).ToArray().ToHex()}");

                var instructions = Nethereum.EVM.ProgramInstructionsUtils.GetProgramInstructions(worldByteArray);
                _output.WriteLine($"World bytecode parsed instructions count: {instructions.Count}");

                // Show instruction at PC ~122
                var nearPC122 = instructions.Where(x => x.Step >= 115 && x.Step <= 130).ToList();
                _output.WriteLine($"Instructions near PC 122:");
                foreach (var instr in nearPC122)
                {
                    _output.WriteLine($"  PC={instr.Step} {instr.Instruction}");
                }
            }

            var deployWorldFunction = new Nethereum.Mud.Contracts.WorldFactory.ContractDefinition.DeployWorldFunction
            {
                Salt = worldSalt.HexToByteArray(),
                Gas = 30_000_000
            };
            var receipt = await worldFactoryService.ContractHandler.SendRequestAndWaitForReceiptAsync(deployWorldFunction);

            _output.WriteLine($"Receipt Status: {receipt.Status?.Value}");
            _output.WriteLine($"Receipt GasUsed: {receipt.GasUsed?.Value}");
            _output.WriteLine($"Receipt BlockNumber: {receipt.BlockNumber?.Value}");
            _output.WriteLine($"Receipt TransactionHash: {receipt.TransactionHash}");
            _output.WriteLine($"Receipt ContractAddress: {receipt.ContractAddress}");

            var logsArray = receipt.Logs;
            var logsCount = logsArray?.Length ?? 0;
            _output.WriteLine($"Receipt Logs Count: {logsCount}");
            _output.WriteLine($"Receipt Logs Type: {receipt.Logs?.GetType().FullName ?? "null"}");

            if (logsArray != null && logsCount > 0)
            {
                for (int i = 0; i < logsCount; i++)
                {
                    var log = logsArray[i];
                    _output.WriteLine($"Log[{i}] Address: {log.Address}");
                    _output.WriteLine($"Log[{i}] Topics: {string.Join(", ", log.Topics ?? Array.Empty<object>())}");
                    _output.WriteLine($"Log[{i}] Data: {log.Data}");
                }
            }

            var events = receipt.DecodeAllEvents<WorldDeployedEventDTO>();
            _output.WriteLine($"WorldDeployed Events Count: {events.Count}");

            Assert.True(logsCount > 0, "Receipt should have logs");
            Assert.True(events.Count > 0, "Should have WorldDeployed event");
        }

        private async Task DeployCreate2ProxyAsync()
        {
            // CREATE2 factory is pre-deployed at genesis - just verify it's available
            var create2Service = _web3!.Eth.Create2DeterministicDeploymentProxyService;
            var hasProxy = await create2Service.HasProxyBeenDeployedAsync(_create2ProxyAddress);
            if (!hasProxy)
            {
                throw new Exception($"CREATE2 factory not found at {_create2ProxyAddress}");
            }
        }

        private async Task DeployFullWorldAsync()
        {
            await DeployCreate2ProxyAsync();

            if (_factoryAddresses == null)
            {
                _factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                    _web3!, _create2ProxyAddress, Salt);
            }

            if (string.IsNullOrEmpty(_worldAddress))
            {
                var worldSalt = "0x0000000000000000000000000000000000000000000000000000000000000001";
                var worldEvent = await _worldFactoryService.DeployWorldAsync(_web3!, worldSalt, _factoryAddresses);
                _worldAddress = worldEvent.NewContract;
            }
        }
    }
}
