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
    [Trait("Workflow", "ERC20-SpendingLimit")]
    public class SmartSessionERC20WorkflowTests
    {
        private readonly ERC7579TestFixture _fixture;

        public SmartSessionERC20WorkflowTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Scenario_DailyERC20Allowance_SessionKeyCanSpendUpToLimit()
        {
            // SCENARIO: A dApp wants to allow a session key to spend up to 100 USDC per day
            //
            // GIVEN: An account owner sets up a session with:
            //   - Session key controlled by a burner wallet
            //   - ERC20 spending limit of 100 USDC on transfer function
            //
            // WHEN: The session is configured
            //
            // THEN: The session key can execute transfers up to the spending limit

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var spendingLimitPolicy = "0x2222222222222222222222222222222222222222";
            var usdcToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var dailyLimit = BigInteger.Parse("100000000"); // 100 USDC (6 decimals)
            var salt = new byte[32];
            salt[31] = 1;

            var spendingLimitInitData = ERC20SpendingLimitBuilder.SingleToken(usdcToken, dailyLimit);

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithERC20TransferAction(usdcToken, spendingLimitPolicy, spendingLimitInitData)
                .WithPaymasterPermission(true);

            var session = config.ToSession();

            Assert.Single(session.Actions);
            Assert.Equal(usdcToken, session.Actions[0].ActionTarget);
            Assert.Equal("0xa9059cbb".HexToByteArray(), session.Actions[0].ActionTargetSelector);
            Assert.Single(session.Actions[0].ActionPolicies);
            Assert.Equal(spendingLimitPolicy, session.Actions[0].ActionPolicies[0].Policy);
        }

        [Fact]
        public void Scenario_MultiTokenSpendingLimits_SessionKeyCanSpendDifferentTokens()
        {
            // SCENARIO: A gaming dApp allows session key to spend multiple tokens
            //
            // GIVEN: An account sets up a session with spending limits for:
            //   - 100 USDC for in-game purchases
            //   - 500 GAME tokens for marketplace
            //
            // WHEN: The session is configured with multiple token limits
            //
            // THEN: The session has separate actions for each token

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var spendingLimitPolicy = "0x2222222222222222222222222222222222222222";
            var usdcToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var gameToken = "0x3333333333333333333333333333333333333333";
            var salt = new byte[32];

            var usdcLimit = BigInteger.Parse("100000000"); // 100 USDC
            var gameLimit = BigInteger.Parse("500000000000000000000"); // 500 GAME (18 decimals)

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithERC20TransferAction(
                    usdcToken,
                    spendingLimitPolicy,
                    ERC20SpendingLimitBuilder.SingleToken(usdcToken, usdcLimit))
                .WithERC20TransferAction(
                    gameToken,
                    spendingLimitPolicy,
                    ERC20SpendingLimitBuilder.SingleToken(gameToken, gameLimit));

            var session = config.ToSession();

            Assert.Equal(2, session.Actions.Count);
            Assert.Equal(usdcToken, session.Actions[0].ActionTarget);
            Assert.Equal(gameToken, session.Actions[1].ActionTarget);
        }

        [Fact]
        public void Scenario_TransferAndApprovePermissions_SessionKeyCanDoMultipleERC20Operations()
        {
            // SCENARIO: A DeFi dApp needs a session that can both transfer and approve tokens
            //
            // GIVEN: An account sets up a session for DeFi operations:
            //   - Can transfer USDC (limited to 1000)
            //   - Can approve USDC for a DEX (limited to 1000)
            //
            // WHEN: The session is configured
            //
            // THEN: Both transfer and approve actions are configured

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var spendingLimitPolicy = "0x2222222222222222222222222222222222222222";
            var usdcToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var limit = BigInteger.Parse("1000000000"); // 1000 USDC
            var salt = new byte[32];
            salt[0] = 0xDE;
            salt[1] = 0xF1;

            var transferAction = ActionDataBuilder.ERC20Transfer(usdcToken, spendingLimitPolicy, limit);
            var approveAction = ActionDataBuilder.ERC20Approve(usdcToken, spendingLimitPolicy, limit);

            var config = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(transferAction)
                .WithAction(approveAction);

            var session = config.ToSession();

            Assert.Equal(2, session.Actions.Count);
            Assert.Equal("0xa9059cbb".HexToByteArray(), session.Actions[0].ActionTargetSelector);
            Assert.Equal("0x095ea7b3".HexToByteArray(), session.Actions[1].ActionTargetSelector);
        }

        [Fact]
        public void Given_ERC20SpendingLimitBuilder_When_AddingMultipleTokens_Then_AllTokensAreIncluded()
        {
            // Given: A spending limit builder
            var usdc = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var usdt = "0xdAC17F958D2ee523a2206206994597C13D831ec7";
            var dai = "0x6B175474E89094C44Da98b954EedeAC495271d0F";

            // When: Adding multiple token limits
            var builder = new ERC20SpendingLimitBuilder()
                .AddTokenLimit(usdc, BigInteger.Parse("100000000"))
                .AddTokenLimit(usdt, BigInteger.Parse("200000000"))
                .AddTokenLimit(dai, BigInteger.Parse("300000000000000000000"));

            // Then: Build succeeds with encoded data
            var initData = builder.Build();
            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }

        [Fact]
        public void Given_ERC20SpendingLimitBuilder_When_NoTokensAdded_Then_BuildThrows()
        {
            // Given: An empty spending limit builder
            var builder = new ERC20SpendingLimitBuilder();

            // When/Then: Building throws
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Given_ERC20SpendingLimitBuilder_When_ZeroLimit_Then_Throws()
        {
            // Given: A spending limit builder
            var token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

            // When/Then: Adding zero limit throws
            Assert.Throws<ArgumentException>(() =>
                new ERC20SpendingLimitBuilder().AddTokenLimit(token, BigInteger.Zero));
        }

        [Fact]
        public void Given_ERC20SpendingLimitBuilder_When_UsingSingleToken_Then_EncodeSucceeds()
        {
            // Given: A single token with limit
            var token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var limit = BigInteger.Parse("1000000000");

            // When: Using static helper
            var initData = ERC20SpendingLimitBuilder.SingleToken(token, limit);

            // Then: Init data is encoded
            Assert.NotNull(initData);
            Assert.True(initData.Length > 0);
        }
    }
}
