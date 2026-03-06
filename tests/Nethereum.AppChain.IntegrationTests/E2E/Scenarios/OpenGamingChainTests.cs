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
    public class OpenGamingChainTests : IClassFixture<OpenTrustFixture>
    {
        private readonly OpenTrustFixture _fixture;
        private readonly ITestOutputHelper _output;

        public OpenGamingChainTests(OpenTrustFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Given_OpenTrustChain_When_AnyPlayerSubmits_Then_TransactionAccepted()
        {
            var player = _fixture.TestAccounts[0];
            var recipient = _fixture.TestAccounts[1];
            var transferAmount = Web3.Web3.Convert.ToWei(1);

            var (txHash, success) = await _fixture.SendTransactionAsync(player, recipient.Address, transferAmount);

            Assert.True(success, "Transaction should be accepted from any player");
            Assert.False(string.IsNullOrEmpty(txHash), "Transaction hash should be returned");

            await _fixture.ProduceBlockAsync();

            var recipientBalance = await _fixture.GetBalanceAsync(recipient.Address);
            Assert.True(recipientBalance >= transferAmount, "Recipient should have received the transfer");

            _output.WriteLine($"Player {player.Address} sent {transferAmount} wei to {recipient.Address}");
            _output.WriteLine($"Transaction hash: {txHash}");
        }

        [Fact]
        public async Task Given_OpenTrustChain_When_MultiplePlayersSubmit_Then_AllTransactionsAccepted()
        {
            var successCount = 0;
            var transferAmount = Web3.Web3.Convert.ToWei(0.1m);

            for (int i = 0; i < 5; i++)
            {
                var player = _fixture.TestAccounts[i];
                var recipient = _fixture.OperatorAccount;

                var (txHash, success) = await _fixture.SendTransactionAsync(player, recipient.Address, transferAmount);
                if (success)
                {
                    successCount++;
                    _output.WriteLine($"Player {i} transaction accepted: {txHash}");
                }
            }

            await _fixture.ProduceBlockAsync();

            Assert.Equal(5, successCount);
            _output.WriteLine($"All 5 player transactions were accepted");
        }

        [Fact]
        public async Task Given_OpenTrustChain_When_UnknownAddressSubmits_Then_TransactionAccepted()
        {
            var unknownPlayer = _fixture.CreateTestAccounts(1)[0];
            var recipient = _fixture.OperatorAccount;
            var transferAmount = BigInteger.Zero;

            var (txHash, success, error) = await _fixture.TrySendTransactionAsync(
                unknownPlayer, recipient.Address, transferAmount);

            Assert.True(success, $"Unknown address should be able to submit in open trust model. Error: {error}");

            _output.WriteLine($"Unknown player {unknownPlayer.Address} transaction accepted");
            _output.WriteLine($"Transaction hash: {txHash}");
        }

        [Fact]
        public async Task Given_OpenTrustChain_When_BlockProduced_Then_BlockNumberIncreases()
        {
            var initialBlock = await _fixture.GetBlockNumberAsync();

            await _fixture.ProduceBlockAsync();
            var afterFirstBlock = await _fixture.GetBlockNumberAsync();

            await _fixture.ProduceBlockAsync();
            var afterSecondBlock = await _fixture.GetBlockNumberAsync();

            Assert.True(afterFirstBlock > initialBlock, "Block number should increase after first block");
            Assert.True(afterSecondBlock > afterFirstBlock, "Block number should increase after second block");

            _output.WriteLine($"Block progression: {initialBlock} -> {afterFirstBlock} -> {afterSecondBlock}");
        }

        [Fact]
        public void Given_OpenTrustChain_When_ChainStarts_Then_ChainPropertiesCorrect()
        {
            Assert.NotNull(_fixture.Chain);
            Assert.NotNull(_fixture.Chain!.AppChain);
            Assert.NotNull(_fixture.Chain.Sequencer);
            Assert.Equal("OpenGameChain", _fixture.Chain.ChainName);
            Assert.Equal(new BigInteger(420420), _fixture.Chain.ChainId);

            _output.WriteLine($"Chain Name: {_fixture.Chain.ChainName}");
            _output.WriteLine($"Chain ID: {_fixture.Chain.ChainId}");
            _output.WriteLine($"Operator: {_fixture.Chain.OperatorAddress}");
        }
    }
}
