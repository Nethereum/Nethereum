using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.IntegrationTests.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.SmartSession
{
    [Collection(ERC7579TestFixture.ERC7579_COLLECTION)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "SmartSession")]
    [Trait("Workflow", "Actions")]
    public class SmartSessionActionWorkflowTests
    {
        private readonly ERC7579TestFixture _fixture;

        public SmartSessionActionWorkflowTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Scenario_GamingSession_SessionKeyCanCallGameFunctions()
        {
            // SCENARIO: A gaming dApp allows a session key to call specific game functions
            //
            // GIVEN: A game account wants to allow automated actions:
            //   - Can call movePlayer(x, y) on game contract
            //   - Can call collectItem(itemId) on game contract
            //   - Cannot call other functions (like transferOwnership)
            //
            // WHEN: The session is configured with specific function selectors
            //
            // THEN: Only allowed functions are in the session actions

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var gameContract = "0x3333333333333333333333333333333333333333";
            var salt = new byte[32];
            salt[31] = 0x42;

            var movePlayerSelector = "0x12345678";
            var collectItemSelector = "0xabcdef01";

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(ActionDataBuilder.UnrestrictedAction(gameContract, movePlayerSelector, sudoPolicy))
                .WithAction(ActionDataBuilder.UnrestrictedAction(gameContract, collectItemSelector, sudoPolicy));

            var session = config.ToSession();

            Assert.Equal(2, session.Actions.Count);
            Assert.All(session.Actions, a => Assert.Equal(gameContract, a.ActionTarget));
            Assert.Equal(movePlayerSelector.HexToByteArray(), session.Actions[0].ActionTargetSelector);
            Assert.Equal(collectItemSelector.HexToByteArray(), session.Actions[1].ActionTargetSelector);
        }

        [Fact]
        public void Scenario_DeFiSwapSession_SessionKeyCanSwapWithValueLimit()
        {
            // SCENARIO: A DeFi dApp allows a session key to perform swaps with value limits
            //
            // GIVEN: An account wants to automate trading:
            //   - Can call swap() on DEX router
            //   - Value per transaction limited to 0.1 ETH
            //
            // WHEN: The session is configured with value limits
            //
            // THEN: The UniAction policy enforces value limits

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var uniActionPolicy = "0x2222222222222222222222222222222222222222";
            var dexRouter = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            var swapSelector = "0x38ed1739";
            var salt = new byte[32];

            var maxValuePerSwap = BigInteger.Parse("100000000000000000"); // 0.1 ETH in wei
            var policyInitData = UniActionPolicyBuilder.EmptyPolicy(maxValuePerSwap);

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(dexRouter)
                    .WithSelector(swapSelector)
                    .WithUniActionPolicy(uniActionPolicy, policyInitData)
                    .Build());

            var session = config.ToSession();

            Assert.Single(session.Actions);
            Assert.Equal(dexRouter, session.Actions[0].ActionTarget);
            Assert.Equal(swapSelector.HexToByteArray(), session.Actions[0].ActionTargetSelector);
        }

        [Fact]
        public void Scenario_NFTMintingSession_SessionKeyCanMintWithParameterChecks()
        {
            // SCENARIO: An NFT platform allows automated minting with parameter validation
            //
            // GIVEN: An artist wants to allow batch minting:
            //   - Can call mint(address to, uint256 tokenId)
            //   - The 'to' parameter must equal the session owner
            //
            // WHEN: The session is configured with parameter rules
            //
            // THEN: The UniAction policy validates parameters

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var uniActionPolicy = "0x2222222222222222222222222222222222222222";
            var nftContract = "0x4444444444444444444444444444444444444444";
            var ownerAddress = "0x5555555555555555555555555555555555555555";
            var mintSelector = "0x40c10f19";
            var salt = new byte[32];

            var policyInitData = new UniActionPolicyBuilder()
                .WithEqualityCheck(0, ownerAddress.HexToByteArray())
                .Build();

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(nftContract)
                    .WithSelector(mintSelector)
                    .WithUniActionPolicy(uniActionPolicy, policyInitData)
                    .Build());

            var session = config.ToSession();

            Assert.Single(session.Actions);
            Assert.NotEmpty(session.Actions[0].ActionPolicies[0].InitData);
        }

        [Fact]
        public void Given_ActionDataBuilder_When_UsingFluentAPI_Then_ActionIsBuilt()
        {
            // Given: An action data builder
            var target = "0x1234567890123456789012345678901234567890";
            var selector = "0xabcdef01";
            var policyAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            // When: Building with fluent API
            var action = new ActionDataBuilder()
                .WithTarget(target)
                .WithSelector(selector)
                .WithPolicy(policyAddress, new byte[] { 1, 2, 3 })
                .Build();

            // Then: Action is correctly built
            Assert.Equal(target, action.ActionTarget);
            Assert.Equal(selector.HexToByteArray(), action.ActionTargetSelector);
            Assert.Single(action.ActionPolicies);
        }

        [Fact]
        public void Given_ActionDataBuilder_When_NoTarget_Then_BuildThrows()
        {
            // Given: A builder without target
            var builder = new ActionDataBuilder()
                .WithSelector("0xabcdef01")
                .WithPolicy("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            // When/Then: Building throws
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Given_ActionDataBuilder_When_NoSelector_Then_BuildThrows()
        {
            // Given: A builder without selector
            var builder = new ActionDataBuilder()
                .WithTarget("0x1234567890123456789012345678901234567890")
                .WithPolicy("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            // When/Then: Building throws
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Given_ActionDataBuilder_When_NoPolicies_Then_BuildThrows()
        {
            // Given: A builder without policies
            var builder = new ActionDataBuilder()
                .WithTarget("0x1234567890123456789012345678901234567890")
                .WithSelector("0xabcdef01");

            // When/Then: Building throws
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Given_ActionDataBuilder_When_InvalidSelectorLength_Then_Throws()
        {
            // Given: A selector with wrong length
            var invalidSelector = new byte[] { 1, 2, 3 };

            // When/Then: Setting selector throws
            Assert.Throws<ArgumentException>(() =>
                new ActionDataBuilder().WithSelector(invalidSelector));
        }

        [Fact]
        public void Given_UniActionPolicyBuilder_When_AddingMultipleRules_Then_RulesAreAdded()
        {
            // Given: A UniAction policy builder

            // When: Adding multiple parameter rules
            var builder = new UniActionPolicyBuilder()
                .WithValueLimit(BigInteger.Parse("1000000000000000000"))
                .WithEqualityCheck(0, new byte[32])
                .WithMaxValue(32, new byte[32])
                .WithMinValue(64, new byte[32]);

            // Then: Build succeeds
            var initData = builder.Build();
            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }

        [Fact]
        public void Given_UniActionPolicyBuilder_When_AddingOver16Rules_Then_Throws()
        {
            // Given: A builder approaching the limit
            var builder = new UniActionPolicyBuilder();
            for (int i = 0; i < 16; i++)
            {
                builder.WithParamRule(ParamCondition.Equal, (ulong)i, new byte[32]);
            }

            // When/Then: Adding 17th rule throws
            Assert.Throws<InvalidOperationException>(() =>
                builder.WithParamRule(ParamCondition.Equal, 16, new byte[32]));
        }

        [Fact]
        public void Given_UniActionPolicyBuilder_When_UsingLimitedUsage_Then_LimitIsConfigured()
        {
            // Given: A builder with limited usage
            var refValue = new byte[32];
            refValue[31] = 0x42;
            var usageLimit = new BigInteger(100);

            // When: Adding limited usage rule
            var builder = new UniActionPolicyBuilder()
                .WithLimitedUsage(0, refValue, usageLimit);

            // Then: Build succeeds
            var initData = builder.Build();
            Assert.NotNull(initData);
        }
    }
}
