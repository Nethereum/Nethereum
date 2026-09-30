using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Signing;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(WebAuthnModularAccountBundlerCollection.COLLECTION_NAME)]
    public class WebAuthnUserOpE2ETests
    {
        private readonly WebAuthnModularAccountBundlerFixture _fixture;

        public WebAuthnUserOpE2ETests(WebAuthnModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(string accountAddress, byte[] initCode)> CreateCounterfactualAccountAsync(
            System.Numerics.BigInteger pubKeyX, System.Numerics.BigInteger pubKeyY)
        {
            var salt = WebAuthnModularAccountBundlerFixture.CreateSalt((ulong)Random.Shared.NextInt64());
            var initData = WebAuthnAccountInitDataBuilder.BuildWebAuthn(
                _fixture.WebAuthnValidatorService.ContractAddress, pubKeyX, pubKeyY, requireUV: false);

            var accountAddress = await _fixture.FactoryService.GetAddressQueryAsync(salt, initData);
            await _fixture.Bootstrap.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(1));

            var initCodeBuilder = new NethereumAccountInitCodeBuilder(_fixture.FactoryService.ContractAddress, salt, initData);
            var initCode = Nethereum.Util.ByteUtil.Merge(
                initCodeBuilder.FactoryAddress.HexToByteArray(),
                initCodeBuilder.BuildFactoryData());

            return (accountAddress, initCode);
        }

        private UserOperation BuildCountUserOperation(string accountAddress, byte[] initCode)
        {
            var countCallData = new CountFunction().GetCallData();
            var executeCallData = new ExecuteFunction
            {
                Mode = ERC7579ModeLib.EncodeSingleDefault(),
                ExecutionCalldata = ERC7579ExecutionLib.EncodeSingle(_fixture.TestCounterService.ContractAddress, 0, countCallData)
            }.GetCallData();

            return new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = executeCallData,
                CallGasLimit = 400_000,
                VerificationGasLimit = 1_500_000,
                PreVerificationGas = 100_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
        }

        [Fact]
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        public async Task Given_software_P256_authenticator_When_create_account_and_send_op_Then_onchain_WebAuthnValidator_accepts_and_op_lands()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var (pubKeyX, pubKeyY) = authenticator.GetPublicKey();
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(pubKeyX, pubKeyY);

            var (accountAddress, initCode) = await CreateCounterfactualAccountAsync(pubKeyX, pubKeyY);

            var codeBeforeDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(accountAddress);
            Assert.True(codeBeforeDeploy == null || codeBeforeDeploy.Length == 0);

            var userOp = BuildCountUserOperation(accountAddress, initCode);

            var signingService = new WebAuthnAccountSigningService(authenticator, credentialId, rpId: "nethereum.local");
            var validator = new WebAuthnValidatorModule(_fixture.WebAuthnValidatorService.ContractAddress);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signingService, validator);
            var rpcOp = UserOperationConverter.ToRpcFormat(packedOp);

            var userOpHash = await _fixture.Bundler.SendUserOperation.SendRequestAsync(rpcOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(userOpHash));

            var receipt = await _fixture.Bundler.GetUserOperationReceipt.SendRequestAsync(userOpHash);
            Assert.NotNull(receipt);
            Assert.True(receipt.Success, receipt.Reason);

            var codeAfterDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(accountAddress);
            Assert.NotNull(codeAfterDeploy);
            Assert.True(codeAfterDeploy.Length > 0);

            var count = await _fixture.TestCounterService.CountersQueryAsync(accountAddress);
            Assert.Equal(System.Numerics.BigInteger.One, count);
        }

        [Fact]
        [Trait("UseCase", "WebAuthnValidator")]
        [Trait("Doc", "docs/aa/webauthn.md")]
        public async Task Given_op_signed_by_a_different_P256_key_than_the_registered_credential_Then_onchain_validation_rejects()
        {
            var registeredAuthenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var (registeredX, registeredY) = registeredAuthenticator.GetPublicKey();
            var registeredCredentialId = WebAuthnValidatorFormat.GenerateCredentialId(registeredX, registeredY);

            var (accountAddress, initCode) = await CreateCounterfactualAccountAsync(registeredX, registeredY);

            var userOp = BuildCountUserOperation(accountAddress, initCode);

            var wrongKeyAuthenticator = new SoftwareWebAuthnAuthenticator(requireUV: false, origin: "https://nethereum.local");
            var signingService = new WebAuthnAccountSigningService(wrongKeyAuthenticator, registeredCredentialId, rpId: "nethereum.local");
            var validator = new WebAuthnValidatorModule(_fixture.WebAuthnValidatorService.ContractAddress);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signingService, validator);
            var rpcOp = UserOperationConverter.ToRpcFormat(packedOp);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                _fixture.Bundler.SendUserOperation.SendRequestAsync(rpcOp, _fixture.EntryPointService.ContractAddress));

            var code = ex switch
            {
                BundlerRpcException bundlerEx => bundlerEx.Code,
                RpcResponseException rpcEx => rpcEx.RpcError.Code,
                _ => throw new Exception($"Unexpected exception type {ex.GetType()}: {ex.Message}", ex)
            };
            Assert.Equal(BundlerErrorCodes.InvalidSignature, code);
            Assert.Contains("AA24", ex.Message);

            var codeAfterAttempt = await _fixture.Bootstrap.Node.GetCodeAsync(accountAddress);
            Assert.True(codeAfterAttempt == null || codeAfterAttempt.Length == 0);
        }
    }
}
