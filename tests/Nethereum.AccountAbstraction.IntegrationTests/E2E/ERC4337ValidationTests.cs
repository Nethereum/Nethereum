using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "ERC4337-Validation")]
    public class ERC4337ValidationTests
    {
        private readonly DevChainBundlerFixture _fixture;

        private const string DUMMY_SIGNATURE =
            "0xfffffffffffffffffffffffffffffff0000000000000000000000000000000007aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1c";

        public ERC4337ValidationTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        #region AA11 - Sender Already Constructed

        [Fact]
        [Trait("ErrorCode", "AA10")]
        public async Task Given_AccountAlreadyDeployed_When_InitCodeProvided_Then_RevertsWithAA10()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 11001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var deployInitCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = deployInitCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var deployFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(deployFunction);

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Precondition: Account must be deployed");

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                InitCode = deployInitCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // Per ERC-4337: "If initCode is not empty, verify the sender doesn't already have code deployed"
            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.True(
                ex.Message.Contains("AA10") || ex.Message.Contains("AA11") || ex.Message.Contains("already"),
                $"Expected AA10/AA11 error but got: {ex.Message}");
        }

        #endregion

        #region AA13 - InitCode Failed or OOG

        [Fact]
        [Trait("ErrorCode", "AA13")]
        public async Task Given_InitCodeOOG_When_VerificationGasInsufficient_Then_RevertsWithAA13()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 13001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 3m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 100,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with insufficient verification gas should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA13") ||
                    ex.Message.Contains("initCode") ||
                    ex.Message.Contains("OOG") ||
                    ex.Message.Contains("gas"),
                    $"Expected AA13/initCode/gas error but got: {ex.Message}");
            }
        }

        [Fact]
        [Trait("ErrorCode", "AA13")]
        public async Task Given_FactoryNotDeployed_When_InitCodeExecuted_Then_RevertsWithAA13()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 13002;

            var nonExistentFactory = "0x0000000000000000000000000000000000dead01".HexToByteArray();

            var createAccountCallDataHex = _fixture.AccountFactoryService.ContractHandler
                .GetFunction<Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition.CreateAccountFunction>()
                .GetData(new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition.CreateAccountFunction
                {
                    Owner = ownerAddress,
                    Salt = salt
                });
            var createAccountCallData = createAccountCallDataHex.HexToByteArray();

            var initCode = new byte[20 + createAccountCallData.Length];
            Array.Copy(nonExistentFactory, 0, initCode, 0, 20);
            Array.Copy(createAccountCallData, 0, initCode, 20, createAccountCallData.Length);

            var accountAddress = "0x1111111111111111111111111111111111111111";
            await _fixture.FundAccountAsync(accountAddress, 3m);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.True(
                ex.Message.Contains("AA13") ||
                ex.Message.Contains("initCode") ||
                ex.Message.Contains("factory"),
                $"Expected AA13/initCode/factory error but got: {ex.Message}");
        }

        #endregion

        #region AA21 - Insufficient Funds / Didn't Pay Prefund

        [Fact]
        [Trait("ErrorCode", "AA21")]
        public async Task Given_InsufficientAccountBalance_When_NoPaymaster_Then_RevertsWithAA21()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 21001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            await _fixture.FundAccountAsync(accountAddress, 0.00001m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with insufficient funds should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA21") ||
                    ex.Message.Contains("prefund") ||
                    ex.Message.Contains("balance") ||
                    ex.Message.Contains("insufficient"),
                    $"Expected AA21/prefund/balance error but got: {ex.Message}");
            }
        }

        #endregion

        #region AA24 - Invalid Signature

        [Fact]
        [Trait("ErrorCode", "AA24")]
        public async Task Given_EmptySignature_When_Validated_Then_RevertsWithAA24()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 24001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = UserOperationBuilder.PackUserOperation(userOp);
            packedOp.Signature = new byte[65];

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true, "Operation with empty signature should fail");
        }

        #endregion

        #region AA25 - Invalid Nonce

        [Fact]
        [Trait("ErrorCode", "AA25")]
        public async Task Given_NonceReused_When_Submitted_Then_RevertsWithAA25()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 25001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var firstOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedFirstOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(firstOp, accountKey);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedFirstOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal((BigInteger)2, currentNonce);

            var reusedNonceOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedReusedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(reusedNonceOp, accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedReusedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.Contains("AA25", ex.Message);
        }

        #endregion

        #region Mempool - Duplicate Rejection

        [Fact]
        [Trait("Category", "ERC4337-Mempool")]
        [Trait("Feature", "DuplicateRejection")]
        public async Task Given_OperationInMempool_When_DuplicateSubmitted_Then_Rejected()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 70001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Equal(BundlerErrorCodes.InvalidFields, ex.Code);
            Assert.True(
                ex.Message.ToLower().Contains("duplicate") ||
                ex.Message.ToLower().Contains("already") ||
                ex.Message.ToLower().Contains("exists"),
                $"Expected duplicate rejection but got: {ex.Message}");
        }

        #endregion

        #region 2D Nonce - Independent Keys

        [Fact]
        [Trait("Category", "ERC4337-Nonce")]
        [Trait("Feature", "2DNonce")]
        public async Task Given_DifferentNonceKeys_When_Submitted_Then_ExecuteIndependently()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 80001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            BigInteger nonceKey1 = 1;
            BigInteger nonceKey2 = 2;

            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
            var nonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

            var sequence1 = nonce1 & ((BigInteger.One << 64) - 1);
            var sequence2 = nonce2 & ((BigInteger.One << 64) - 1);

            Assert.Equal(BigInteger.Zero, sequence1);
            Assert.Equal(BigInteger.Zero, sequence2);

            var fullNonce1 = nonce1;
            var fullNonce2 = nonce2;

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                Nonce = fullNonce1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var userOp2 = new UserOperation
            {
                Sender = accountAddress,
                Nonce = fullNonce2,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, accountKey);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp1, packedOp2 },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 10000000
            };

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var newNonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
            var newNonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

            var newSequence1 = newNonce1 & ((BigInteger.One << 64) - 1);
            var newSequence2 = newNonce2 & ((BigInteger.One << 64) - 1);

            Assert.Equal(BigInteger.One, newSequence1);
            Assert.Equal(BigInteger.One, newSequence2);
        }

        #endregion

        #region Execution Failure - CallData Reverts

        [Fact]
        [Trait("Category", "ERC4337-Execution")]
        [Trait("Feature", "ExecutionRevert")]
        public async Task Given_CallDataReverts_When_Executed_Then_EmitsRevertEventButBundleSucceeds()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 50001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var executeFunction = new ExecuteFunction
            {
                Dest = "0x0000000000000000000000000000000000000000",
                Value = BigInteger.Parse("1000000000000000000000"),
                Data = Array.Empty<byte>()
            };
            var functionBuilder = new Nethereum.Contracts.FunctionBuilder<ExecuteFunction>(accountAddress);
            var executeCallData = functionBuilder.GetDataAsBytes(executeFunction);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = executeCallData,
                CallGasLimit = 100000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(result?.Receipt);
            Assert.Equal((BigInteger)1, result.Receipt.Status.Value);

            var revertEvents = result.Receipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
            var hasRevertEvent = revertEvents.Count > 0;

            var hasFailedUserOp = result.UserOpResults?.Any(r => !r.Success) ?? false;

            Assert.True(hasRevertEvent || hasFailedUserOp,
                "Expected UserOp to fail execution (revert event or failed result)");
        }

        #endregion

        #region AA41 - Account Validation OOG

        [Fact]
        [Trait("ErrorCode", "AA41")]
        public async Task Given_InsufficientVerificationGas_When_AccountValidates_Then_RevertsWithAA41()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 41001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 1000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                    packedOp, _fixture.EntryPointService.ContractAddress);
                await _fixture.BundlerService.ExecuteBundleAsync();

                var status = await _fixture.BundlerService.GetUserOperationStatusAsync(userOpHash);
                Assert.Equal(UserOpState.Failed, status.State);
                Assert.True(
                    status.Error != null &&
                    (status.Error.Contains("AA41") ||
                     status.Error.Contains("AA26") ||
                     status.Error.Contains("verification") ||
                     status.Error.Contains("gas") ||
                     status.Error.Contains("reverted")),
                    $"Expected a verification gas failure but got: {status.Error}");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA41") ||
                    ex.Message.Contains("verification") ||
                    ex.Message.Contains("gas"),
                    $"Expected AA41/verification gas error but got: {ex.Message}");
            }
        }

        #endregion

        #region AA51 - Paymaster Deposit Too Low

        [Fact]
        [Trait("ErrorCode", "AA51")]
        public async Task Given_PaymasterNoDeposit_When_SponsoringOp_Then_RevertsWithAA51()
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);


            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 51001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = Array.Empty<byte>()
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with unfunded paymaster should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.Equal(BundlerErrorCodes.PaymasterDepositTooLow, ex.Code);
                Assert.True(
                    ex.Message.Contains("AA51") ||
                    ex.Message.Contains("paymaster") ||
                    ex.Message.Contains("deposit") ||
                    ex.Message.Contains("balance"),
                    $"Expected AA51/paymaster deposit error but got: {ex.Message}");
            }
        }

        [Fact]
        [Trait("ErrorCode", "AA51")]
        public async Task Given_PaymasterMinimalDeposit_When_SponsoringExpensiveOp_Then_RevertsWithAA51()
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = 1
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 51002;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100000,
                VerificationGasLimit = 300000,
                PreVerificationGas = 100000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = Array.Empty<byte>()
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with minimally funded paymaster should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.Equal(BundlerErrorCodes.PaymasterDepositTooLow, ex.Code);
                Assert.True(
                    ex.Message.Contains("AA51") ||
                    ex.Message.Contains("paymaster") ||
                    ex.Message.Contains("deposit") ||
                    ex.Message.Contains("balance"),
                    $"Expected AA51/paymaster deposit error but got: {ex.Message}");
            }
        }

        #endregion

        #region Paymaster Success Scenarios

        [Fact]
        [Trait("Category", "ERC4337-Paymaster")]
        [Trait("Feature", "PaymasterSponsorship")]
        public async Task Given_FundedPaymaster_When_SponsoringOp_Then_Succeeds()
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await paymasterService.AddStakeRequestAndWaitForReceiptAsync(
                new TestPaymasterAcceptAll.ContractDefinition.AddStakeFunction
                {
                    UnstakeDelaySec = 86400,
                    AmountToSend = Web3.Web3.Convert.ToWei(0.1m)
                });

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(5m)
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 52001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = Array.Empty<byte>()
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Paymaster-sponsored operation should succeed: {result.Error}");

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Account should be deployed");
        }

        #endregion

        #region AA42 - Insufficient Call Gas (Execution OOG)

        [Fact]
        [Trait("ErrorCode", "AA42")]
        public async Task Given_VeryLowCallGas_When_Executing_Then_RevertsWithAA42OrFails()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 42001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var recipient = "0x" + new string('9', 40);
            var executeFunction = new ExecuteFunction
            {
                Dest = recipient,
                Value = Web3.Web3.Convert.ToWei(0.001m),
                Data = Array.Empty<byte>()
            };
            var functionBuilder = new Nethereum.Contracts.FunctionBuilder<ExecuteFunction>(accountAddress);
            var executeCallData = functionBuilder.GetDataAsBytes(executeFunction);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = executeCallData,
                CallGasLimit = 100,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                if (result != null)
                {
                    var revertEvents = result.Receipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
                    var hasRevertEvent = revertEvents.Count > 0;
                    var hasFailedUserOp = result.UserOpResults?.Any(r => !r.Success) ?? false;

                    Assert.True(hasRevertEvent || hasFailedUserOp || !result.Success,
                        "Execution should fail due to insufficient call gas");
                }
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA42") ||
                    ex.Message.ToLower().Contains("call") ||
                    ex.Message.ToLower().Contains("gas") ||
                    ex.Message.ToLower().Contains("execution"),
                    $"Expected AA42/call gas error but got: {ex.Message}");
            }
        }

        #endregion

        #region AA52 - Paymaster Validation Failed

        [Fact]
        [Trait("ErrorCode", "AA52")]
        public async Task Given_VerifyingPaymasterWithInvalidSignature_When_Validating_Then_RevertsWithAA52()
        {
            var signerKey = EthECKey.GenerateKey();
            var signerAddress = signerKey.GetPublicAddress();

            var paymasterDeployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(5m)
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 52101;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var invalidPaymasterData = new byte[77];

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = invalidPaymasterData
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true,
                    "Operation with invalid paymaster signature should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA52") ||
                    ex.Message.Contains("AA34") ||
                    ex.Message.ToLower().Contains("paymaster") ||
                    ex.Message.ToLower().Contains("signature") ||
                    ex.Message.ToLower().Contains("validation"),
                    $"Expected AA52/paymaster validation error but got: {ex.Message}");
            }
        }

        #endregion

        #region Batch Processing

        [Fact]
        [Trait("Category", "ERC4337-Batch")]
        [Trait("Feature", "BatchExecution")]
        public async Task Given_MultipleValidOps_When_ProcessedInSameBatch_Then_AllSucceed()
        {
            var accountKey1 = EthECKey.GenerateKey();
            var ownerAddress1 = accountKey1.GetPublicAddress();
            ulong salt1 = 80101;

            var accountKey2 = EthECKey.GenerateKey();
            var ownerAddress2 = accountKey2.GetPublicAddress();
            ulong salt2 = 80102;

            var accountAddress1 = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress1, salt1);
            await _fixture.FundAccountAsync(accountAddress1, 5m);

            var accountAddress2 = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress2, salt2);
            await _fixture.FundAccountAsync(accountAddress2, 5m);

            var initCode1 = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress1, salt1);
            var initCode2 = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress2, salt2);

            var deployOp1 = new UserOperation
            {
                Sender = accountAddress1,
                Nonce = 0,
                InitCode = initCode1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var deployOp2 = new UserOperation
            {
                Sender = accountAddress2,
                Nonce = 0,
                InitCode = initCode2,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp1, accountKey1);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp2, accountKey2);

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp1, packedOp2 },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 10000000
            };

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var code1 = await _fixture.GetCodeAsync(accountAddress1);
            var code2 = await _fixture.GetCodeAsync(accountAddress2);

            Assert.True(code1.Length > 0, "Account 1 should be deployed");
            Assert.True(code2.Length > 0, "Account 2 should be deployed");

            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress1, 0);
            var nonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress2, 0);

            Assert.Equal((BigInteger)1, nonce1);
            Assert.Equal((BigInteger)1, nonce2);
        }

        #endregion

        #region AA32 - Paymaster Signature Expired (validUntil in past)

        [Fact]
        [Trait("ErrorCode", "AA32")]
        public async Task Given_PaymasterWithExpiredSignature_When_Validating_Then_RevertsWithAA32()
        {
            var signerKey = EthECKey.GenerateKey();
            var signerAddress = signerKey.GetPublicAddress();

            var paymasterDeployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(5m)
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 32001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiredTimestamp = now - 3600;
            var validAfter = (ulong)0;

            var paymasterData = new byte[77];
            var validUntilBytes = BitConverter.GetBytes(expiredTimestamp);
            if (BitConverter.IsLittleEndian) Array.Reverse(validUntilBytes);
            Array.Copy(validUntilBytes, 2, paymasterData, 0, 6);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = paymasterData
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true,
                    "Operation with expired paymaster timestamp should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA32") ||
                    ex.Message.ToLower().Contains("expired") ||
                    ex.Message.ToLower().Contains("paymaster") ||
                    ex.Message.ToLower().Contains("timestamp") ||
                    ex.Message.ToLower().Contains("validation"),
                    $"Expected AA32/expired error but got: {ex.Message}");
            }
        }

        #endregion

        #region AA33 - Paymaster Not Yet Valid (validAfter in future)

        [Fact]
        [Trait("ErrorCode", "AA33")]
        public async Task Given_PaymasterWithFutureValidAfter_When_Validating_Then_RevertsWithAA33()
        {
            var signerKey = EthECKey.GenerateKey();
            var signerAddress = signerKey.GetPublicAddress();

            var paymasterDeployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(5m)
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 33001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validUntil = now + 7200;
            var futureValidAfter = now + 3600;

            var paymasterData = new byte[77];
            var validUntilBytes = BitConverter.GetBytes(validUntil);
            if (BitConverter.IsLittleEndian) Array.Reverse(validUntilBytes);
            Array.Copy(validUntilBytes, 2, paymasterData, 0, 6);
            var validAfterBytes = BitConverter.GetBytes(futureValidAfter);
            if (BitConverter.IsLittleEndian) Array.Reverse(validAfterBytes);
            Array.Copy(validAfterBytes, 2, paymasterData, 6, 6);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = paymasterData
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true,
                    "Operation with future validAfter should fail");
            }
            catch (BundlerRpcException ex)
            {
                Assert.True(
                    ex.Message.Contains("AA33") ||
                    ex.Message.ToLower().Contains("not yet valid") ||
                    ex.Message.ToLower().Contains("paymaster") ||
                    ex.Message.ToLower().Contains("timestamp") ||
                    ex.Message.ToLower().Contains("validation"),
                    $"Expected AA33/not-yet-valid error but got: {ex.Message}");
            }
        }

        #endregion

        #region Gas Estimation

        [Fact]
        [Trait("Category", "ERC4337-GasEstimation")]
        [Trait("Feature", "EstimateGas")]
        public async Task Given_ValidUserOp_When_EstimatingGas_Then_ReturnsReasonableValues()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 90001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(estimate);
            Assert.True(estimate.VerificationGasLimit.Value > 0, "VerificationGasLimit should be > 0");
            Assert.True(estimate.CallGasLimit.Value > 0, "CallGasLimit should be > 0");
            Assert.True(estimate.PreVerificationGas.Value > 0, "PreVerificationGas should be > 0");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.True(result?.Success ?? false, $"Operation with estimated gas should succeed: {result?.Error}");
        }

        #endregion

        #region Edge Cases

        [Fact]
        [Trait("Category", "ERC4337-EdgeCase")]
        [Trait("Feature", "ZeroCallData")]
        public async Task Given_ZeroCallData_When_Executed_Then_Succeeds()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 95001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Zero callData operation should succeed: {result.Error}");

            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal((BigInteger)2, finalNonce);
        }

        [Fact]
        [Trait("Category", "ERC4337-EdgeCase")]
        [Trait("Feature", "MaxGasValues")]
        public async Task Given_HighGasLimits_When_Executed_Then_Succeeds()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 95002;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(deployOp, accountKey);
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            });

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 1000000,
                VerificationGasLimit = 1000000,
                PreVerificationGas = 100000,
                MaxFeePerGas = 5000000000,
                MaxPriorityFeePerGas = 2000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"High gas limit operation should succeed: {result.Error}");
        }

        #endregion
    }
}
