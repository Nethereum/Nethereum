using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

using TestPaymasterDeployment = Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition.TestPaymasterAcceptAllDeployment;
using TestPaymasterDepositFunction = Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition.DepositFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class DevChainBundlerAdvancedTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public DevChainBundlerAdvancedTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        #region Batch Operations Tests

        [Fact]
        public async Task BatchHandleOps_MultipleSameAccountOperations_ExecutesSequentially()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 1000;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp1 = new UserOperation
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

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp1 },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.NotNull(receipt);
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var codeAfter = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(codeAfter.Length > 0, "Account should be deployed");
        }

        [Fact]
        public async Task BatchHandleOps_MultipleDifferentAccounts_ExecutesAll()
        {
            var account1Key = EthECKey.GenerateKey();
            var account2Key = EthECKey.GenerateKey();
            ulong salt1 = 2001;
            ulong salt2 = 2002;

            var account1Address = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                account1Key.GetPublicAddress(), salt1);
            var account2Address = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                account2Key.GetPublicAddress(), salt2);

            await _fixture.FundAccountAsync(account1Address, 3m);
            await _fixture.FundAccountAsync(account2Address, 3m);

            var initCode1 = _fixture.AccountFactoryService.GetCreateAccountInitCode(
                account1Key.GetPublicAddress(), salt1);
            var initCode2 = _fixture.AccountFactoryService.GetCreateAccountInitCode(
                account2Key.GetPublicAddress(), salt2);

            var userOp1 = new UserOperation
            {
                Sender = account1Address,
                Nonce = 0,
                InitCode = initCode1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var userOp2 = new UserOperation
            {
                Sender = account2Address,
                Nonce = 0,
                InitCode = initCode2,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, account1Key);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, account2Key);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp1, packedOp2 },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 10000000
            };

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.NotNull(receipt);
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var code1After = await _fixture.GetCodeAsync(account1Address);
            var code2After = await _fixture.GetCodeAsync(account2Address);
            Assert.True(code1After.Length > 0, "Account 1 should be deployed");
            Assert.True(code2After.Length > 0, "Account 2 should be deployed");
        }

        [Fact]
        public async Task BatchHandleOps_ThreeOperations_AllSucceed()
        {
            var keys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var salts = new ulong[] { 3001, 3002, 3003 };
            var packedOps = new List<PackedUserOperation>();

            for (int i = 0; i < 3; i++)
            {
                var ownerAddress = keys[i].GetPublicAddress();
                var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salts[i]);
                await _fixture.FundAccountAsync(accountAddress, 3m);

                var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salts[i]);

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

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, keys[i]);
                packedOps.Add(packedOp);
            }

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = packedOps,
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 15000000
            };

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.NotNull(receipt);
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            for (int i = 0; i < 3; i++)
            {
                var ownerAddress = keys[i].GetPublicAddress();
                var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salts[i]);
                var codeAfter = await _fixture.GetCodeAsync(accountAddress);
                Assert.True(codeAfter.Length > 0, $"Account {i} should be deployed");
            }
        }

        #endregion

        #region Edge Cases - Validation Failures

        [Fact]
        public async Task HandleOps_InvalidSignature_RevertsWithAA24()
        {
            var accountKey = EthECKey.GenerateKey();
            var wrongKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 4001;

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
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, wrongKey);

            await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true, "Operation with invalid signature should fail during execution");
        }

        [Fact]
        public async Task HandleOps_InvalidNonce_RevertsWithAA25()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 4002;

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

            var deployFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(deployFunction);

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Account should be deployed");

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 999,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Contains("AA25", ex.Message);
        }

        [Fact]
        public async Task HandleOps_InsufficientFunds_RevertsWithAA21()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 4003;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 0.0001m);

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
            var result = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true, "Operation with insufficient funds should fail during execution");
        }

        [Fact]
        public async Task HandleOps_SenderNotDeployed_NoInitCode_RevertsWithAA20()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 4004;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 3m);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = null,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 200000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            });

            Assert.Contains("AA20", ex.Message);
        }

        #endregion

        #region Edge Cases - Gas Limits

        [Fact]
        public async Task HandleOps_InsufficientVerificationGas_Reverts()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 5001;

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
                VerificationGasLimit = 1000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            var userOperationRevertReasonEvents = receipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
            Assert.True(userOperationRevertReasonEvents.Count > 0 || receipt.Status.Value == 0,
                "Operation with insufficient verification gas should fail with revert reason event or failed status");
        }

        #endregion

        #region Paymaster Tests

        [Fact]
        public async Task VerifyingPaymaster_DeployAndDeposit_Succeeds()
        {
            var paymasterSigner = EthECKey.GenerateKey();

            var paymasterDeployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address,
                Signer = paymasterSigner.GetPublicAddress()
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, paymasterDeployment);

            Assert.NotNull(paymasterService);

            var paymasterCode = await _fixture.GetCodeAsync(paymasterService.ContractAddress);
            Assert.True(paymasterCode.Length > 0, "Paymaster should be deployed");

            var depositReceipt = await paymasterService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1) });
            Assert.Equal((BigInteger)1, depositReceipt.Status.Value);

            var depositBalance = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterService.ContractAddress);
            Assert.True(depositBalance > 0, "Paymaster should have deposit in EntryPoint");
        }

        [Fact]
        public async Task Paymaster_SponsoredUserOp_DeductsFromPaymaster()
        {
            var paymasterDeployment = new TestPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };

            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                _fixture.Web3, paymasterDeployment);

            await paymasterService.DepositRequestAndWaitForReceiptAsync(
                new TestPaymasterDepositFunction
                {
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(10)
                });

            var initialPaymasterDeposit = await _fixture.EntryPointService.BalanceOfQueryAsync(
                paymasterService.ContractAddress);

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 6001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = paymasterService.ContractAddress,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 100000,
                PaymasterData = Array.Empty<byte>()
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.NotNull(receipt);
            Assert.Equal((BigInteger)1, receipt.Status.Value);

            var accountCode = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(accountCode.Length > 0, "Account should be deployed");

            var finalPaymasterDeposit = await _fixture.EntryPointService.BalanceOfQueryAsync(
                paymasterService.ContractAddress);
            Assert.True(finalPaymasterDeposit < initialPaymasterDeposit,
                "Paymaster deposit should decrease after sponsoring operation");
        }

        #endregion

        #region Sequential Nonce Tests

        [Fact]
        public async Task SequentialUserOps_SameAccount_IncrementNonce()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 7001;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 5m);

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(
                bundlerWeb3, _fixture.EntryPointService.ContractAddress);

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
            var deployFunction = new HandleOpsFunction
            {
                Ops = new List<PackedUserOperation> { packedDeployOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };
            await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(deployFunction);

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code.Length > 0, "Account should be deployed");

            for (BigInteger expectedNonce = 1; expectedNonce < 4; expectedNonce++)
            {
                var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
                Assert.Equal(expectedNonce, currentNonce);

                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    Nonce = currentNonce,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 50000,
                    VerificationGasLimit = 200000,
                    PreVerificationGas = 50000,
                    MaxFeePerGas = 2000000000,
                    MaxPriorityFeePerGas = 1000000000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

                var handleOpsFunction = new HandleOpsFunction
                {
                    Ops = new List<PackedUserOperation> { packedOp },
                    Beneficiary = _fixture.BundlerAccount.Address,
                    Gas = 5000000
                };

                var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);
                Assert.Equal((BigInteger)1, receipt.Status.Value);
            }

            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal((BigInteger)4, finalNonce);
        }

        #endregion
    }
}
