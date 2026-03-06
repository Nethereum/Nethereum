using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.Contracts;
using Nethereum.CoreChain.Rpc;
using Nethereum.Util;

using NethereumAccountExecuteFunction = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SignatureExtensions = Nethereum.Model.SignatureExtensions;
using DevChainRpcClientLocal = Nethereum.DevChain.DevChainRpcClient;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Bootstrap
{
    public class BasicDevChainRpcTests
    {
        [Fact]
        public async Task BasicRpcThroughDevChainRpcClient_ChainId_Works()
        {
            var config = new DevChainConfig
            {
                ChainId = 31337,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            var node = new DevChainNode(config);
            var operatorAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
            await node.StartAsync(new[] { operatorAddress }, BigInteger.Parse("100000000000000000000000"));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var services = new EmptyServiceProvider();
            var context = new RpcContext(node, 31337, services);
            var dispatcher = new RpcDispatcher(registry, context);

            var request = new RpcRequestMessage(1, "eth_chainId");
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var rpcClient = new DevChainRpcClientLocal(dispatcher);
            var operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            var account = new Account(operatorPrivateKey, 31337);
            var web3 = new Web3.Web3(account, rpcClient);

            var chainId = await web3.Eth.ChainId.SendRequestAsync();
            Assert.Equal(31337, (int)chainId.Value);
        }

        [Fact]
        public async Task BasicRpcThroughDevChainRpcClient_GetBalance_Works()
        {
            var config = new DevChainConfig
            {
                ChainId = 31337,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            var node = new DevChainNode(config);
            var operatorAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
            await node.StartAsync(new[] { operatorAddress }, BigInteger.Parse("100000000000000000000000"));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var services = new EmptyServiceProvider();
            var context = new RpcContext(node, 31337, services);
            var dispatcher = new RpcDispatcher(registry, context);

            var rpcClient = new DevChainRpcClientLocal(dispatcher);
            var operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            var account = new Account(operatorPrivateKey, 31337);
            var web3 = new Web3.Web3(account, rpcClient);

            var balance = await web3.Eth.GetBalance.SendRequestAsync(operatorAddress);
            Assert.True(balance.Value > 0, "Balance should be positive");
        }
    }

    [Collection(AppChainE2EFixture.COLLECTION_NAME)]
    public class AppChainBootstrapTests
    {
        private readonly AppChainE2EFixture _fixture;

        public AppChainBootstrapTests(AppChainE2EFixture fixture)
        {
            _fixture = fixture;
        }


        [Fact]
        public async Task UC1_1_LaunchFreshAppChain_CreatesGenesisAndAcceptsRpc()
        {
            var chainId = await _fixture.Web3.Eth.ChainId.SendRequestAsync();
            Assert.Equal(AppChainE2EFixture.CHAIN_ID, (int)chainId.Value);

            var blockNumber = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            Assert.True(blockNumber.Value >= 0, "Block number should be non-negative");

            var operatorBalance = await _fixture.GetBalanceAsync(_fixture.OperatorAccount.Address);
            Assert.True(operatorBalance > 0, "Operator account should be prefunded");
        }

        [Fact]
        public async Task UC1_2_DeployAAContractSuite_AllContractsDeployed()
        {
            Assert.NotNull(_fixture.EntryPointService);
            Assert.NotNull(_fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(_fixture.EntryPointService.ContractAddress));

            Assert.NotNull(_fixture.AccountRegistryService);
            Assert.NotNull(_fixture.AccountRegistryService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(_fixture.AccountRegistryService.ContractAddress));

            Assert.NotNull(_fixture.AccountFactoryService);
            Assert.NotNull(_fixture.AccountFactoryService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(_fixture.AccountFactoryService.ContractAddress));

            Assert.NotNull(_fixture.SponsoredPaymasterService);
            Assert.NotNull(_fixture.SponsoredPaymasterService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(_fixture.SponsoredPaymasterService.ContractAddress));

            var entryPointCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(
                _fixture.EntryPointService.ContractAddress);
            Assert.True(entryPointCode.Length > 2, "EntryPoint should have deployed bytecode");

            var registryCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(
                _fixture.AccountRegistryService.ContractAddress);
            Assert.True(registryCode.Length > 2, "AccountRegistry should have deployed bytecode");

            var factoryCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(
                _fixture.AccountFactoryService.ContractAddress);
            Assert.True(factoryCode.Length > 2, "AccountFactory should have deployed bytecode");

            var paymasterCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(
                _fixture.SponsoredPaymasterService.ContractAddress);
            Assert.True(paymasterCode.Length > 2, "SponsoredPaymaster should have deployed bytecode");
        }

        [Fact]
        public async Task UC1_3_ConfigureBundlerService_BundlerOperational()
        {
            Assert.NotNull(_fixture.BundlerService);

            var entryPoints = await _fixture.BundlerService.SupportedEntryPointsAsync();
            Assert.NotNull(entryPoints);
            Assert.Single(entryPoints);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLowerInvariant(),
                entryPoints[0].ToLowerInvariant());

            var chainId = await _fixture.BundlerService.ChainIdAsync();
            Assert.Equal(AppChainE2EFixture.CHAIN_ID, (int)chainId);

            var stats = await _fixture.BundlerService.GetStatsAsync();
            Assert.NotNull(stats);
            Assert.Equal(0, stats.PendingCount);
        }

        [Fact]
        public async Task DiagnosticTest_ContractQueryWorks()
        {
            var salt = new byte[32];
            var ownerAddress = _fixture.UserAccounts[0].Address;
            var initData = _fixture.EncodeInitData(ownerAddress);

            var address = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            Assert.NotNull(address);
            Assert.False(string.IsNullOrEmpty(address));
        }

        [Fact]
        public async Task DiagnosticTest_ContractQueryAfterSetBalance()
        {
            var salt = new byte[32];
            salt[0] = 200;
            var ownerAddress = _fixture.UserAccounts[0].Address;
            var initData = _fixture.EncodeInitData(ownerAddress);

            var address = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            Assert.NotNull(address);

            await _fixture.SetBalanceAsync(address, Nethereum.Web3.Web3.Convert.ToWei(10));

            var isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(
                new IsDeployedFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            Assert.False(isDeployed);
        }

        [Fact]
        public async Task DiagnosticTest_SimpleTransactionWorks()
        {
            var recipient = _fixture.UserAccounts[1].Address;
            var transferAmount = Nethereum.Web3.Web3.Convert.ToWei(0.1m);

            var balanceBefore = await _fixture.GetBalanceAsync(recipient);

            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = _fixture.OperatorAccount.Address,
                To = recipient,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(transferAmount),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(21000),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(2000000000)
            };

            var txHash = await _fixture.Web3.Eth.TransactionManager.SendTransactionAsync(txInput);
            Assert.NotNull(txHash);

            var balanceAfter = await _fixture.GetBalanceAsync(recipient);
            Assert.True(balanceAfter > balanceBefore, "Balance should increase after transfer");
        }

        [Fact]
        public async Task DiagnosticTest_CreateSmartAccountWorks()
        {
            var salt = new byte[32];
            salt[0] = 250;
            var ownerAddress = _fixture.UserAccounts[0].Address;
            var initData = _fixture.EncodeInitData(ownerAddress);

            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };

            var receipt = await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);
            Assert.NotNull(receipt);
            Assert.True(receipt.Status.Value == 1, "Transaction should succeed");

            var accountCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(accountCode.Length > 2, $"Smart account should have code at {smartAccountAddress}. Got code length: {accountCode.Length}");
        }

        [Fact]
        public async Task DiagnosticTest_DirectRpcCallAfterSetBalance()
        {
            var salt = new byte[32];
            salt[0] = 201;
            var ownerAddress = _fixture.UserAccounts[0].Address;
            var initData = _fixture.EncodeInitData(ownerAddress);

            var getAddressFunction = new GetAddressFunction
            {
                Salt = salt,
                InitData = initData
            };

            var address = await _fixture.AccountFactoryService.GetAddressQueryAsync(getAddressFunction);
            Assert.NotNull(address);

            await _fixture.SetBalanceAsync(address, Nethereum.Web3.Web3.Convert.ToWei(10));

            var isDeployedFunction = new IsDeployedFunction
            {
                Salt = salt,
                InitData = initData
            };

            var callData = isDeployedFunction.GetCallData();
            var to = _fixture.AccountFactoryService.ContractAddress;

            var callResult = await _fixture.Node.CallAsync(
                to,
                callData,
                _fixture.OperatorAccount.Address
            );

            Assert.True(callResult.Success, $"Call failed: {callResult.RevertReason}");
        }

        [Fact]
        public async Task DiagnosticTest_HandleOpsDirectCall()
        {
            var userAccount = _fixture.UserAccounts[0];
            var recipientAccount = _fixture.UserAccounts[1];
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            var salt = new byte[32];
            salt[0] = 251;
            var initData = _fixture.EncodeInitData(userAccount.Address);
            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            var createReceipt = await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);
            Assert.True(createReceipt.Status.Value == 1, "Smart account creation should succeed");

            var accountCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(accountCode.Length > 2, $"Smart account should have code at {smartAccountAddress}. Got code length: {accountCode.Length}, code: {accountCode}");

            var actualOwner = await _fixture.ECDSAValidatorService.GetOwnerQueryAsync(smartAccountAddress);
            Assert.True(userAccount.Address.ToLowerInvariant() == actualOwner.ToLowerInvariant(),
                $"Smart account owner should be {userAccount.Address}, but got {actualOwner}");

            await _fixture.SetBalanceAsync(smartAccountAddress, Web3.Web3.Convert.ToWei(10));

            // Pre-deposit funds to EntryPoint for the smart account (avoids _payPrefund call during validation)
            var depositFunction = new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
            {
                Account = smartAccountAddress,
                AmountToSend = Web3.Web3.Convert.ToWei(5)
            };
            var depositReceipt = await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(depositFunction);
            Assert.True(depositReceipt.Status.Value == 1, "Deposit to EntryPoint should succeed");

            // Activate the smart account in the registry
            var activateReceipt = await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);
            Assert.True(activateReceipt.Status.Value == 1, "Account activation should succeed");

            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeSingle(recipientAccount.Address, transferAmount, System.Array.Empty<byte>());
            var executeFunction = new NethereumAccountExecuteFunction
            {
                Mode = mode,
                ExecutionCalldata = executionCalldata
            };

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = new Nethereum.AccountAbstraction.UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = nonce,
                InitCode = System.Array.Empty<byte>(),
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 1000000,
                VerificationGasLimit = 10000000,
                PreVerificationGas = 1000000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = Nethereum.Util.AddressUtil.ZERO_ADDRESS,
                PaymasterData = System.Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0,
                Signature = System.Array.Empty<byte>()
            };

            var signerKey = new Nethereum.Signer.EthECKey(userAccount.PrivateKey);
            var packedUserOp = Nethereum.AccountAbstraction.UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOp,
                _fixture.EntryPointService.ContractAddress,
                AppChainE2EFixture.CHAIN_ID,
                signerKey);

            // Prefix signature with validator address for modular account
            packedUserOp.Signature = ByteUtil.Merge(
                _fixture.ECDSAValidatorService.ContractAddress.HexToByteArray(),
                packedUserOp.Signature);

            var userOpHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedUserOp);

            // Check if account is active in registry before handleOps
            var isAccountActive = await _fixture.AccountRegistryService.IsActiveQueryAsync(smartAccountAddress);
            var accountStatus = await _fixture.AccountRegistryService.GetStatusQueryAsync(smartAccountAddress);

            var debugInfo = $"\nDEBUG INFO:\n" +
                $"  User Address: {userAccount.Address}\n" +
                $"  Smart Account: {smartAccountAddress}\n" +
                $"  Smart Account Owner: {actualOwner}\n" +
                $"  EntryPoint Address: {_fixture.EntryPointService.ContractAddress}\n" +
                $"  AccountRegistry Address: {_fixture.AccountRegistryService.ContractAddress}\n" +
                $"  AccountFactory Address: {_fixture.AccountFactoryService.ContractAddress}\n" +
                $"  Account Is Active: {isAccountActive}\n" +
                $"  Account Status: {accountStatus}\n" +
                $"  UserOpHash: {userOpHash.ToHex(true)}\n" +
                $"  Signature Length: {packedUserOp.Signature.Length} bytes\n";

            var handleOpsFunction = new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.HandleOpsFunction
            {
                Ops = new System.Collections.Generic.List<Nethereum.AccountAbstraction.Structs.PackedUserOperation> { packedUserOp },
                Beneficiary = _fixture.OperatorAccount.Address,
                Gas = 5000000
            };

            var callData = handleOpsFunction.GetCallData();

            // Trace the call to see what's happening inside the EVM
            var traceResult = await _fixture.Node.TraceCallAsync(
                new Nethereum.RPC.Eth.DTOs.CallInput
                {
                    From = _fixture.OperatorAccount.Address,
                    To = _fixture.EntryPointService.ContractAddress,
                    Data = callData.ToHex(true),
                    Gas = new Nethereum.Hex.HexTypes.HexBigInteger(10000000)
                });

            // Check if ecrecover was called and what it returned
            var ecrecoverCalls = traceResult.StructLogs?
                .Select((log, idx) => new { log, idx })
                .Where(x => x.log.Op == "STATICCALL" && x.idx > 0)
                .ToList();

            var traceDebug = "";
            if (ecrecoverCalls != null && ecrecoverCalls.Any())
            {
                foreach (var call in ecrecoverCalls.Take(5))
                {
                    var stack = call.log.Stack;
                    if (stack != null && stack.Count >= 6)
                    {
                        var addr = stack[stack.Count - 2];
                        traceDebug += $"\n  STATICCALL to: {addr}";
                    }
                }
            }

            var callResult = await _fixture.Node.CallAsync(
                _fixture.EntryPointService.ContractAddress,
                callData,
                _fixture.OperatorAccount.Address
            );

            var ecRecoverDebug = "";
            var callDebug = "";
            var revertDebug = "";
            var gasDebug = "";

            // Log simulation result but don't fail on it - try actual execution instead
            var simulationInfo = "";
            if (!callResult.Success)
            {
                var revertHex = callResult.ReturnData != null ? callResult.ReturnData.ToHex(true) : "null";
                simulationInfo = $"\nSimulation failed (but trying actual execution): {revertHex}";
            }

            try
            {
                // Try actual execution regardless of simulation result
                var receipt = await _fixture.EntryPointService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);
                Assert.NotNull(receipt);
                Assert.True(receipt.Status.Value == 1, $"HandleOps should succeed. Status: {receipt.Status.Value}{simulationInfo}{debugInfo}\nGas Debug:{gasDebug}\nEcRecover Debug:{ecRecoverDebug}\nCall Debug:{callDebug}\nRevert Debug:{revertDebug}");
            }
            catch (Nethereum.Contracts.SmartContractCustomErrorRevertException scex)
            {
                Assert.Fail($"HandleOps reverted with custom error: {scex.Message}{simulationInfo}{debugInfo}\nGas Debug:{gasDebug}\nEcRecover Debug:{ecRecoverDebug}\nCall Debug:{callDebug}\nRevert Debug:{revertDebug}");
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"HandleOps transaction failed: {ex.GetType().Name}: {ex.Message}{simulationInfo}{debugInfo}\nGas Debug:{gasDebug}\nEcRecover Debug:{ecRecoverDebug}\nCall Debug:{callDebug}\nRevert Debug:{revertDebug}");
            }
        }

        private static byte[] PackAccountGasLimits(BigInteger verificationGasLimit, BigInteger callGasLimit)
        {
            var result = new byte[32];
            var verificationBytes = verificationGasLimit.ToByteArray(isUnsigned: true, isBigEndian: true);
            var callBytes = callGasLimit.ToByteArray(isUnsigned: true, isBigEndian: true);

            System.Array.Copy(verificationBytes, 0, result, 16 - verificationBytes.Length, verificationBytes.Length);
            System.Array.Copy(callBytes, 0, result, 32 - callBytes.Length, callBytes.Length);

            return result;
        }

        private static byte[] PackGasFees(BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            var result = new byte[32];
            var priorityBytes = maxPriorityFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);
            var maxBytes = maxFeePerGas.ToByteArray(isUnsigned: true, isBigEndian: true);

            System.Array.Copy(priorityBytes, 0, result, 16 - priorityBytes.Length, priorityBytes.Length);
            System.Array.Copy(maxBytes, 0, result, 32 - maxBytes.Length, maxBytes.Length);

            return result;
        }
    }
}
