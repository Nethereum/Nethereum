using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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

        public ERC4337ValidationTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        #region AA11 - Sender Already Constructed

        [Fact]
        [Trait("ErrorCode", "AA10")]
        public async Task Given_AccountAlreadyDeployed_When_InitCodeProvided_Then_RevertsWithAA10()
        {
            // GIVEN: An account that is already deployed on-chain
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 11001;

            // First, deploy the account
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

            // Verify account is deployed
            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Precondition: Account must be deployed");

            // WHEN: Submitting a UserOp with initCode for the already-deployed account
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,  // Next nonce after deployment
                InitCode = deployInitCode,  // Should NOT be provided for deployed account
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // THEN: Bundler rejects with AA10 (sender already constructed)
            // Per ERC-4337: "If initCode is not empty, verify the sender doesn't already have code deployed"
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            // AA10 is "sender already constructed"
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
            // GIVEN: A UserOp with insufficient verificationGasLimit for initCode execution
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
                VerificationGasLimit = 100,  // Way too low for contract deployment
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // WHEN: Submitting the UserOp
            // Note: In unsafe mode, bundler may accept the op but execution will fail
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                // THEN: Either submission fails with AA13 or execution fails
                Assert.False(result?.Success ?? true, "Operation with insufficient verification gas should fail");
            }
            catch (InvalidOperationException ex)
            {
                // Expected if bundler validates gas limits
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
            // GIVEN: InitCode pointing to a non-existent factory address
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 13002;

            // Non-existent factory address
            var nonExistentFactory = "0x0000000000000000000000000000000000dead01".HexToByteArray();

            // Build initCode with non-existent factory
            // Format: factory address (20 bytes) + factory calldata
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

            // Use a deterministic address for sender
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

            // WHEN: Submitting with non-existent factory
            // THEN: Should fail with AA13 (initCode failed - no code at factory)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

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
            // GIVEN: An account with balance too low to pay for gas
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 21001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            // Fund with only a tiny amount - not enough to cover required prefund
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

            // WHEN: Submitting with insufficient funds
            // Note: In unsafe mode, bundler may accept the op but execution will fail
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                // THEN: Either submission fails with AA21 or execution fails
                Assert.False(result?.Success ?? true, "Operation with insufficient funds should fail");
            }
            catch (InvalidOperationException ex)
            {
                // Expected if bundler validates prefund
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
            // GIVEN: A deployed account with an empty signature
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 24001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy the account first
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

            // WHEN: Submitting a UserOp with empty/zeroed signature
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

            // Pack but don't sign properly - use empty signature
            var packedOp = UserOperationBuilder.PackUserOperation(userOp);
            packedOp.Signature = new byte[65]; // Empty 65-byte signature

            // THEN: Should fail with AA24 (signature error)
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
            // GIVEN: An account that has already used nonce 1
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 25001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            // Deploy account (nonce 0)
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

            // Execute nonce 1
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

            // Verify nonce is now 2
            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal((BigInteger)2, currentNonce);

            // WHEN: Trying to reuse nonce 1
            var reusedNonceOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,  // Already used!
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedReusedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(reusedNonceOp, accountKey);

            // THEN: Should fail with AA25 (invalid nonce)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedReusedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Contains("AA25", ex.Message);
        }

        #endregion

        #region Mempool - Duplicate Rejection

        [Fact]
        [Trait("Category", "ERC4337-Mempool")]
        [Trait("Feature", "DuplicateRejection")]
        public async Task Given_OperationInMempool_When_DuplicateSubmitted_Then_Rejected()
        {
            // GIVEN: An operation already in the mempool
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

            // First submission succeeds
            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            // WHEN: Submitting the exact same operation again
            // THEN: Should be rejected as duplicate
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

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
            // GIVEN: A deployed account
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 80001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            // Deploy account first
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            var deployOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,  // Key 0, sequence 0
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

            // Use two different nonce keys
            BigInteger nonceKey1 = 1;
            BigInteger nonceKey2 = 2;

            // GetNonceQueryAsync returns the FULL nonce (key << 64 | sequence)
            // For a fresh key, sequence should be 0, so full nonce = key << 64
            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
            var nonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

            // Extract sequence from full nonce (lower 64 bits)
            var sequence1 = nonce1 & ((BigInteger.One << 64) - 1);
            var sequence2 = nonce2 & ((BigInteger.One << 64) - 1);

            Assert.Equal(BigInteger.Zero, sequence1);
            Assert.Equal(BigInteger.Zero, sequence2);

            // Use the full nonces returned by EntryPoint (already includes key)
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

            // WHEN: Executing both operations in same bundle
            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp1, packedOp2 },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 10000000
            };

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            // THEN: Both succeed, and both nonce keys are incremented
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var newNonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
            var newNonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

            // Extract sequences from full nonces
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
            // GIVEN: A deployed account with callData that will revert
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 50001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // CallData that calls execute with value > account balance (will fail)
            // SimpleAccount.execute(address dest, uint256 value, bytes calldata data)
            // Build the calldata using the function message
            var executeFunction = new ExecuteFunction
            {
                Dest = "0x0000000000000000000000000000000000000000",  // Zero address
                Value = BigInteger.Parse("1000000000000000000000"),  // 1000 ETH - more than we have
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

            // WHEN: Executing the operation via bundler
            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            // THEN: Bundle transaction succeeds (status=1), and either:
            // - UserOp emits UserOperationRevertReason event, OR
            // - UserOp has success=false in UserOpResults
            Assert.NotNull(result?.Receipt);
            Assert.Equal((BigInteger)1, result.Receipt.Status.Value);

            // Check for UserOperationRevertReason event OR check UserOpResults
            var revertEvents = result.Receipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
            var hasRevertEvent = revertEvents.Count > 0;

            // Also check UserOpResults if available
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
            // GIVEN: A deployed account with a UserOp that has too little verificationGasLimit
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 41001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // WHEN: Submitting a UserOp with verification gas too low for signature validation
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 1000,  // Too low for ECDSA recovery
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // THEN: Should fail with AA41 or verification gas error
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with insufficient verification gas should fail");
            }
            catch (InvalidOperationException ex)
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
            // GIVEN: A paymaster with zero deposit (not enough to cover gas)
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            // Don't deposit anything - paymaster has zero balance

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 51001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // WHEN: Submitting a UserOp using the unfunded paymaster
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

            // THEN: Should fail with AA51 (paymaster deposit too low)
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with unfunded paymaster should fail");
            }
            catch (InvalidOperationException ex)
            {
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
            // GIVEN: A paymaster with only 1 wei deposit (not enough for expensive operation)
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            // Deposit tiny amount (1 wei)
            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterService.ContractHandler.ContractAddress,
                    AmountToSend = 1  // 1 wei - not enough for any operation
                });

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 51002;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // WHEN: Submitting an expensive UserOp using the minimally funded paymaster
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100000,  // Higher gas
                VerificationGasLimit = 300000,  // Higher gas
                PreVerificationGas = 100000,  // Higher gas
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000,
                PaymasterData = Array.Empty<byte>()
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // THEN: Should fail with AA51 (paymaster deposit too low)
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true, "Operation with minimally funded paymaster should fail");
            }
            catch (InvalidOperationException ex)
            {
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
            // GIVEN: A paymaster with sufficient deposit and stake
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            // Add stake and deposit
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
            // Note: Account doesn't need funding when paymaster sponsors!

            // Deploy account using paymaster
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

            // WHEN: Submitting the paymaster-sponsored operation
            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            // THEN: Operation succeeds
            Assert.NotNull(result);
            Assert.True(result.Success, $"Paymaster-sponsored operation should succeed: {result.Error}");

            // Verify account was created
            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Account should be deployed");
        }

        #endregion

        #region AA42 - Insufficient Call Gas (Execution OOG)

        [Fact]
        [Trait("ErrorCode", "AA42")]
        public async Task Given_VeryLowCallGas_When_Executing_Then_RevertsWithAA42OrFails()
        {
            // GIVEN: A deployed account with a UserOp that has very low callGasLimit
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 42001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // Create a call that requires gas (transfer ETH)
            var recipient = "0x" + new string('9', 40);
            var executeFunction = new ExecuteFunction
            {
                Dest = recipient,
                Value = Web3.Web3.Convert.ToWei(0.001m),
                Data = Array.Empty<byte>()
            };
            var functionBuilder = new Nethereum.Contracts.FunctionBuilder<ExecuteFunction>(accountAddress);
            var executeCallData = functionBuilder.GetDataAsBytes(executeFunction);

            // WHEN: Submitting a UserOp with callGasLimit too low for execution
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = executeCallData,
                CallGasLimit = 100,  // Way too low for any meaningful execution
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // THEN: Should fail with AA42 or execution failure
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                // If it gets past validation, execution should fail
                if (result != null)
                {
                    // Check for revert event or failed result
                    var revertEvents = result.Receipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
                    var hasRevertEvent = revertEvents.Count > 0;
                    var hasFailedUserOp = result.UserOpResults?.Any(r => !r.Success) ?? false;

                    Assert.True(hasRevertEvent || hasFailedUserOp || !result.Success,
                        "Execution should fail due to insufficient call gas");
                }
            }
            catch (InvalidOperationException ex)
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
            // GIVEN: A VerifyingPaymaster that requires valid signatures
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

            // Fund the paymaster via EntryPoint deposit
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

            // Deploy account first
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

            // WHEN: Submitting a UserOp with paymaster but INVALID signature
            // The VerifyingPaymaster expects: validUntil (6 bytes) + validAfter (6 bytes) + signature (65 bytes)
            var invalidPaymasterData = new byte[77];  // 6 + 6 + 65 = 77 bytes, all zeros = invalid signature

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
                PaymasterData = invalidPaymasterData  // Invalid signature!
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            // THEN: Should fail with AA52 or AA34 (paymaster signature validation failed)
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                // If bundler is in unsafe mode, execution should fail
                Assert.False(result?.Success ?? true,
                    "Operation with invalid paymaster signature should fail");
            }
            catch (InvalidOperationException ex)
            {
                // Valid rejection for paymaster signature failure
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
            // GIVEN: Two different accounts with valid operations
            var accountKey1 = EthECKey.GenerateKey();
            var ownerAddress1 = accountKey1.GetPublicAddress();
            ulong salt1 = 80101;

            var accountKey2 = EthECKey.GenerateKey();
            var ownerAddress2 = accountKey2.GetPublicAddress();
            ulong salt2 = 80102;

            // Setup first account
            var accountAddress1 = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress1, salt1);
            await _fixture.FundAccountAsync(accountAddress1, 5m);

            // Setup second account
            var accountAddress2 = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress2, salt2);
            await _fixture.FundAccountAsync(accountAddress2, 5m);

            // Create deploy operations for both accounts
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

            // WHEN: Executing both operations in a single handleOps call
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

            // THEN: Both accounts should be deployed
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var code1 = await _fixture.GetCodeAsync(accountAddress1);
            var code2 = await _fixture.GetCodeAsync(accountAddress2);

            Assert.True(code1.Length > 0, "Account 1 should be deployed");
            Assert.True(code2.Length > 0, "Account 2 should be deployed");

            // Verify nonces are incremented
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
            // GIVEN: A VerifyingPaymaster with validUntil timestamp in the past
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

            // Fund the paymaster
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

            // Deploy account first
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

            // WHEN: Creating paymaster data with expired validUntil (timestamp in the past)
            // PaymasterData format: validUntil (6 bytes) + validAfter (6 bytes) + signature (65 bytes)
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiredTimestamp = now - 3600;  // 1 hour in the past
            var validAfter = (ulong)0;

            // Build paymaster data with expired timestamp
            var paymasterData = new byte[77];  // 6 + 6 + 65
            // validUntil (6 bytes, big-endian)
            var validUntilBytes = BitConverter.GetBytes(expiredTimestamp);
            if (BitConverter.IsLittleEndian) Array.Reverse(validUntilBytes);
            Array.Copy(validUntilBytes, 2, paymasterData, 0, 6);  // Take last 6 bytes
            // validAfter (6 bytes) - leave as zeros
            // signature (65 bytes) - leave as zeros (invalid but we're testing timestamp)

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

            // THEN: Should fail with AA32 (paymaster expired) or timestamp validation error
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true,
                    "Operation with expired paymaster timestamp should fail");
            }
            catch (InvalidOperationException ex)
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
            // GIVEN: A VerifyingPaymaster with validAfter timestamp in the future
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

            // Fund the paymaster
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

            // Deploy account first
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

            // WHEN: Creating paymaster data with validAfter in the future
            // PaymasterData format: validUntil (6 bytes) + validAfter (6 bytes) + signature (65 bytes)
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validUntil = now + 7200;  // 2 hours from now
            var futureValidAfter = now + 3600;  // 1 hour from now (not yet valid!)

            // Build paymaster data with future validAfter
            var paymasterData = new byte[77];  // 6 + 6 + 65
            // validUntil (6 bytes, big-endian)
            var validUntilBytes = BitConverter.GetBytes(validUntil);
            if (BitConverter.IsLittleEndian) Array.Reverse(validUntilBytes);
            Array.Copy(validUntilBytes, 2, paymasterData, 0, 6);
            // validAfter (6 bytes, big-endian)
            var validAfterBytes = BitConverter.GetBytes(futureValidAfter);
            if (BitConverter.IsLittleEndian) Array.Reverse(validAfterBytes);
            Array.Copy(validAfterBytes, 2, paymasterData, 6, 6);
            // signature (65 bytes) - leave as zeros

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

            // THEN: Should fail with AA33 (paymaster not yet valid) or timestamp validation error
            try
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await _fixture.BundlerService.ExecuteBundleAsync();

                Assert.False(result?.Success ?? true,
                    "Operation with future validAfter should fail");
            }
            catch (InvalidOperationException ex)
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
            // GIVEN: A deployed account
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 90001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account first
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

            // WHEN: Estimating gas for a simple operation
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            // THEN: Gas estimates should be reasonable
            Assert.NotNull(estimate);
            Assert.True(estimate.VerificationGasLimit.Value > 0, "VerificationGasLimit should be > 0");
            Assert.True(estimate.CallGasLimit.Value > 0, "CallGasLimit should be > 0");
            Assert.True(estimate.PreVerificationGas.Value > 0, "PreVerificationGas should be > 0");

            // Verify the operation can be executed with estimated gas
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
            // GIVEN: A deployed account with empty callData (no-op)
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 95001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            // Deploy account
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

            // WHEN: Submitting an operation with zero callData
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),  // Empty = no-op
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            // THEN: Operation should succeed (no-op is valid)
            Assert.NotNull(result);
            Assert.True(result.Success, $"Zero callData operation should succeed: {result.Error}");

            // Verify nonce incremented
            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal((BigInteger)2, finalNonce);
        }

        [Fact]
        [Trait("Category", "ERC4337-EdgeCase")]
        [Trait("Feature", "MaxGasValues")]
        public async Task Given_HighGasLimits_When_Executed_Then_Succeeds()
        {
            // GIVEN: A deployed account with high but valid gas limits
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 95002;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 10m);

            // Deploy account
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

            // WHEN: Submitting with high gas limits (but still valid)
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 1000000,  // 1M gas
                VerificationGasLimit = 1000000,  // 1M gas
                PreVerificationGas = 100000,
                MaxFeePerGas = 5000000000,  // 5 Gwei
                MaxPriorityFeePerGas = 2000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            // THEN: Operation should succeed with high gas limits
            Assert.NotNull(result);
            Assert.True(result.Success, $"High gas limit operation should succeed: {result.Error}");
        }

        #endregion
    }
}
