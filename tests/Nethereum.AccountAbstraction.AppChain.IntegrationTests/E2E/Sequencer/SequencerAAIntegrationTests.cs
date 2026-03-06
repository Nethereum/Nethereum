using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Xunit;

using NethereumAccountExecuteFunction = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Sequencer
{
    [Collection(SequencerAAFixture.COLLECTION_NAME)]
    public class SequencerAAIntegrationTests
    {
        private readonly SequencerAAFixture _fixture;

        public SequencerAAIntegrationTests(SequencerAAFixture fixture)
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

        private byte[] PrefixSignatureWithValidator(byte[] signature)
        {
            return ByteUtil.Merge(
                _fixture.ECDSAValidatorService.ContractAddress.HexToByteArray(),
                signature);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_AppChainWithSequencer_When_AAContractsDeployed_Then_EntryPointIsAccessible()
        {
            var entryPointCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(entryPointCode);
            Assert.NotEqual("0x", entryPointCode);
            Assert.True(entryPointCode.Length > 10, "EntryPoint should have deployed code");

            var blockNumber = await _fixture.GetBlockNumberAsync();
            Assert.True(blockNumber > 0, "Blocks should have been produced during contract deployment");
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_SmartAccount_When_HandleOpsCalled_Then_TransactionProcessedBySequencer()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = _fixture.UserAccounts[0];
            var recipient = _fixture.UserAccounts[1];
            var transferAmount = Web3.Web3.Convert.ToWei(0.5m);

            var salt = new byte[32];
            salt[0] = 10;
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

            var recipientBalanceBefore = await _fixture.GetBalanceAsync(recipient.Address);
            var blockNumberBefore = await _fixture.GetBlockNumberAsync();

            var callData = CreateERC7579ExecuteCallData(recipient.Address, transferAmount, Array.Empty<byte>());

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
                SequencerAAFixture.CHAIN_ID,
                signerKey);
            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle execution failed: {bundleResult.Error}");
            Assert.NotNull(bundleResult.TransactionHash);

            var blockNumberAfter = await _fixture.GetBlockNumberAsync();
            Assert.True(blockNumberAfter > blockNumberBefore,
                "Sequencer should have produced a new block for the bundle transaction");

            var recipientBalanceAfter = await _fixture.GetBalanceAsync(recipient.Address);
            Assert.Equal(recipientBalanceBefore + transferAmount, recipientBalanceAfter);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_Bundler_When_SubmitsHandleOps_Then_TransactionGoesThrough_SequencerTxPool()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = _fixture.UserAccounts[2];
            var recipient = _fixture.OperatorAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            var salt = new byte[32];
            salt[0] = 20;
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

            var callData = CreateERC7579ExecuteCallData(recipient.Address, transferAmount, Array.Empty<byte>());

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
                SequencerAAFixture.CHAIN_ID,
                signerKey);
            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle should be executed via sequencer: {bundleResult.Error}");

            var txReceipt = await _fixture.Web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(bundleResult.TransactionHash);

            Assert.NotNull(txReceipt);
            Assert.True(txReceipt.Status.Value == 1, "HandleOps transaction should succeed");
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_MultipleUserOps_When_BundleExecuted_Then_AllProcessedInSingleBlock()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccounts = new[] { _fixture.UserAccounts[3], _fixture.UserAccounts[4] };
            var recipient = _fixture.OperatorAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.05m);

            var smartAccountAddresses = new List<string>();

            for (int i = 0; i < userAccounts.Length; i++)
            {
                var userAccount = userAccounts[i];
                var salt = new byte[32];
                salt[0] = (byte)(30 + i);
                var initData = _fixture.EncodeInitData(userAccount.Address);

                var smartAccountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(
                    new GetAddressFunction
                    {
                        Salt = salt,
                        InitData = initData
                    });

                smartAccountAddresses.Add(smartAccountAddress);

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
            }

            var recipientBalanceBefore = await _fixture.GetBalanceAsync(recipient.Address);

            for (int i = 0; i < userAccounts.Length; i++)
            {
                var userAccount = userAccounts[i];
                var smartAccountAddress = smartAccountAddresses[i];

                var callData = CreateERC7579ExecuteCallData(recipient.Address, transferAmount, Array.Empty<byte>());

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
                    SequencerAAFixture.CHAIN_ID,
                    signerKey);
                packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

                await _fixture.BundlerService.SendUserOperationAsync(
                    packedUserOp,
                    _fixture.EntryPointService.ContractAddress);
            }

            var pendingOps = await _fixture.BundlerService.GetPendingUserOperationsAsync();
            Assert.Equal(2, pendingOps.Length);

            var blockNumberBefore = await _fixture.GetBlockNumberAsync();

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle with multiple UserOps should succeed: {bundleResult.Error}");

            var blockNumberAfter = await _fixture.GetBlockNumberAsync();
            Assert.True(blockNumberAfter >= blockNumberBefore + 1,
                $"At least one block should be produced for the bundle. Before: {blockNumberBefore}, After: {blockNumberAfter}");

            var recipientBalanceAfter = await _fixture.GetBalanceAsync(recipient.Address);
            var expectedIncrease = transferAmount * userAccounts.Length;
            Assert.Equal(recipientBalanceBefore + expectedIncrease, recipientBalanceAfter);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_AccountWithInitCode_When_UserOpSubmitted_Then_AccountCreatedAndExecuted()
        {
            await _fixture.ResetBundlerServiceAsync();

            var userAccount = new Account(
                "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d",
                SequencerAAFixture.CHAIN_ID);
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

            var isDeployedBefore = await _fixture.AccountFactoryService.IsDeployedQueryAsync(
                new IsDeployedFunction
                {
                    Salt = salt,
                    InitData = initData
                });
            Assert.False(isDeployedBefore, "Account should not be deployed yet");

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

            var callData = CreateERC7579ExecuteCallData(recipient.Address, transferAmount, Array.Empty<byte>());

            var recipientBalanceBefore = await _fixture.GetBalanceAsync(recipient.Address);

            var userOp = new UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = BigInteger.Zero,
                InitCode = initCode,
                CallData = callData,
                CallGasLimit = 100000,
                VerificationGasLimit = 600000,
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
                SequencerAAFixture.CHAIN_ID,
                signerKey);
            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                packedUserOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);

            var bundleResult = await _fixture.BundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle with initCode should succeed: {bundleResult.Error}");

            var isDeployedAfter = await _fixture.AccountFactoryService.IsDeployedQueryAsync(
                new IsDeployedFunction
                {
                    Salt = salt,
                    InitData = initData
                });
            Assert.True(isDeployedAfter, "Account should be deployed after UserOp execution");

            var recipientBalanceAfter = await _fixture.GetBalanceAsync(recipient.Address);
            Assert.Equal(recipientBalanceBefore + transferAmount, recipientBalanceAfter);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);
            Assert.Equal(BigInteger.One, nonce);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Integration")]
        public async Task Given_SequencerConfig_When_BlocksProduced_Then_CorrectBlockHeaders()
        {
            var blockNumber = await _fixture.GetBlockNumberAsync();
            var latestBlock = await _fixture.Sequencer.GetLatestBlockAsync();

            Assert.NotNull(latestBlock);
            Assert.Equal(blockNumber, latestBlock.BlockNumber);
            Assert.NotNull(latestBlock.StateRoot);
            Assert.NotNull(latestBlock.TransactionsHash);
            Assert.Equal(SequencerAAFixture.CHAIN_ID, _fixture.AppChain.Config.ChainId);
        }
    }
}
