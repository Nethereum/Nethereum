using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.IntegrationTests.E2E.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.E2E.Scenarios
{
    [Collection("Sequential")]
    [Trait("Category", "AppChainBuilder-E2E")]
    public class InviteTreeSocialChainTests : InviteTreeFixture, IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        public InviteTreeSocialChainTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_InviteTreeChain_When_RootUserInvites_Then_InvitedCanSubmit()
        {
            var rootUser = OperatorAccount;
            var invitedUser = CreateTestAccounts(1)[0];

            Assert.False(IsActivated(invitedUser.Address),
                "User should not be activated before invitation");

            var inviteSuccess = InviteUser(rootUser.Address, invitedUser.Address);
            Assert.True(inviteSuccess, "Root user should be able to invite");

            Assert.True(IsActivated(invitedUser.Address),
                "User should be activated after invitation");

            var (txHash, txSuccess) = await SendTransactionAsync(
                invitedUser, rootUser.Address, BigInteger.Zero);

            Assert.True(txSuccess, "Invited user should be able to submit transactions");

            await ProduceBlockAsync();

            _output.WriteLine($"Root user {rootUser.Address} invited {invitedUser.Address}");
            _output.WriteLine($"Invited user transaction: {txHash}");
        }

        [Fact]
        public async Task Given_InviteTreeChain_When_InvitedUserInvitesOthers_Then_ChainGrows()
        {
            var rootUser = OperatorAccount;
            var level1User = CreateTestAccounts(1)[0];
            var level2User = CreateTestAccounts(1)[0];

            InviteUser(rootUser.Address, level1User.Address);
            Assert.True(IsActivated(level1User.Address), "Level 1 user should be activated");

            var level1Invited = InviteUser(level1User.Address, level2User.Address);
            Assert.True(level1Invited, "Level 1 user should be able to invite");
            Assert.True(IsActivated(level2User.Address), "Level 2 user should be activated");

            var (txHash, txSuccess) = await SendTransactionAsync(
                level2User, rootUser.Address, BigInteger.Zero);

            Assert.True(txSuccess, "Level 2 user should be able to submit transactions");

            await ProduceBlockAsync();

            _output.WriteLine($"Invite tree: Root -> Level1 ({level1User.Address}) -> Level2 ({level2User.Address})");
            _output.WriteLine($"Level 2 user transaction: {txHash}");
            _output.WriteLine($"Total activated users: {ActivatedUsers.Count}");
        }

        [Fact]
        public void Given_InviteTreeChain_When_MaxInvitesReached_Then_CannotInviteMore()
        {
            var inviter = CreateTestAccounts(1)[0];
            InviteUser(OperatorAccount.Address, inviter.Address);

            var invitees = CreateTestAccounts(4);
            InviteUser(inviter.Address, invitees[0].Address);
            InviteUser(inviter.Address, invitees[1].Address);
            InviteUser(inviter.Address, invitees[2].Address);

            Assert.Equal(0, RemainingInvites(inviter.Address));
            _output.WriteLine($"Inviter has used all 3 invites");

            var fourthInvite = InviteUser(inviter.Address, invitees[3].Address);
            Assert.False(fourthInvite, "Fourth invite should fail - max reached");

            Assert.False(IsActivated(invitees[3].Address),
                "Fourth user should not be activated");

            _output.WriteLine($"Fourth invitation correctly rejected");
        }

        [Fact]
        public async Task Given_InviteTreeChain_When_UninvitedUser_Then_TransactionRejected()
        {
            var uninvitedUser = CreateTestAccounts(1)[0];

            Assert.False(IsActivated(uninvitedUser.Address),
                "User should not be activated without invitation");

            var (_, success, error) = await TrySendTransactionAsync(
                uninvitedUser, OperatorAccount.Address, BigInteger.Zero);

            Assert.False(success, "Uninvited user should not be able to submit transactions");

            _output.WriteLine($"Uninvited user {uninvitedUser.Address} correctly rejected");
            _output.WriteLine($"Error: {error}");
        }

        [Fact]
        public void Given_InviteTreeChain_When_AlreadyActivated_Then_CannotBeInvitedAgain()
        {
            var rootUser = OperatorAccount;
            var user = CreateTestAccounts(1)[0];

            var firstInvite = InviteUser(rootUser.Address, user.Address);
            Assert.True(firstInvite, "First invite should succeed");

            var duplicateInvite = InviteUser(rootUser.Address, user.Address);
            Assert.False(duplicateInvite, "Duplicate invite should fail");

            _output.WriteLine($"Duplicate invitation correctly rejected");
        }

        [Fact]
        public void Given_InviteTreeChain_When_Started_Then_RootUserActivated()
        {
            Assert.True(IsActivated(OperatorAccount.Address),
                "Root user (operator) should be activated on startup");

            _output.WriteLine($"Social chain started: {Chain!.ChainName}");
            _output.WriteLine($"Root user: {OperatorAccount.Address}");
        }

        [Fact]
        public async Task Given_InviteTreeChain_When_DeepInviteChain_Then_AllLevelsCanSubmit()
        {
            var root = OperatorAccount;
            var user1 = CreateTestAccounts(1)[0];

            InviteUser(root.Address, user1.Address);

            var nestedUsers = CreateTestAccounts(3);
            var current = user1;
            foreach (var next in nestedUsers)
            {
                var invited = InviteUser(current.Address, next.Address);
                Assert.True(invited, $"Should be able to invite at each level");
                current = next;
            }

            var (txHash, success) = await SendTransactionAsync(
                current, root.Address, BigInteger.Zero);

            Assert.True(success, "Deeply nested user should be able to submit");

            await ProduceBlockAsync();

            _output.WriteLine($"Deep chain: Root -> User1 -> ... -> User4");
            _output.WriteLine($"Deepest user transaction: {txHash}");
            _output.WriteLine($"Total activated users: {ActivatedUsers.Count}");
        }
    }
}
