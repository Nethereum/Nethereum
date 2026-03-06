using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.E2E.Scenarios
{
    [Collection("Sequential")]
    [Trait("Category", "AppChainBuilder-E2E")]
    public class WhitelistEnterpriseChainTests : IClassFixture<WhitelistTrustFixture>
    {
        private readonly WhitelistTrustFixture _fixture;
        private readonly ITestOutputHelper _output;

        public WhitelistEnterpriseChainTests(WhitelistTrustFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Given_WhitelistChain_When_WhitelistedSubmits_Then_TransactionAccepted()
        {
            var whitelistedUser = _fixture.TestAccounts[0];
            _fixture.AddToWhitelist(whitelistedUser.Address);

            var recipient = _fixture.OperatorAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            var (txHash, success) = await _fixture.SendTransactionAsync(whitelistedUser, recipient.Address, transferAmount);

            Assert.True(success, "Whitelisted user should be able to submit transactions");
            Assert.False(string.IsNullOrEmpty(txHash));

            await _fixture.ProduceBlockAsync();

            _output.WriteLine($"Whitelisted user {whitelistedUser.Address} transaction accepted: {txHash}");
        }

        [Fact]
        public async Task Given_WhitelistChain_When_NonWhitelistedSubmits_Then_TransactionRejected()
        {
            var nonWhitelistedUser = _fixture.TestAccounts[1];

            Assert.False(_fixture.IsWhitelisted(nonWhitelistedUser.Address),
                "User should not be whitelisted for this test");

            var recipient = _fixture.OperatorAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            var (txHash, success, error) = await _fixture.TrySendTransactionAsync(
                nonWhitelistedUser, recipient.Address, transferAmount);

            Assert.False(success, "Non-whitelisted user should not be able to submit transactions");

            _output.WriteLine($"Non-whitelisted user {nonWhitelistedUser.Address} was correctly rejected");
            _output.WriteLine($"Error: {error}");
        }

        [Fact]
        public async Task Given_WhitelistChain_When_AdminAddsNewMember_Then_NewMemberCanSubmit()
        {
            var newMember = _fixture.TestAccounts[2];

            Assert.False(_fixture.IsWhitelisted(newMember.Address),
                "New member should not be whitelisted initially");

            var (_, failedBefore, _) = await _fixture.TrySendTransactionAsync(
                newMember, _fixture.OperatorAccount.Address, BigInteger.Zero);
            Assert.False(failedBefore, "Should fail before being whitelisted");

            _fixture.AddToWhitelist(newMember.Address);

            Assert.True(_fixture.IsWhitelisted(newMember.Address),
                "New member should now be whitelisted");

            var (txHash, successAfter) = await _fixture.SendTransactionAsync(
                newMember, _fixture.OperatorAccount.Address, BigInteger.Zero);

            Assert.True(successAfter, "New member should be able to submit after being whitelisted");

            await _fixture.ProduceBlockAsync();

            _output.WriteLine($"New member {newMember.Address} added to whitelist and can now submit");
            _output.WriteLine($"Transaction hash: {txHash}");
        }

        [Fact]
        public async Task Given_WhitelistChain_When_MemberRemoved_Then_MemberCannotSubmit()
        {
            var member = _fixture.TestAccounts[3];
            _fixture.AddToWhitelist(member.Address);

            var (txHash, successBefore) = await _fixture.SendTransactionAsync(
                member, _fixture.OperatorAccount.Address, BigInteger.Zero);
            Assert.True(successBefore, "Member should be able to submit while whitelisted");

            await _fixture.ProduceBlockAsync();

            _output.WriteLine($"Member successfully submitted while whitelisted: {txHash}");

            _fixture.RemoveFromWhitelist(member.Address);

            Assert.False(_fixture.IsWhitelisted(member.Address),
                "Member should no longer be whitelisted");

            var (_, successAfter, error) = await _fixture.TrySendTransactionAsync(
                member, _fixture.OperatorAccount.Address, BigInteger.Zero);

            Assert.False(successAfter, "Removed member should not be able to submit");

            _output.WriteLine($"Member {member.Address} removed from whitelist");
            _output.WriteLine($"Subsequent transaction correctly rejected: {error}");
        }

        [Fact]
        public async Task Given_WhitelistChain_When_OperatorSubmits_Then_AlwaysAccepted()
        {
            var recipient = _fixture.TestAccounts[4];
            var transferAmount = Web3.Web3.Convert.ToWei(1);

            var (txHash, success) = await _fixture.SendTransactionAsync(
                _fixture.OperatorAccount, recipient.Address, transferAmount);

            Assert.True(success, "Operator should always be able to submit");

            await _fixture.ProduceBlockAsync();

            var balance = await _fixture.GetBalanceAsync(recipient.Address);
            Assert.True(balance >= transferAmount, "Recipient should have received the transfer");

            _output.WriteLine($"Operator transaction accepted: {txHash}");
        }

        [Fact]
        public void Given_WhitelistChain_When_Started_Then_ChainConfigured()
        {
            Assert.NotNull(_fixture.Chain);
            Assert.Equal("CorpChain", _fixture.Chain!.ChainName);
            Assert.True(_fixture.CurrentWhitelist.Count >= 1, "At least operator should be whitelisted");

            _output.WriteLine($"Enterprise chain started: {_fixture.Chain.ChainName}");
            _output.WriteLine($"Current whitelist size: {_fixture.CurrentWhitelist.Count}");
        }
    }
}
