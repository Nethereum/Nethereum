using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SmartSession
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "SmartSession")]
    [Trait("Integration", "DevChain")]
    public class SmartSessionIntegrationTests
    {
        private readonly ERC7579TestFixture _fixture;

        public SmartSessionIntegrationTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Given_SmartSession_When_Deployed_Then_CanQueryModuleType()
        {
            // Given: Deploy SmartSession module
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            // When: Query module type
            var isValidator = await smartSessionService.IsModuleTypeQueryAsync(
                ERC7579ModuleTypes.TYPE_VALIDATOR);

            // Then: SmartSession is a validator module
            Assert.True(isValidator);
            Assert.NotEmpty(smartSessionService.ContractAddress);
        }

        [Fact]
        public async Task Given_SudoPolicy_When_Deployed_Then_CanQueryInterface()
        {
            // Given: Deploy SudoPolicy
            var sudoPolicyService = await SudoPolicyService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SudoPolicyDeployment());

            // When: Query supports interface (IActionPolicy = 0x05c00895)
            var supportsActionPolicy = await sudoPolicyService.SupportsInterfaceQueryAsync(
                "0x05c00895".HexToByteArray());

            // Then: SudoPolicy supports IActionPolicy interface
            Assert.True(supportsActionPolicy);
        }

        [Fact]
        public async Task Given_ERC20SpendingLimitPolicy_When_Deployed_Then_CanQueryInterface()
        {
            // Given: Deploy ERC20SpendingLimitPolicy
            var spendingLimitService = await ERC20SpendingLimitPolicyService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new ERC20SpendingLimitPolicyDeployment());

            // When: Query supports interface
            var supportsActionPolicy = await spendingLimitService.SupportsInterfaceQueryAsync(
                "0x05c00895".HexToByteArray());

            // Then: Policy supports IActionPolicy interface
            Assert.True(supportsActionPolicy);
        }

        [Fact]
        public async Task Given_SmartSession_When_QueryingPermissionId_Then_ReturnsHash()
        {
            // Given: Deploy SmartSession and create a session config
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var sessionKeyValidator = _fixture.ECDSAValidatorService.ContractAddress;
            var salt = new byte[32];
            salt[31] = 42;

            var session = new Session
            {
                SessionValidator = sessionKeyValidator,
                SessionValidatorInitData = _fixture.OwnerAddress.HexToByteArray(),
                Salt = salt,
                UserOpPolicies = new List<PolicyData>(),
                Erc7739Policies = new ERC7739Data
                {
                    AllowedERC7739Content = new List<ERC7739Context>(),
                    Erc1271Policies = new List<PolicyData>()
                },
                Actions = new List<ActionData>(),
                PermitERC4337Paymaster = false
            };

            // When: Query permission ID
            var permissionId = await smartSessionService.GetPermissionIdQueryAsync(session);

            // Then: Permission ID is 32 bytes
            Assert.NotNull(permissionId);
            Assert.Equal(32, permissionId.Length);
        }

        [Fact]
        public async Task Given_SmartSession_When_QueryingNonce_Then_ReturnsZeroForNewSession()
        {
            // Given: Deploy SmartSession and an account
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var permissionId = new byte[32];
            permissionId[31] = 1;

            // When: Query nonce for new permission
            var nonce = await smartSessionService.GetNonceQueryAsync(permissionId, account.ContractAddress);

            // Then: Nonce is 0 for new sessions
            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task Given_SmartSession_When_QueryingPermissionIDs_Then_ReturnsEmptyForNewAccount()
        {
            // Given: Deploy SmartSession and create new account
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Query permission IDs
            var permissionIds = await smartSessionService.GetPermissionIDsQueryAsync(account.ContractAddress);

            // Then: No permissions enabled yet
            Assert.NotNull(permissionIds);
            Assert.Empty(permissionIds);
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingIsInitialized_Then_ReturnsFalseForNewAccount()
        {
            // Given: Deploy SmartSession and create new account
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            // When: Check if initialized
            var isInitialized = await smartSessionService.IsInitializedQueryAsync(account.ContractAddress);

            // Then: Not initialized (SmartSession not installed as module yet)
            Assert.False(isInitialized);
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingPermissionEnabled_Then_ReturnsFalseForNewPermission()
        {
            // Given: Deploy SmartSession and create account
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var permissionId = new byte[32];
            permissionId[31] = 123;

            // When: Check if permission is enabled
            var isEnabled = await smartSessionService.IsPermissionEnabledQueryAsync(
                permissionId, account.ContractAddress);

            // Then: Permission not enabled
            Assert.False(isEnabled);
        }

        [Fact]
        public async Task Given_SmartSession_When_CheckingSessionValidator_Then_ReturnsFalseForUnsetSession()
        {
            // Given: Deploy SmartSession and create account
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var salt = _fixture.CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var account = await _fixture.CreateAccountAsync(salt);

            var permissionId = new byte[32];

            // When: Check if session validator is set
            var isSet = await smartSessionService.IsISessionValidatorSetQueryAsync(
                permissionId, account.ContractAddress);

            // Then: Session validator not set
            Assert.False(isSet);
        }

        [Fact]
        public async Task Given_SessionKey_When_CreatingConfig_Then_CanGeneratePermissionId()
        {
            // Given: A session key (new EOA for testing)
            var sessionKey = EthECKey.GenerateKey();
            var sessionKeyAddress = sessionKey.GetPublicAddress();

            // Deploy SmartSession
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            // Create session config using our fluent API
            var salt = new byte[32];
            salt[31] = 99;

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_fixture.ECDSAValidatorService.ContractAddress)
                .WithSessionValidatorInitData(sessionKeyAddress)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            var session = sessionConfig.ToSession();

            // When: Get permission ID
            var permissionId = await smartSessionService.GetPermissionIdQueryAsync(session);

            // Then: Permission ID is deterministic
            Assert.Equal(32, permissionId.Length);

            // Same session should produce same permission ID
            var permissionId2 = await smartSessionService.GetPermissionIdQueryAsync(session);
            Assert.Equal(permissionId.ToHex(), permissionId2.ToHex());
        }

        [Fact]
        public async Task Given_DifferentSalts_When_CreatingSessions_Then_ProduceDifferentPermissionIds()
        {
            // Given: Deploy SmartSession
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var validatorAddress = _fixture.ECDSAValidatorService.ContractAddress;
            var ownerData = _fixture.OwnerAddress.HexToByteArray();

            // Create two sessions with different salts
            var salt1 = new byte[32];
            salt1[31] = 1;

            var salt2 = new byte[32];
            salt2[31] = 2;

            var session1 = new Session
            {
                SessionValidator = validatorAddress,
                SessionValidatorInitData = ownerData,
                Salt = salt1,
                UserOpPolicies = new List<PolicyData>(),
                Erc7739Policies = new ERC7739Data
                {
                    AllowedERC7739Content = new List<ERC7739Context>(),
                    Erc1271Policies = new List<PolicyData>()
                },
                Actions = new List<ActionData>(),
                PermitERC4337Paymaster = false
            };

            var session2 = new Session
            {
                SessionValidator = validatorAddress,
                SessionValidatorInitData = ownerData,
                Salt = salt2,
                UserOpPolicies = new List<PolicyData>(),
                Erc7739Policies = new ERC7739Data
                {
                    AllowedERC7739Content = new List<ERC7739Context>(),
                    Erc1271Policies = new List<PolicyData>()
                },
                Actions = new List<ActionData>(),
                PermitERC4337Paymaster = false
            };

            // When: Get permission IDs
            var permissionId1 = await smartSessionService.GetPermissionIdQueryAsync(session1);
            var permissionId2 = await smartSessionService.GetPermissionIdQueryAsync(session2);

            // Then: Different salts produce different permission IDs
            Assert.NotEqual(permissionId1.ToHex(), permissionId2.ToHex());
        }

        [Fact]
        public async Task Given_SessionWithActions_When_QueryingPermissionId_Then_OnlyCoreMembersAffectId()
        {
            // Given: Deploy SmartSession and policies
            var smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SmartSessionDeployment());

            var sudoPolicyService = await SudoPolicyService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SudoPolicyDeployment());

            var validatorAddress = _fixture.ECDSAValidatorService.ContractAddress;
            var salt = new byte[32];
            salt[31] = 55;

            // Session without actions
            var sessionNoActions = new Session
            {
                SessionValidator = validatorAddress,
                SessionValidatorInitData = _fixture.OwnerAddress.HexToByteArray(),
                Salt = salt,
                UserOpPolicies = new List<PolicyData>(),
                Erc7739Policies = new ERC7739Data
                {
                    AllowedERC7739Content = new List<ERC7739Context>(),
                    Erc1271Policies = new List<PolicyData>()
                },
                Actions = new List<ActionData>(),
                PermitERC4337Paymaster = false
            };

            // Session with an action (same core params: validator, initData, salt)
            var sessionWithAction = new Session
            {
                SessionValidator = validatorAddress,
                SessionValidatorInitData = _fixture.OwnerAddress.HexToByteArray(),
                Salt = salt,
                UserOpPolicies = new List<PolicyData>(),
                Erc7739Policies = new ERC7739Data
                {
                    AllowedERC7739Content = new List<ERC7739Context>(),
                    Erc1271Policies = new List<PolicyData>()
                },
                Actions = new List<ActionData>
                {
                    new ActionData
                    {
                        ActionTarget = "0x1234567890123456789012345678901234567890",
                        ActionTargetSelector = "0xa9059cbb".HexToByteArray(),
                        ActionPolicies = new List<PolicyData>
                        {
                            new PolicyData
                            {
                                Policy = sudoPolicyService.ContractAddress,
                                InitData = Array.Empty<byte>()
                            }
                        }
                    }
                },
                PermitERC4337Paymaster = false
            };

            // When: Get permission IDs
            var idNoActions = await smartSessionService.GetPermissionIdQueryAsync(sessionNoActions);
            var idWithAction = await smartSessionService.GetPermissionIdQueryAsync(sessionWithAction);

            // Then: Permission ID is based only on (validator, initData, salt)
            // Actions are stored separately and don't affect the ID
            Assert.Equal(idNoActions.ToHex(), idWithAction.ToHex());
        }

        [Fact]
        public async Task Given_SudoPolicy_When_CheckingAction_Then_ReturnsZeroForAllActions()
        {
            // Given: Deploy SudoPolicy
            var sudoPolicyService = await SudoPolicyService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new SudoPolicyDeployment());

            var configId = new byte[32];
            var account = _fixture.OwnerAddress;
            var target = "0x1234567890123456789012345678901234567890";
            var value = BigInteger.One;
            var calldata = "0xa9059cbb".HexToByteArray();

            // When: Check action (sudo policy should always return 0 = allowed)
            var result = await sudoPolicyService.CheckActionQueryAsync(
                configId, account, target, value, calldata);

            // Then: Returns 0 (SIG_VALIDATION_SUCCESS)
            Assert.Equal(BigInteger.Zero, result);
        }
    }
}
