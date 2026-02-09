using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SmartSessions
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class SmartSessionTests
    {
        private readonly ERC7579TestFixture _fixture;
        private SmartSessionService? _sessionService;

        public SmartSessionTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SmartSessionService> GetOrDeploySessionAsync()
        {
            if (_sessionService != null)
                return _sessionService;

            _sessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            return _sessionService;
        }

        [Fact]
        public async Task DeploySmartSession_Succeeds()
        {
            var service = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task IsModuleType_Validator_ReturnsTrue()
        {
            var session = await GetOrDeploySessionAsync();

            var isValidator = await session.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);

            Assert.True(isValidator);
        }

        [Fact]
        public async Task IsModuleType_Executor_ReturnsFalse()
        {
            var session = await GetOrDeploySessionAsync();

            var isExecutor = await session.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);

            Assert.False(isExecutor);
        }

        [Fact]
        public async Task IsInitialized_UnknownAccount_ReturnsFalse()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var isInitialized = await session.IsInitializedQueryAsync(unknownAccount);

            Assert.False(isInitialized);
        }

        [Fact]
        public async Task GetPermissionIDs_UnknownAccount_ReturnsEmptyList()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";

            var permissionIds = await session.GetPermissionIDsQueryAsync(unknownAccount);

            Assert.NotNull(permissionIds);
            Assert.Empty(permissionIds);
        }

        [Fact]
        public async Task GetNonce_UnknownPermission_ReturnsZero()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";
            var permissionId = new byte[32];

            var nonce = await session.GetNonceQueryAsync(permissionId, unknownAccount);

            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task GetUserOpPolicies_UnknownAccount_ReturnsEmptyList()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";
            var permissionId = new byte[32];

            var policies = await session.GetUserOpPoliciesQueryAsync(unknownAccount, permissionId);

            Assert.NotNull(policies);
            Assert.Empty(policies);
        }

        [Fact]
        public async Task GetERC1271Policies_UnknownAccount_ReturnsEmptyList()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";
            var permissionId = new byte[32];

            var policies = await session.GetERC1271PoliciesQueryAsync(unknownAccount, permissionId);

            Assert.NotNull(policies);
            Assert.Empty(policies);
        }

        [Fact]
        public async Task GetEnabledActions_UnknownAccount_ReturnsEmptyList()
        {
            var session = await GetOrDeploySessionAsync();
            var unknownAccount = "0x1234567890123456789012345678901234567890";
            var permissionId = new byte[32];

            var actions = await session.GetEnabledActionsQueryAsync(unknownAccount, permissionId);

            Assert.NotNull(actions);
            Assert.Empty(actions);
        }
    }
}
