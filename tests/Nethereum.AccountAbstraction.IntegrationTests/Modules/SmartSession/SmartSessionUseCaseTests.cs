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
    [Trait("Workflow", "UseCase")]
    public class SmartSessionUseCaseTests
    {
        private readonly ERC7579TestFixture _fixture;

        public SmartSessionUseCaseTests(ERC7579TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void UseCase1_GaslessTransactions_SessionWithPaymasterPermission()
        {
            // USE CASE: User wants gasless transactions via paymaster
            //
            // The user wants to interact with a dApp without paying gas directly.
            // A paymaster (sponsor) will cover gas costs.
            //
            // Configuration:
            // - Enable paymaster permission
            // - Allow specific dApp interactions

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var dappContract = "0x3333333333333333333333333333333333333333";
            var salt = new byte[32];
            salt[31] = 1;

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithPaymasterPermission(true)
                .WithAction(ActionDataBuilder.UnrestrictedAction(dappContract, "0x12345678", sudoPolicy))
                .ToSession();

            Assert.True(session.PermitERC4337Paymaster);
            Assert.Single(session.Actions);
        }

        [Fact]
        public void UseCase2_DelegatedWallet_MultipleTokenApprovalSession()
        {
            // USE CASE: User delegates wallet management to a third party
            //
            // A portfolio manager needs limited access to manage funds:
            // - Can transfer multiple tokens up to daily limits
            // - Cannot withdraw ETH
            // - Cannot call arbitrary contracts

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var spendingPolicy = "0x2222222222222222222222222222222222222222";
            var salt = new byte[32];
            salt[31] = 2;

            var usdc = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var dai = "0x6B175474E89094C44Da98b954EedeAC495271d0F";
            var weth = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";

            var usdcLimit = BigInteger.Parse("10000000000"); // 10,000 USDC
            var daiLimit = BigInteger.Parse("10000000000000000000000"); // 10,000 DAI
            var wethLimit = BigInteger.Parse("5000000000000000000"); // 5 WETH

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithERC20TransferAction(usdc, spendingPolicy, ERC20SpendingLimitBuilder.SingleToken(usdc, usdcLimit))
                .WithERC20TransferAction(dai, spendingPolicy, ERC20SpendingLimitBuilder.SingleToken(dai, daiLimit))
                .WithERC20TransferAction(weth, spendingPolicy, ERC20SpendingLimitBuilder.SingleToken(weth, wethLimit))
                .ToSession();

            Assert.Equal(3, session.Actions.Count);
            Assert.False(session.PermitERC4337Paymaster);
        }

        [Fact]
        public void UseCase3_SocialLoginSession_WebAppWithLimitedAccess()
        {
            // USE CASE: Web3 app with social login (passkey/WebAuthn)
            //
            // A user logs in via social/passkey to a web app.
            // The session key is stored in browser, with strict limits:
            // - Very low spending limits (micro-transactions)
            // - Only app-specific contract calls
            // - Short-lived session (enforced off-chain)

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var spendingPolicy = "0x2222222222222222222222222222222222222222";
            var sudoPolicy = "0x3333333333333333333333333333333333333333";
            var appContract = "0x4444444444444444444444444444444444444444";
            var usdc = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var salt = new byte[32];
            salt[31] = 3;

            var microLimit = BigInteger.Parse("10000000"); // 10 USDC only

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithPaymasterPermission(true)
                .WithERC20TransferAction(usdc, spendingPolicy, ERC20SpendingLimitBuilder.SingleToken(usdc, microLimit))
                .WithAction(ActionDataBuilder.UnrestrictedAction(appContract, "0x11111111", sudoPolicy))
                .WithAction(ActionDataBuilder.UnrestrictedAction(appContract, "0x22222222", sudoPolicy))
                .ToSession();

            Assert.True(session.PermitERC4337Paymaster);
            Assert.Equal(3, session.Actions.Count);
        }

        [Fact]
        public void UseCase4_AutomatedDCA_RecurringBuySession()
        {
            // USE CASE: Automated Dollar-Cost Averaging (DCA)
            //
            // User wants to automate weekly ETH purchases:
            // - Can call swap() on DEX
            // - Maximum $100 worth per transaction
            // - Only specific DEX router allowed

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var uniActionPolicy = "0x2222222222222222222222222222222222222222";
            var uniswapRouter = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            var swapSelector = "0x38ed1739";
            var salt = new byte[32];
            salt[31] = 4;

            var maxSwapValue = BigInteger.Parse("100000000"); // 100 USDC worth

            var policyData = new UniActionPolicyBuilder()
                .WithValueLimit(maxSwapValue)
                .Build();

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(uniswapRouter)
                    .WithSelector(swapSelector)
                    .WithUniActionPolicy(uniActionPolicy, policyData)
                    .Build())
                .ToSession();

            Assert.Single(session.Actions);
            Assert.Equal(uniswapRouter, session.Actions[0].ActionTarget);
        }

        [Fact]
        public void UseCase5_NFTGallerySession_AutomatedListingManagement()
        {
            // USE CASE: NFT Gallery automated listing
            //
            // An NFT collector automates gallery management:
            // - Can list NFTs for sale (setApprovalForAll)
            // - Can update listing prices
            // - Cannot transfer NFTs directly
            // - All actions limited to specific marketplace

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var nftCollection = "0x3333333333333333333333333333333333333333";
            var marketplace = "0x4444444444444444444444444444444444444444";
            var salt = new byte[32];
            salt[31] = 5;

            var setApprovalSelector = "0xa22cb465";
            var createListingSelector = "0x12345678";
            var updatePriceSelector = "0xabcdef01";

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(ActionDataBuilder.UnrestrictedAction(nftCollection, setApprovalSelector, sudoPolicy))
                .WithAction(ActionDataBuilder.UnrestrictedAction(marketplace, createListingSelector, sudoPolicy))
                .WithAction(ActionDataBuilder.UnrestrictedAction(marketplace, updatePriceSelector, sudoPolicy))
                .ToSession();

            Assert.Equal(3, session.Actions.Count);
        }

        [Fact]
        public void UseCase6_MultiChainSession_SameSessionAcrossChains()
        {
            // USE CASE: Multi-chain session configuration
            //
            // A user wants consistent session across L2s:
            // - Same session key validator
            // - Same salt (deterministic permissionId)
            // - Different contract addresses per chain (handled at runtime)

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var salt = new byte[32];
            salt[0] = 0xCC;
            salt[1] = 0xCC;
            salt[31] = 6;

            var baseConfig = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithPaymasterPermission(true);

            var baseSession = baseConfig.ToSession();

            Assert.Equal(sessionKeyValidator, baseSession.SessionValidator);
            Assert.Equal(salt, baseSession.Salt);
            Assert.True(baseSession.PermitERC4337Paymaster);
        }

        [Fact]
        public void UseCase7_SubscriptionSession_RecurringPayments()
        {
            // USE CASE: Subscription payments
            //
            // User authorizes recurring subscription payments:
            // - Can call chargeSubscription() on billing contract
            // - Limited to specific amount per period
            // - Only billing contract can be called

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var uniActionPolicy = "0x2222222222222222222222222222222222222222";
            var billingContract = "0x3333333333333333333333333333333333333333";
            var chargeSelector = "0x44444444";
            var salt = new byte[32];
            salt[31] = 7;

            var monthlyLimit = BigInteger.Parse("50000000"); // 50 USDC per month

            var policyData = new UniActionPolicyBuilder()
                .WithValueLimit(monthlyLimit)
                .Build();

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(billingContract)
                    .WithSelector(chargeSelector)
                    .WithUniActionPolicy(uniActionPolicy, policyData)
                    .Build())
                .ToSession();

            Assert.Single(session.Actions);
            Assert.Equal(billingContract, session.Actions[0].ActionTarget);
        }

        [Fact]
        public void UseCase8_DAOGovernance_VotingSession()
        {
            // USE CASE: DAO governance voting
            //
            // A DAO member delegates voting to an agent:
            // - Can call vote(proposalId, support) on governance
            // - Cannot call execute() or propose()
            // - Voting power is preserved (snapshot-based)

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var governanceContract = "0x3333333333333333333333333333333333333333";
            var voteSelector = "0x56781234";
            var salt = new byte[32];
            salt[31] = 8;

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(ActionDataBuilder.UnrestrictedAction(governanceContract, voteSelector, sudoPolicy))
                .ToSession();

            Assert.Single(session.Actions);
            Assert.Equal(voteSelector.HexToByteArray(), session.Actions[0].ActionTargetSelector);
        }

        [Fact]
        public void UseCase9_EmergencyRecovery_LimitedAccessSession()
        {
            // USE CASE: Emergency recovery session
            //
            // A backup key with severely limited access:
            // - Can only call emergencyWithdraw()
            // - No token transfers
            // - No contract interactions

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var sudoPolicy = "0x2222222222222222222222222222222222222222";
            var accountContract = "0x3333333333333333333333333333333333333333";
            var emergencySelector = "0x99999999";
            var salt = new byte[32];
            salt[31] = 9;

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(ActionDataBuilder.UnrestrictedAction(accountContract, emergencySelector, sudoPolicy))
                .ToSession();

            Assert.Single(session.Actions);
            Assert.False(session.PermitERC4337Paymaster);
        }

        [Fact]
        public void UseCase10_ComplexDeFi_MultiProtocolSession()
        {
            // USE CASE: Complex DeFi session across protocols
            //
            // Power user wants to automate complex DeFi strategies:
            // - Uniswap: swap tokens
            // - Aave: supply/withdraw collateral
            // - Compound: borrow/repay
            // - All with value limits

            var sessionKeyValidator = "0x1111111111111111111111111111111111111111";
            var uniActionPolicy = "0x2222222222222222222222222222222222222222";
            var salt = new byte[32];
            salt[31] = 10;

            var uniswap = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            var aave = "0x87870Bca3F3fD6335C3F4ce8392D69350B4fA4E2";
            var compound = "0xc3d688B66703497DAA19211EEdff47f25384cdc3";

            var swapLimit = BigInteger.Parse("1000000000000000000"); // 1 ETH
            var supplyLimit = BigInteger.Parse("5000000000000000000"); // 5 ETH
            var borrowLimit = BigInteger.Parse("500000000000000000"); // 0.5 ETH

            var session = new SmartSessionConfig()
                .WithSessionValidator(sessionKeyValidator)
                .WithSalt(salt)
                .WithAction(new ActionDataBuilder()
                    .WithTarget(uniswap)
                    .WithSelector("0x38ed1739")
                    .WithUniActionPolicy(uniActionPolicy, UniActionPolicyBuilder.EmptyPolicy(swapLimit))
                    .Build())
                .WithAction(new ActionDataBuilder()
                    .WithTarget(aave)
                    .WithSelector("0x617ba037")
                    .WithUniActionPolicy(uniActionPolicy, UniActionPolicyBuilder.EmptyPolicy(supplyLimit))
                    .Build())
                .WithAction(new ActionDataBuilder()
                    .WithTarget(compound)
                    .WithSelector("0x1a1be1c0")
                    .WithUniActionPolicy(uniActionPolicy, UniActionPolicyBuilder.EmptyPolicy(borrowLimit))
                    .Build())
                .ToSession();

            Assert.Equal(3, session.Actions.Count);
        }
    }
}
