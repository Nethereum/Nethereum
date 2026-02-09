using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
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
            // Given: A SmartSession contract (it's a validator module)
            var sessionService = await GetSmartSessionServiceAsync();

            // When: Checking if it's a validator type module
            var isValidator = await sessionService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: It confirms it's a validator
            Assert.True(isValidator);
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingExecutorType_Then_ReturnsFalse()
        {
            // Given: A SmartSession contract
            var sessionService = await GetSmartSessionServiceAsync();

            // When: Checking if it's an executor type module
            var isExecutor = await sessionService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_EXECUTOR);

            // Then: It's not an executor
            Assert.False(isExecutor);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_UsingFluentAPI_Then_SessionIsConfigured()
        {
            // Given: A fluent session configuration builder
            var sessionValidator = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 1;

            // When: Building a session with chained methods
            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionValidator)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            // Then: Session config is properly set
            Assert.Equal(sessionValidator, config.SessionValidator);
            Assert.Equal(salt, config.Salt);
            Assert.True(config.PermitERC4337Paymaster);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingUserOpPolicy_Then_PolicyIsAdded()
        {
            // Given: A session configuration
            var policyAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var initData = new byte[] { 1, 2, 3, 4 };

            // When: Adding a UserOp policy
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithUserOpPolicy(policyAddress, initData);

            // Then: Policy is in the list
            Assert.Single(config.UserOpPolicies);
            Assert.Equal(policyAddress, config.UserOpPolicies[0].Policy);
            Assert.Equal(initData, config.UserOpPolicies[0].InitData);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingMultiplePolicies_Then_AllPoliciesAreAdded()
        {
            // Given: A session configuration
            var policy1 = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var policy2 = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var policy3 = "0xcccccccccccccccccccccccccccccccccccccccc";

            // When: Adding multiple UserOp policies
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithUserOpPolicy(policy1)
                .WithUserOpPolicy(policy2)
                .WithUserOpPolicy(policy3);

            // Then: All policies are added
            Assert.Equal(3, config.UserOpPolicies.Count);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_AddingSudoPolicy_Then_PolicyHasEmptyInitData()
        {
            // Given: A session configuration
            var sudoPolicyAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            // When: Adding a sudo policy
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890")
                .WithSalt(new byte[32])
                .WithSudoPolicy(sudoPolicyAddress);

            // Then: Sudo policy has empty init data
            Assert.Single(config.UserOpPolicies);
            Assert.Equal(sudoPolicyAddress, config.UserOpPolicies[0].Policy);
            Assert.Empty(config.UserOpPolicies[0].InitData);
        }

        [Fact]
        public void Given_SmartSessionConfigWithNoSalt_When_ConvertingToSession_Then_Throws()
        {
            // Given: A session config without salt
            var config = new SmartSessionConfig()
                .WithSessionValidator("0x1234567890123456789012345678901234567890");

            // When/Then: Converting to session throws
            Assert.Throws<InvalidOperationException>(() => config.ToSession());
        }

        [Fact]
        public void Given_SmartSessionConfigWithNoValidator_When_ConvertingToSession_Then_Throws()
        {
            // Given: A session config without validator
            var config = new SmartSessionConfig()
                .WithSalt(new byte[32]);

            // When/Then: Converting to session throws
            Assert.Throws<InvalidOperationException>(() => config.ToSession());
        }

        [Fact]
        public void Given_ValidSmartSessionConfig_When_ConvertingToSession_Then_ReturnsSession()
        {
            // Given: A valid session config
            var validator = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 42;

            var config = new SmartSessionConfig()
                .WithSessionValidator(validator)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            // When: Converting to session
            var session = config.ToSession();

            // Then: Session has correct values
            Assert.Equal(validator, session.SessionValidator);
            Assert.Equal(salt, session.Salt);
            Assert.True(session.PermitERC4337Paymaster);
            Assert.NotNull(session.UserOpPolicies);
            Assert.NotNull(session.Actions);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_UsingStaticCreate_Then_ConfigIsCorrect()
        {
            // Given: Config created via static method
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var validatorAddress = "0x1234567890123456789012345678901234567890";
            var salt = new byte[32];
            salt[31] = 1;

            // When: Using static Create method
            var config = SmartSessionConfig.Create(moduleAddress, validatorAddress, salt);

            // Then: Config is properly set
            Assert.Equal(moduleAddress, config.ModuleAddress);
            Assert.Equal(validatorAddress, config.SessionValidator);
            Assert.Equal(salt, config.Salt);
            Assert.Equal(ERC7579ModuleTypes.TYPE_VALIDATOR, config.ModuleTypeId);
        }

        [Fact]
        public void Given_SmartSessionConfig_When_UsingStaticCreateWithOwner_Then_InitDataIsSet()
        {
            // Given: Config created via static method with owner
            var moduleAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var validatorAddress = "0x1234567890123456789012345678901234567890";
            var ownerAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var salt = new byte[32];

            // When: Using static CreateWithOwner method
            var config = SmartSessionConfig.CreateWithOwner(
                moduleAddress, validatorAddress, ownerAddress, salt);

            // Then: Session validator init data contains owner
            Assert.Equal(20, config.SessionValidatorInitData.Length);
        }
    }
}
