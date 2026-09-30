using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SmartSession
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "SmartSession")]
    public class SmartSessionBehaviorTests
    {
        private readonly ERC7579TestFixture _fixture;
        private SmartSessionService _smartSessionService;

        public SmartSessionBehaviorTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SmartSessionService> GetSmartSessionServiceAsync()
        {
            if (_smartSessionService == null)
            {
                _smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                    _fixture.Web3, new SmartSessionDeployment());
            }
            return _smartSessionService;
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingModuleType_Then_ReturnsValidatorType()
        {
            var sessionService = await GetSmartSessionServiceAsync();

            var isValidator = await sessionService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingExecutorType_Then_ReturnsFalse()
        {
            var sessionService = await GetSmartSessionServiceAsync();

            var isExecutor = await sessionService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.False(isExecutor);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_UsingFluentAPI_Then_SessionIsConfigured()
        {
            var sessionValidator = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 1;

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionValidator)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            Assert.Equal(sessionValidator, config.SessionValidator);
            Assert.Equal(salt, config.Salt);
            Assert.True(config.PermitERC4337Paymaster);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingUserOpPolicy_Then_PolicyIsAdded()
        {
            var policyAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var initData = new byte[] { 1, 2, 3, 4 };

            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithUserOpPolicy(policyAddress, initData);

            Assert.Single(config.UserOpPolicies);
            Assert.Equal(policyAddress, config.UserOpPolicies[0].Policy);
            Assert.Equal(initData, config.UserOpPolicies[0].InitData);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingMultiplePolicies_Then_AllPoliciesAreAdded()
        {
            var policy1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var policy2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var policy3 = "0xcccccccccccccccccccccccccccccccccccccccc";

            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithUserOpPolicy(policy1)
                .WithUserOpPolicy(policy2)
                .WithUserOpPolicy(policy3);

            Assert.Equal(3, config.UserOpPolicies.Count);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingSudoPolicy_Then_PolicyHasEmptyInitData()
        {
            var sudoPolicyAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithSudoPolicy(sudoPolicyAddress);

            Assert.Single(config.UserOpPolicies);
            Assert.Equal(sudoPolicyAddress, config.UserOpPolicies[0].Policy);
            Assert.Empty(config.UserOpPolicies[0].InitData);
        }

        [Fact]
        public void Given_SmartSessionConfigWithNoSalt_When_ConvertingToSession_Then_Throws()
        {
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890");

            Assert.Throws<InvalidOperationException>(() => config.ToSession());
        }

        [Fact]
        public void Given_SmartSessionConfigWithNoValidator_When_ConvertingToSession_Then_Throws()
        {
            var config = new SmartSessionConfig()
                .WithSalt(new byte[32]);

            Assert.Throws<InvalidOperationException>(() => config.ToSession());
        }

        [Fact]
        public void Given_ValidSmartSessionConfig_When_ConvertingToSession_Then_ReturnsSession()
        {
            var validator = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 42;

            var config = new SmartSessionConfig()
                .WithSessionValidator(validator)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            var session = config.ToSession();

            Assert.Equal(validator, session.SessionValidator);
            Assert.Equal(salt, session.Salt);
            Assert.True(session.PermitERC4337Paymaster);
            Assert.NotNull(session.UserOpPolicies);
            Assert.NotNull(session.Actions);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-sessions-and-policies", "Create a SmartSession config with a static salt", Order = 12)]
        public void Given_SmartSessionConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var validatorAddress = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 1;

            var config = SmartSessionConfig.Create(moduleAddress, validatorAddress, salt);

            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(validatorAddress, config.SessionValidator);
            Assert.Equal(salt, config.Salt);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-sessions-and-policies", "Create a SmartSession config with an owner", Order = 13)]
        public void Given_SmartSessionConfig_When_UsingStaticCreateWithOwner_Then_InitDataIsSet()
        {
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var validatorAddress = "0x1234567890123456789012345678901234567890";
            var ownerAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var salt = new byte[32];

            var config = SmartSessionConfig.CreateWithOwner(
                moduleAddress, validatorAddress, ownerAddress, salt);

            Assert.Equal(20, config.SessionValidatorInitData.Length);
        }
    }
}
