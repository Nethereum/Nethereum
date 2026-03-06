using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Xunit;
using NethereumAccountExecuteFunction = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.UserOperations
{
    [Collection(AppChainE2EFixture.COLLECTION_NAME)]
    public class UserOperationE2ETests
    {
        private readonly AppChainE2EFixture _fixture;

        public UserOperationE2ETests(AppChainE2EFixture fixture)
        {
            _fixture = fixture;
        }

        private byte[] CreateERC7579ExecuteCallData(string target, BigInteger value, byte[] data)
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeSingle(target, value, data);

            var executeFunction = new NethereumAccountExecuteFunction
            {
                Mode = mode,
                ExecutionCalldata = executionCalldata
            };

            return executeFunction.GetCallData();
        }

        private byte[] CreateERC7579BatchExecuteCallData(Call[] calls)
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeBatch(calls);

            var executeFunction = new NethereumAccountExecuteFunction
            {
                Mode = mode,
                ExecutionCalldata = executionCalldata
            };

            return executeFunction.GetCallData();
        }

        private byte[] PrefixSignatureWithValidator(byte[] signature)
        {
            return ByteUtil.Merge(
                _fixture.ECDSAValidatorService.ContractAddress.HexToByteArray(),
                signature);
        }

        [Fact]
        public async Task UC3_1_SimpleETHTransfer_ViaUserOp_TransfersSuccessfully()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = _fixture.UserAccounts[0];
            var recipientAccount = _fixture.UserAccounts[1];
            var transferAmount = Web3.Web3.Convert.ToWei(1);

            var salt = new byte[32];
            salt[0] = 100;
            var initData = _fixture.EncodeInitData(userAccount.Address);

            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            await _fixture.SetBalanceAsync(smartAccountAddress, Web3.Web3.Convert.ToWei(10));

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);

            var recipientBalanceBefore = await _fixture.GetBalanceAsync(recipientAccount.Address);

            var callData = CreateERC7579ExecuteCallData(
                recipientAccount.Address,
                transferAmount,
                Array.Empty<byte>());

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = new UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = callData,
                CallGasLimit = 100000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0
            };

            var signerKey = new EthECKey(userAccount.PrivateKey);
            var packedUserOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOp,
                _fixture.EntryPointService.ContractAddress,
                AppChainE2EFixture.CHAIN_ID,
                signerKey);

            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();
            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"HandleOps should succeed: {bundleResult.Error}");

            var recipientBalanceAfter = await _fixture.GetBalanceAsync(recipientAccount.Address);
            Assert.Equal(recipientBalanceBefore + transferAmount, recipientBalanceAfter);
        }

        [Fact]
        public async Task UC3_3_BatchOperations_ViaUserOp_ExecutesAtomically()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = _fixture.UserAccounts[2];
            var recipient1 = _fixture.UserAccounts[3];
            var recipient2 = _fixture.UserAccounts[4];
            var transferAmount1 = Web3.Web3.Convert.ToWei(0.5m);
            var transferAmount2 = Web3.Web3.Convert.ToWei(0.3m);

            var salt = new byte[32];
            salt[0] = 1;
            var initData = _fixture.EncodeInitData(userAccount.Address);

            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            await _fixture.SetBalanceAsync(smartAccountAddress, Web3.Web3.Convert.ToWei(10));

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);

            var balance1Before = await _fixture.GetBalanceAsync(recipient1.Address);
            var balance2Before = await _fixture.GetBalanceAsync(recipient2.Address);

            var calls = new[]
            {
                new Call { Target = recipient1.Address, Value = transferAmount1, Data = Array.Empty<byte>() },
                new Call { Target = recipient2.Address, Value = transferAmount2, Data = Array.Empty<byte>() }
            };

            var callData = CreateERC7579BatchExecuteCallData(calls);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = new UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = callData,
                CallGasLimit = 150000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0
            };

            var signerKey = new EthECKey(userAccount.PrivateKey);
            var packedUserOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOp,
                _fixture.EntryPointService.ContractAddress,
                AppChainE2EFixture.CHAIN_ID,
                signerKey);

            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle execution failed: {bundleResult.Error}");

            var balance1After = await _fixture.GetBalanceAsync(recipient1.Address);
            var balance2After = await _fixture.GetBalanceAsync(recipient2.Address);

            Assert.Equal(balance1Before + transferAmount1, balance1After);
            Assert.Equal(balance2Before + transferAmount2, balance2After);

            var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
            Assert.Equal(nonce + 1, newNonce);
        }

        [Fact]
        public async Task UC3_4_AccountCreationWithExecution_CombinedInSingleUserOp()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = new Account(
                "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a",
                AppChainE2EFixture.CHAIN_ID);
            var recipient = _fixture.OperatorAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            var salt = new byte[32];
            salt[0] = 99;
            var initData = _fixture.EncodeInitData(userAccount.Address);

            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            await _fixture.SetBalanceAsync(smartAccountAddress, Web3.Web3.Convert.ToWei(10));

            var isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(
                new IsDeployedFunction
                {
                    Salt = salt,
                    InitData = initData
                });
            Assert.False(isDeployed);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });
            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(smartAccountAddress);
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            var initCode = ByteUtil.Merge(
                _fixture.AccountFactoryService.ContractAddress.HexToByteArray(),
                createAccountFunction.GetCallData());

            var callData = CreateERC7579ExecuteCallData(
                recipient.Address,
                transferAmount,
                Array.Empty<byte>());

            var recipientBalanceBefore = await _fixture.GetBalanceAsync(recipient.Address);

            var userOp = new UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = BigInteger.Zero,
                InitCode = initCode,
                CallData = callData,
                CallGasLimit = 100000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 100000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0
            };

            var signerKey = new EthECKey(userAccount.PrivateKey);
            var packedUserOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOp,
                _fixture.EntryPointService.ContractAddress,
                AppChainE2EFixture.CHAIN_ID,
                signerKey);

            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle execution failed: {bundleResult.Error}");

            isDeployed = await _fixture.AccountFactoryService.IsDeployedQueryAsync(
                new IsDeployedFunction
                {
                    Salt = salt,
                    InitData = initData
                });
            Assert.True(isDeployed);

            var recipientBalanceAfter = await _fixture.GetBalanceAsync(recipient.Address);
            Assert.Equal(recipientBalanceBefore + transferAmount, recipientBalanceAfter);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
            Assert.Equal(BigInteger.One, nonce);
        }

        [Fact]
        public async Task UC3_2_NonceIncrementsCorrectly_AfterSuccessfulUserOp()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = new Account(
                "0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6",
                AppChainE2EFixture.CHAIN_ID);
            var recipient = _fixture.UserAccounts[0];

            var salt = new byte[32];
            salt[0] = 50;
            var initData = _fixture.EncodeInitData(userAccount.Address);

            var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            await _fixture.SetBalanceAsync(smartAccountAddress, Web3.Web3.Convert.ToWei(10));

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            await _fixture.AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);

            var initialNonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
            Assert.Equal(BigInteger.Zero, initialNonce);

            for (int i = 0; i < 3; i++)
            {
                var callData = CreateERC7579ExecuteCallData(
                    recipient.Address,
                    Web3.Web3.Convert.ToWei(0.01m),
                    Array.Empty<byte>());

                var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

                var userOp = new UserOperation
                {
                    Sender = smartAccountAddress,
                    Nonce = currentNonce,
                    InitCode = Array.Empty<byte>(),
                    CallData = callData,
                    CallGasLimit = 100000,
                    VerificationGasLimit = 500000,
                    PreVerificationGas = 50000,
                    MaxFeePerGas = 2000000000,
                    MaxPriorityFeePerGas = 1000000000,
                    Paymaster = AddressUtil.ZERO_ADDRESS,
                    PaymasterData = Array.Empty<byte>(),
                    PaymasterVerificationGasLimit = 0,
                    PaymasterPostOpGasLimit = 0
                };

                var signerKey = new EthECKey(userAccount.PrivateKey);
                var packedUserOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                    userOp,
                    _fixture.EntryPointService.ContractAddress,
                    AppChainE2EFixture.CHAIN_ID,
                    signerKey);

                packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

                await _fixture.BundlerService.SendUserOperationAsync(
                    packedUserOp,
                    _fixture.EntryPointService.ContractAddress);

                var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();
                Assert.NotNull(bundleResult);
                Assert.True(bundleResult.Success, $"Bundle {i} execution failed: {bundleResult.Error}");

                var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
                Assert.Equal(currentNonce + 1, newNonce);
            }

            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
            Assert.Equal(new BigInteger(3), finalNonce);
        }
    }
}
