using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class DevChainBundlerBasicTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public DevChainBundlerBasicTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Fixture_Initializes_WithEntryPointDeployed()
        {
            Assert.NotNull(_fixture.Node);
            Assert.NotNull(_fixture.Web3);
            Assert.NotNull(_fixture.EntryPointService);
            Assert.NotNull(_fixture.AccountFactoryService);
            Assert.NotNull(_fixture.BundlerService);

            var code = await _fixture.GetCodeAsync(_fixture.EntryPointService.ContractAddress);
            Assert.NotNull(code);
            Assert.True(code.Length > 0, "EntryPoint should have code deployed");
        }

        [Fact]
        public async Task Web3_CanTransferEther()
        {
            var recipient = "0x" + new string('1', 40);
            var initialBalance = await _fixture.GetBalanceAsync(recipient);

            var receipt = await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(recipient, 0.1m);

            Assert.NotNull(receipt);
            Assert.True(receipt.Status.Value == 1, "Transfer should succeed");

            var finalBalance = await _fixture.GetBalanceAsync(recipient);
            Assert.True(finalBalance > initialBalance, "Recipient balance should increase");
        }

        [Fact]
        public async Task SenderCreator_IsDeployedAndMatchesFactory()
        {
            // Check SenderCreator from EntryPoint
            var epSenderCreator = await _fixture.EntryPointService.SenderCreatorQueryAsync();
            Assert.NotNull(epSenderCreator);

            // Check SenderCreator expected by Factory
            var factorySenderCreator = await _fixture.AccountFactoryService.SenderCreatorQueryAsync();
            Assert.NotNull(factorySenderCreator);

            // They should match
            Assert.Equal(
                epSenderCreator.ToLower(),
                factorySenderCreator.ToLower());

            // Verify SenderCreator has code
            var code = await _fixture.GetCodeAsync(epSenderCreator);
            Assert.NotNull(code);
            Assert.True(code.Length > 0, $"SenderCreator should have code, address: {epSenderCreator}");
        }

        [Fact]
        public async Task CreateFundedAccount_DeploysSmartAccount()
        {
            var accountKey = Nethereum.Signer.EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 100;

            // Step 1: Get predicted address from factory
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            Assert.NotNull(accountAddress);

            // Step 2: Fund the predicted address (account needs to pay for gas)
            // Gas required: (verificationGas + callGas + preVerGas) * maxFeePerGas
            // = (500000 + 50000 + 50000) * 2 gwei = 1.2 ETH max
            await _fixture.FundAccountAsync(accountAddress, 2m);
            var balance = await _fixture.GetBalanceAsync(accountAddress);
            Assert.True(balance > 0, $"Account should be funded, balance: {balance}");

            // Step 3: Verify no code yet
            var codeBefore = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "Account should not have code before deployment");

            // Step 4: Get init code (factory address + createAccount calldata)
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);
            Assert.NotNull(initCode);
            Assert.Equal(88, initCode.Length); // 20 bytes address + 68 bytes calldata

            // Step 5: Create UserOperation with sender already set
            var userOp = new Nethereum.AccountAbstraction.UserOperation
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

            // Step 6: Sign the UserOperation
            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
            Assert.NotNull(packedOp);
            Assert.Equal(accountAddress.ToLower(), packedOp.Sender.ToLower());

            // Step 7: First try eth_call to get revert reason
            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new System.Collections.Generic.List<PackedUserOperation> { packedOp },
                Beneficiary = _fixture.BundlerAccount.Address,
                Gas = 5000000
            };

            var opInfo = $"Sender={packedOp.Sender}, Nonce={packedOp.Nonce}, InitCode={packedOp.InitCode?.Length}b, " +
                        $"AccountGasLimits={packedOp.AccountGasLimits?.Length}b, PreVerGas={packedOp.PreVerificationGas}, " +
                        $"GasFees={packedOp.GasFees?.Length}b, Signature={packedOp.Signature?.Length}b";

            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var bundlerEntryPoint = new Nethereum.AccountAbstraction.EntryPoint.EntryPointService(bundlerWeb3, _fixture.EntryPointService.ContractAddress);

            try
            {
                var callInput = handleOpsFunction.CreateCallInput(bundlerEntryPoint.ContractAddress);
                callInput.From = _fixture.BundlerAccount.Address;
                callInput.Gas = new Nethereum.Hex.HexTypes.HexBigInteger(5000000);
                var callResult = await bundlerWeb3.Eth.Transactions.Call.SendRequestAsync(callInput);
                if (!string.IsNullOrEmpty(callResult) && callResult != "0x")
                {
                    Assert.True(false, $"eth_call returned unexpected data: {callResult}\nUserOp: {opInfo}");
                }
            }
            catch (Nethereum.JsonRpc.Client.RpcResponseException rpcEx)
            {
                Assert.True(false, $"eth_call RPC error: {rpcEx.RpcError?.Message}, data={rpcEx.RpcError?.Data}\nUserOp: {opInfo}");
            }
            catch (Exception ex)
            {
                Assert.True(false, $"eth_call failed: {ex.GetType().Name}: {ex.Message}\nUserOp: {opInfo}");
            }

            try
            {
                var receipt = await bundlerEntryPoint.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

                if (receipt.Status?.Value != 1)
                {
                    var gasUsed = receipt.GasUsed?.Value ?? 0;
                    var logCount = receipt.Logs?.Count() ?? 0;

                    var senderBalance = await _fixture.GetBalanceAsync(packedOp.Sender);
                    var bundlerBalance = await _fixture.GetBalanceAsync(_fixture.BundlerAccount.Address);
                    var senderCode = await _fixture.GetCodeAsync(packedOp.Sender);

                    var balanceInfo = $"SenderBalance={senderBalance}, BundlerBalance={bundlerBalance}, SenderHasCode={(senderCode?.Length ?? 0) > 0}";

                    Assert.True(false, $"HandleOps reverted: TxHash={receipt.TransactionHash}, GasUsed={gasUsed}, Logs={logCount}\n{balanceInfo}\nUserOp: {opInfo}");
                }

                var codeAfter = await _fixture.GetCodeAsync(accountAddress);
                Assert.NotNull(codeAfter);
                Assert.True(codeAfter.Length > 0, "Smart account should have code deployed");
            }
            catch (SmartContractRevertException ex)
            {
                Assert.True(false, $"HandleOps revert exception: {ex.Message}\nUserOp: {opInfo}");
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                string errorDetail;
                if (ex.IsCustomErrorFor<FailedOpError>())
                {
                    var error = ex.DecodeError<FailedOpError>();
                    errorDetail = $"FailedOp[{error.OpIndex}]: {error.Reason}";
                }
                else if (ex.IsCustomErrorFor<FailedOpWithRevertError>())
                {
                    var error = ex.DecodeError<FailedOpWithRevertError>();
                    errorDetail = $"FailedOpWithRevert[{error.OpIndex}]: {error.Reason}, inner={error.Inner?.ToHex()}";
                }
                else
                {
                    errorDetail = $"CustomError: {ex.Message}";
                }
                Assert.True(false, $"HandleOps custom error: {errorDetail}\nUserOp: {opInfo}");
            }
        }

        [Fact]
        public async Task Web3_CanCallGetBalance()
        {
            var balance = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(_fixture.OperatorAccount.Address);
            Assert.True(balance.Value > 0, "Operator account should have balance");
        }

        [Fact]
        public async Task BundlerService_CanGetSupportedEntryPoints()
        {
            var entryPoints = await _fixture.BundlerService.SupportedEntryPointsAsync();

            Assert.NotNull(entryPoints);
            Assert.Single(entryPoints);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLowerInvariant(),
                entryPoints[0].ToLowerInvariant());
        }

        [Fact]
        public async Task EIP7702_CanSetupDelegatedEOA()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 1m);

            var balanceBefore = await _fixture.GetBalanceAsync(authorityAddress);
            Assert.True(balanceBefore > 0, "Authority should have balance");

            var codeBefore = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "EOA should not have code initially");

            var delegateContract = "0x1234567890123456789012345678901234567890";
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegateContract);

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);
        }

        [Fact]
        public async Task EIP712_ClientHashMatchesEntryPointHash()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 200;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new Nethereum.AccountAbstraction.UserOperation
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

            var clientHash = UserOperationBuilder.HashUserOperation(
                packedOp,
                _fixture.EntryPointService.ContractAddress,
                DevChainBundlerFixture.CHAIN_ID);

            var entryPointHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp);

            Assert.Equal(
                entryPointHash.ToHex().ToLower(),
                Sha3Keccack.Current.CalculateHash(clientHash).ToHex().ToLower());
        }

        [Fact]
        public async Task EIP712_SignatureCanRecoverOwner()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 201;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new Nethereum.AccountAbstraction.UserOperation
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

            var entryPointHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp);

            var recoveredAddress = new MessageSigner().EcRecover(entryPointHash, packedOp.Signature.ToHex(true));

            Assert.Equal(
                ownerAddress.ToLower(),
                recoveredAddress.ToLower());
        }

        [Fact]
        public async Task Signature_HasValidFormat()
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 202;

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            var userOp = new Nethereum.AccountAbstraction.UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            Assert.NotNull(packedOp.Signature);
            Assert.Equal(65, packedOp.Signature.Length);

            var v = packedOp.Signature[64];
            Assert.True(v == 27 || v == 28, $"Recovery ID (v) should be 27 or 28, got {v}");
        }

        [Fact]
        public async Task EntryPoint_DomainInfoMatchesExpected()
        {
            var domainInfo = await _fixture.EntryPointService.Eip712DomainQueryAsync();

            Assert.Equal("ERC4337", domainInfo.Name);
            Assert.Equal("1", domainInfo.Version);
            Assert.Equal(DevChainBundlerFixture.CHAIN_ID, (int)domainInfo.ChainId);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                domainInfo.VerifyingContract.ToLower());
        }
    }
}
