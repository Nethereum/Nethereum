using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    /// <summary>
    /// Batch-4 flagship #2 ("your existing EOA becomes smart"): the client-side
    /// <see cref="AAContractHandler.WithEip7702Delegation"/> convenience sends a userOp from a plain,
    /// code-less EOA that is delegated to an account implementation in the SAME bundle via EIP-7702.
    /// The handler signs the 7702 authorisation tuple with the sender's key (the authority == the
    /// sender), rides it on the userOp, and our bundler applies the delegation on-chain and runs the op.
    /// </summary>
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    [Trait("Rule", "ERC4337-ContractHandler-7702")]
    public class Eip7702AAContractHandlerTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eip7702AAContractHandlerTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task WithEip7702Delegation_SendsUserOpFromPlainEoa_DelegatesOnChainAndExecutes()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();

            var delegateAccount = await DeployRuntimeAsync("600060005260206000f3");

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = senderAddress,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m)
                });

            var codeBefore = await _fixture.GetCodeAsync(senderAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "sender must start as a code-less EOA");

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = new AAContractHandler(
                testCounter.ContractAddress,
                senderAddress,
                senderKey,
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                _fixture.EntryPointService.ContractAddress,
                _fixture.Web3)
                .WithEip7702Delegation(delegateAccount);

            var receipt = await handler.SendRequestAndWaitForReceiptAsync(new CountFunction());
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            _output.WriteLine($"userOpSuccess={aaReceipt.UserOpSuccess}, sender={aaReceipt.Sender}, revert={aaReceipt.RevertReason}");

            Assert.True(aaReceipt.UserOpSuccess, $"userOp should succeed. Revert: {aaReceipt.RevertReason}");
            Assert.Equal(senderAddress.ToLowerInvariant(), aaReceipt.Sender?.ToLowerInvariant());

            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(delegateAccount.ToLowerInvariant(), delegated.ToLowerInvariant());
        }

        [Fact]
        public async Task ChangeContractHandlerToAA_EthECKeyOverload_WithEip7702Delegation_SignsAuthorisationAndExecutes()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();

            var delegateAccount = await DeployRuntimeAsync("600060005260206000f3");

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = senderAddress,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m)
                });

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = testCounter.ChangeContractHandlerToAA(
                    senderAddress,
                    senderKey,
                    new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                    _fixture.EntryPointService.ContractAddress)
                .WithEip7702Delegation(delegateAccount);

            var receipt = await handler.SendRequestAndWaitForReceiptAsync(new CountFunction());
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);

            Assert.True(aaReceipt.UserOpSuccess, $"userOp should succeed. Revert: {aaReceipt.RevertReason}");
            Assert.Equal(senderAddress.ToLowerInvariant(), aaReceipt.Sender?.ToLowerInvariant());

            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(delegateAccount.ToLowerInvariant(), delegated.ToLowerInvariant());
        }

        /// <summary>
        /// Regression for two real gaps the flagship test above papers over: its delegate is a
        /// permissive stub that accepts any call but never forwards it, so <c>factoryData</c> stays null
        /// and the userOp's initCode is empty - none of this exercises the real "your EOA becomes a
        /// modular smart account" path (delegate to NethereumAccountFactory.accountImplementation, run
        /// initializeAccount as the post-delegation init). The moment a caller supplies non-null
        /// <c>factoryData</c>, two bugs surfaced, both fixed alongside this test:
        /// (1) <c>EntryPointService.InitialiseUserOperationAsync</c> read back the sender's on-chain
        /// delegation to validate it BEFORE this very op had a chance to apply it, throwing "Must provide
        /// eip7702delegate ..." for every legitimate first-time upgrade - fixed by threading the
        /// already-known delegate through instead of requiring an on-chain read (the same gap existed in
        /// the bundler's <c>UserOpValidator.EstimateGasAsync</c>, fixed alongside it, though that method
        /// is not on the path <see cref="AAContractHandler"/> actually estimates through - see
        /// <c>SimulationGasEstimator</c>, which already handles EIP-7702 state-override estimation
        /// correctly).
        /// (2) <c>SignAndInitialiseUserOperationAsync</c> substitutes the initCode's marker with the real
        /// delegate address to compute the correct EIP-712 signing hash (matching the EntryPoint's own
        /// <c>Eip7702Support._getEip7702InitCodeHashOverride</c>), but was RETURNING the packed op with
        /// that substituted initCode still attached - on-chain, that no longer starts with the literal
        /// 0x7702 marker, so <c>Eip7702Support._isEip7702InitCode</c> sees a plain factory address whose
        /// sender already has code (the delegation already applied) and silently skips the init entirely
        /// (<c>IgnoredInitCode</c>) - the validator is never installed, and validation fails on-chain with
        /// "AA24 signature error" (or "AA21 didn't pay prefund" first, if the account has no EntryPoint
        /// deposit to fall back on). Fixed by restoring the original wire-format initCode onto the packed
        /// op after computing the substituted-hash signature.
        /// </summary>
        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "eip7702-smart-accounts", "Low-level: WithEip7702Delegation + WithErc7579Execution installs the validator on delegation and executes", Order = 2)]
        public async Task WithEip7702Delegation_FactoryData_InstallsValidatorOnModularAccount_SendsUserOpFromPlainEoa_DelegatesAndExecutes()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();
            await _fixture.FundAccountAsync(senderAddress, 5m);

            var codeBefore = await _fixture.GetCodeAsync(senderAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "sender must start as a code-less EOA");

            var ecdsaValidator = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ECDSAValidatorDeployment());
            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new NethereumAccountFactoryDeployment { EntryPoint = _fixture.EntryPointService.ContractAddress });
            var accountImplementation = await factory.AccountImplementationQueryAsync();

            var validator = new EcdsaValidatorModule(ecdsaValidator.ContractAddress);
            var initData = AccountInitDataBuilder.BuildEcdsa(ecdsaValidator.ContractAddress, senderAddress);
            var factoryData = new InitializeAccountFunction { InitData = initData }.GetCallData();

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = new AAContractHandler(
                    testCounter.ContractAddress,
                    senderAddress,
                    senderKey,
                    validator,
                    new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                    _fixture.EntryPointService.ContractAddress,
                    _fixture.Web3)
                .WithEip7702Delegation(accountImplementation, factoryData)
                .WithErc7579Execution();

            var receipt = await handler.SendRequestAndWaitForReceiptAsync(new CountFunction());
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            _output.WriteLine($"userOpSuccess={aaReceipt.UserOpSuccess}, sender={aaReceipt.Sender}, revert={aaReceipt.RevertReason}");

            Assert.True(aaReceipt.UserOpSuccess, $"userOp should succeed. Revert: {aaReceipt.RevertReason}");
            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(accountImplementation.ToLowerInvariant(), delegated.ToLowerInvariant());

            var count = await testCounter.CountersQueryAsync(senderAddress);
            Assert.Equal(BigInteger.One, count);
        }

        /// <summary>
        /// Backs the package README's EIP-7702 "upgrade an existing EOA in place" example, which is
        /// written against the CLIENT-level on-ramp - <see cref="Eip7702AAClientExtensions.CreateEip7702Account"/>
        /// then <see cref="AAClient.ConfigureEip7702{TService}"/> - not the hand-built
        /// <see cref="AAContractHandler"/> construction the test above exercises. Same real modular-account
        /// stack (ECDSAValidator + NethereumAccountFactory's accountImplementation) and same on-chain
        /// assertions (delegation designator, validator install, TestCounter.count() landing), but driven
        /// exclusively through the two documented <see cref="IAAClient"/> methods, proving the README's code
        /// sample is real and passing rather than merely equivalent-by-reading to the handler-level test.
        /// </summary>
        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "send-useroperation", "EIP-7702: upgrade a plain EOA in place to a modular smart account and execute", Order = 7)]
        [NethereumDocExample(DocSection.AccountAbstraction, "eip7702-smart-accounts", "The Simple Way: CreateEip7702Account + ConfigureEip7702 upgrades a plain EOA in place and executes", Order = 1)]
        public async Task Given_ClientCreateEip7702AccountAndConfigureEip7702_When_SendUserOp_Then_DelegatesOnChainAndExecutes()
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            await _fixture.FundAccountAsync(ownerAddress, 5m);

            var codeBefore = await _fixture.GetCodeAsync(ownerAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "sender must start as a code-less EOA");

            var ecdsaValidator = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ECDSAValidatorDeployment());
            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new NethereumAccountFactoryDeployment { EntryPoint = _fixture.EntryPointService.ContractAddress });
            var accountImplementation = await factory.AccountImplementationQueryAsync();

            var deploymentAddresses = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                factory.ContractAddress,
                ecdsaValidator.ContractAddress,
                VerifyingPaymasterAddress: string.Empty);
            var bundler = new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID);
            IAAClient client = new AAClient(_fixture.Web3, deploymentAddresses, bundler);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var account = client.CreateEip7702Account(ownerKey, ecdsaValidator.ContractAddress);
            client.ConfigureEip7702(testCounter, account, ownerKey, accountImplementation);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            _output.WriteLine($"userOpSuccess={aaReceipt.UserOpSuccess}, sender={aaReceipt.Sender}, revert={aaReceipt.RevertReason}");

            Assert.True(aaReceipt.UserOpSuccess, $"userOp should succeed. Revert: {aaReceipt.RevertReason}");
            Assert.Equal(ownerAddress.ToLowerInvariant(), aaReceipt.Sender?.ToLowerInvariant());

            var codeAfter = await _fixture.GetCodeAsync(ownerAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(accountImplementation.ToLowerInvariant(), delegated.ToLowerInvariant());

            var count = await testCounter.CountersQueryAsync(ownerAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        public async Task RawEip7702DelegationPlusInitializeAccount_InstallsValidator_IndependentOfEntryPoint()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);

            var ecdsaValidator = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ECDSAValidatorDeployment());
            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _fixture.Web3,
                new NethereumAccountFactoryDeployment { EntryPoint = _fixture.EntryPointService.ContractAddress });
            var accountImplementation = await factory.AccountImplementationQueryAsync();

            var initData = AccountInitDataBuilder.BuildEcdsa(ecdsaValidator.ContractAddress, authorityAddress);
            var factoryData = new InitializeAccountFunction { InitData = initData }.GetCallData();

            var auth = _fixture.SignAuthorization(authorityKey, accountImplementation, nonce: 0);
            var signedTx = _fixture.CreateType4Transaction(
                await _fixture.Node.GetNonceAsync(_fixture.OperatorAccount.Address),
                authorityAddress,
                new System.Collections.Generic.List<Nethereum.Model.Authorisation7702Signed> { auth },
                gasLimit: 2_000_000,
                data: factoryData);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"raw 7702-delegate+initializeAccount failed: {result.RevertReason}");

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.Equal(23, codeAfter.Length);

            var accountService = new NethereumAccountService(_fixture.Web3, authorityAddress);
            var installed = await accountService.IsModuleInstalledQueryAsync(1, ecdsaValidator.ContractAddress, System.Array.Empty<byte>());
            Assert.True(installed);
        }

        [Fact]
        public async Task WithoutEip7702Delegation_LeavesUserOpUnchanged_NoAuthAttached()
        {
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt: 87021, ethAmount: 2m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = new AAContractHandler(
                testCounter.ContractAddress,
                accountAddress,
                accountKey,
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                _fixture.EntryPointService.ContractAddress,
                _fixture.Web3);

            var packedOp = await handler.CreateUserOperationAsync(new CountFunction());

            Assert.Null(handler.Eip7702DelegationConfig);
            Assert.True(packedOp.InitCode == null || packedOp.InitCode.Length == 0,
                "a non-7702 handler must not synthesise a 0x7702 initCode");
        }

        private async Task<string> DeployRuntimeAsync(string runtimeHex)
        {
            var runtimeLen = runtimeHex.Length / 2;
            var pushOpcode = (0x60 + runtimeLen - 1).ToString("x2");
            var lenHex = runtimeLen.ToString("x2");
            var offsetHex = (32 - runtimeLen).ToString("x2");
            var initCode = $"{pushOpcode}{runtimeHex}600052{"60" + lenHex}{"60" + offsetHex}f3";

            var nonce = await _fixture.Node.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTxHex = new LegacyTransactionSigner().SignTransaction(
                _fixture.OperatorPrivateKey.Substring(2).HexToByteArray(),
                DevChainBundlerFixture.CHAIN_ID,
                "",
                BigInteger.Zero,
                nonce,
                1_000_000_000,
                3_000_000,
                initCode);
            var tx = Nethereum.Model.TransactionFactory.CreateTransaction(signedTxHex);
            var result = await _fixture.Node.SendTransactionAsync(tx);
            Assert.True(result.Success, $"delegate account deployment failed: {result.RevertReason}");
            var txReceipt = await _fixture.Node.GetTransactionReceiptInfoAsync(tx.Hash);
            return txReceipt.ContractAddress;
        }
    }
}
