using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Xunit;
using UserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    /// <summary>
    /// Covers the EnableERC7562Validation config knob added to BundlerRpcServerConfig, mirroring
    /// the MaxVerificationGas host-config fix: the ERC-7562 opcode/storage validation engine
    /// (Nethereum.AccountAbstraction.Bundler.Validation.ERC7562) was already fully implemented
    /// and wired into UserOpValidator, but BundlerRpcServerConfig had no property to flip it on
    /// and ToBundlerConfig() never forwarded it, so the RpcServer host always ran with the
    /// engine OFF regardless of BundlerConfig.CreateStandardConfig/CreateProductionConfig
    /// defaulting it to true.
    ///
    /// These tests exercise the actual UserOpValidator.ValidateAsync path (not just the config
    /// plumbing) against a real deployed SimpleAccount on the shared dev chain, with
    /// SimulateValidation disabled so only structural validation + the ERC-7562 stage run -
    /// isolating the engine itself rather than the separate simulateValidation eth_call
    /// state-override path.
    /// </summary>
    public class Erc7562ValidationEnablementTests
    {
        [Fact]
        public void ToBundlerConfig_PropagatesEnableERC7562Validation()
        {
            var serverConfig = new BundlerRpcServerConfig
            {
                BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
                SupportedEntryPoints = new[] { "0x0000000000000000000000000000000000dEaD" },
                EnableERC7562Validation = true
            };

            var bundlerConfig = serverConfig.ToBundlerConfig();

            Assert.True(bundlerConfig.EnableERC7562Validation);
        }

        [Fact]
        public void EnableERC7562Validation_DefaultsToFalse()
        {
            var serverConfig = new BundlerRpcServerConfig();

            Assert.False(serverConfig.EnableERC7562Validation);
            Assert.False(serverConfig.ToBundlerConfig().EnableERC7562Validation);
        }
    }

    /// <summary>
    /// Proves that flipping the knob on actually constructs a working ERC7562SimulationService
    /// over a live IStateReader (Web3NodeDataServiceAdapter) and runs the engine end-to-end
    /// against a real node, using a clean, well-behaved UserOperation (already-deployed sender,
    /// no factory, no paymaster, an ETH-transfer execute() call that touches no storage besides
    /// the account's own) - i.e. an op a correct ERC-7562 engine must accept.
    /// </summary>
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class Erc7562ValidationEnablementStructuralTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public Erc7562ValidationEnablementStructuralTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ValidateAsync_WithErc7562Enabled_CleanTransferOp_ReportsEngineOutcome()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var recipient = "0x2222222222222222222222222222222222222222";
            var transferAmount = Nethereum.Web3.Web3.Convert.ToWei(0.001m);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.CreateSignedUserOperationAsync(
                accountAddress,
                accountKey,
                executeFunction.GetCallData());

            var serverConfig = new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId,
                EnableERC7562Validation = true
            };

            var bundlerConfig = serverConfig.ToBundlerConfig();
            // Isolate the ERC-7562 stage from the separate simulateValidation eth_call
            // state-override path (covered elsewhere) - structural validation + ERC-7562 only.
            bundlerConfig.SimulateValidation = false;
            bundlerConfig.StrictValidation = false;

            var validator = new UserOpValidator(_fixture.Web3, bundlerConfig);

            var result = await validator.ValidateAsync(userOp, _fixture.EntryPointService.ContractAddress);

            // KEY FINDING SURFACE: this must show whether the engine correctly accepts a clean,
            // well-behaved op end-to-end (IsValid = true), or false-rejects/crashes against a
            // live node - in which case result.Error carries the exact ERC-7562 violation or
            // exception text produced by ValidateERC7562Async.
            Assert.True(result.IsValid,
                $"ERC-7562 engine rejected a clean transfer UserOperation. Error: {result.Error}");
        }

        [Fact]
        public async Task ValidateAsync_WithErc7562Disabled_SameOp_StillValidatesStructurally()
        {
            // Baseline control: with the knob off (RpcServer default), the same op must still
            // pass structural validation - proves the ERC-7562 stage, not something else, is
            // responsible for the outcome observed above.
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var recipient = "0x2222222222222222222222222222222222222222";
            var transferAmount = Nethereum.Web3.Web3.Convert.ToWei(0.001m);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.CreateSignedUserOperationAsync(
                accountAddress,
                accountKey,
                executeFunction.GetCallData());

            var serverConfig = new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var bundlerConfig = serverConfig.ToBundlerConfig();
            bundlerConfig.SimulateValidation = false;
            bundlerConfig.StrictValidation = false;

            var validator = new UserOpValidator(_fixture.Web3, bundlerConfig);

            var result = await validator.ValidateAsync(userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
        }
    }
}
