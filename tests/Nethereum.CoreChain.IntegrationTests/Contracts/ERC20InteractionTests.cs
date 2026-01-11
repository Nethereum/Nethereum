using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Contracts
{
    public class ERC20InteractionTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000"); // 18 decimals

        public ERC20InteractionTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DeployERC20_ReturnsContractAddress()
        {
            var contractAddress = await _fixture.DeployERC20Async();

            Assert.NotNull(contractAddress);
            Assert.StartsWith("0x", contractAddress);
            Assert.Equal(42, contractAddress.Length);
        }

        [Fact]
        public async Task DeployERC20_WithMint_SetsInitialBalance()
        {
            var initialSupply = OneToken * 1000; // 1000 tokens
            var contractAddress = await _fixture.DeployERC20Async(initialSupply);

            var balance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);

            Assert.Equal(initialSupply, balance);
        }

        [Fact]
        public async Task BalanceOf_ReturnsZeroForNewAddress()
        {
            var contractAddress = await _fixture.DeployERC20Async();
            var newAddress = "0x1111111111111111111111111111111111111111";

            var balance = await _fixture.GetERC20BalanceAsync(contractAddress, newAddress);

            Assert.Equal(BigInteger.Zero, balance);
        }

        [Fact]
        public async Task TotalSupply_MatchesMintedAmount()
        {
            var initialSupply = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async(initialSupply);

            var totalSupply = await _fixture.GetERC20TotalSupplyAsync(contractAddress);

            Assert.Equal(initialSupply, totalSupply);
        }

        [Fact]
        public async Task Transfer_UpdatesBalances()
        {
            var initialBalance = OneToken * 1000;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            Assert.True(result.Success, $"Transfer failed: {result.RevertReason}");

            var senderBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var recipientBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.RecipientAddress);

            Assert.Equal(initialBalance - transferAmount, senderBalance);
            Assert.Equal(transferAmount, recipientBalance);
        }

        [Fact]
        public async Task Transfer_EmitsTransferEvent()
        {
            var initialBalance = OneToken * 1000;
            var transferAmount = OneToken * 50;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            Assert.True(result.Success);
            Assert.NotNull(result.Logs);
            Assert.True(result.Logs.Count > 0, "Expected Transfer event log");

            var transferLog = result.Logs[0];
            Assert.Equal(contractAddress.ToLowerInvariant(), transferLog.Address.ToLowerInvariant());
            Assert.Equal(3, transferLog.Topics.Count);
        }

        [Fact]
        public async Task Transfer_FailsWithInsufficientBalance()
        {
            var initialBalance = OneToken * 10;
            var transferAmount = OneToken * 100; // More than balance
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Approve_SetsAllowance()
        {
            var approvalAmount = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async();

            var result = await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            Assert.True(result.Success, $"Approve failed: {result.RevertReason}");

            var allowance = await _fixture.GetERC20AllowanceAsync(contractAddress, _fixture.Address, _fixture.RecipientAddress);
            Assert.Equal(approvalAmount, allowance);
        }

        [Fact]
        public async Task Approve_EmitsApprovalEvent()
        {
            var approvalAmount = OneToken * 200;
            var contractAddress = await _fixture.DeployERC20Async();

            var result = await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            Assert.True(result.Success);
            Assert.NotNull(result.Logs);
            Assert.True(result.Logs.Count > 0, "Expected Approval event log");
        }

        [Fact]
        public async Task Approve_CanUpdateExistingAllowance()
        {
            var firstAmount = OneToken * 100;
            var secondAmount = OneToken * 200;
            var contractAddress = await _fixture.DeployERC20Async();

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, firstAmount);
            var firstAllowance = await _fixture.GetERC20AllowanceAsync(contractAddress, _fixture.Address, _fixture.RecipientAddress);
            Assert.Equal(firstAmount, firstAllowance);

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, secondAmount);
            var secondAllowance = await _fixture.GetERC20AllowanceAsync(contractAddress, _fixture.Address, _fixture.RecipientAddress);
            Assert.Equal(secondAmount, secondAllowance);
        }

        [Fact]
        public async Task TransferFrom_UsesAllowance()
        {
            var initialBalance = OneToken * 1000;
            var approvalAmount = OneToken * 300;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            // Approve self (Address approves Address) so msg.sender has allowance
            await _fixture.ApproveERC20Async(contractAddress, _fixture.Address, approvalAmount);

            var result = await _fixture.TransferFromERC20Async(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            Assert.True(result.Success, $"TransferFrom failed: {result.RevertReason}");

            var senderBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var recipientBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.RecipientAddress);

            Assert.Equal(initialBalance - transferAmount, senderBalance);
            Assert.Equal(transferAmount, recipientBalance);
        }

        [Fact]
        public async Task TransferFrom_DecreasesAllowance()
        {
            var initialBalance = OneToken * 1000;
            var approvalAmount = OneToken * 300;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            // Approve self so msg.sender has allowance
            await _fixture.ApproveERC20Async(contractAddress, _fixture.Address, approvalAmount);

            await _fixture.TransferFromERC20Async(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            var remainingAllowance = await _fixture.GetERC20AllowanceAsync(
                contractAddress,
                _fixture.Address,
                _fixture.Address);  // Check allowance for self (the spender)

            Assert.Equal(approvalAmount - transferAmount, remainingAllowance);
        }

        [Fact]
        public async Task TransferFrom_FailsWithInsufficientAllowance()
        {
            var initialBalance = OneToken * 1000;
            var approvalAmount = OneToken * 50;
            var transferAmount = OneToken * 100; // More than allowance
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            // Approve self with limited amount
            await _fixture.ApproveERC20Async(contractAddress, _fixture.Address, approvalAmount);

            var result = await _fixture.TransferFromERC20Async(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Mint_IncreasesTotalSupply()
        {
            var firstMint = OneToken * 100;
            var secondMint = OneToken * 200;
            var contractAddress = await _fixture.DeployERC20Async(firstMint);

            var initialSupply = await _fixture.GetERC20TotalSupplyAsync(contractAddress);
            Assert.Equal(firstMint, initialSupply);

            await _fixture.MintERC20Async(contractAddress, _fixture.Address, secondMint);

            var finalSupply = await _fixture.GetERC20TotalSupplyAsync(contractAddress);
            Assert.Equal(firstMint + secondMint, finalSupply);
        }

        [Fact]
        public async Task Mint_UpdatesRecipientBalance()
        {
            var contractAddress = await _fixture.DeployERC20Async();
            var mintAmount = OneToken * 777;
            var mintRecipient = "0x2222222222222222222222222222222222222222";

            var initialBalance = await _fixture.GetERC20BalanceAsync(contractAddress, mintRecipient);
            Assert.Equal(BigInteger.Zero, initialBalance);

            await _fixture.MintERC20Async(contractAddress, mintRecipient, mintAmount);

            var finalBalance = await _fixture.GetERC20BalanceAsync(contractAddress, mintRecipient);
            Assert.Equal(mintAmount, finalBalance);
        }

        [Fact]
        public async Task MultipleTransfers_CorrectlyTrackBalances()
        {
            var initialBalance = OneToken * 1000;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var recipient1 = "0x1111111111111111111111111111111111111111";
            var recipient2 = "0x2222222222222222222222222222222222222222";

            await _fixture.TransferERC20Async(contractAddress, recipient1, OneToken * 100);
            await _fixture.TransferERC20Async(contractAddress, recipient2, OneToken * 200);
            await _fixture.TransferERC20Async(contractAddress, recipient1, OneToken * 50);

            var senderBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var balance1 = await _fixture.GetERC20BalanceAsync(contractAddress, recipient1);
            var balance2 = await _fixture.GetERC20BalanceAsync(contractAddress, recipient2);

            Assert.Equal(initialBalance - (OneToken * 350), senderBalance);
            Assert.Equal(OneToken * 150, balance1);
            Assert.Equal(OneToken * 200, balance2);
        }

        [Fact]
        public async Task TransferFrom_SpenderCanTransferApprovedAmount()
        {
            var initialBalance = OneToken * 1000;
            var approvalAmount = OneToken * 300;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            await _fixture.ApproveERC20Async(contractAddress, _fixture.Address2, approvalAmount);

            var result = await _fixture.TransferFromERC20AsSpenderAsync(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            Assert.True(result.Success, $"TransferFrom failed: {result.RevertReason}");

            var senderBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var recipientBalance = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.RecipientAddress);

            Assert.Equal(initialBalance - transferAmount, senderBalance);
            Assert.Equal(transferAmount, recipientBalance);
        }

        [Fact]
        public async Task TransferFrom_SpenderAllowanceDecreases()
        {
            var initialBalance = OneToken * 1000;
            var approvalAmount = OneToken * 300;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            await _fixture.ApproveERC20Async(contractAddress, _fixture.Address2, approvalAmount);

            await _fixture.TransferFromERC20AsSpenderAsync(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            var remainingAllowance = await _fixture.GetERC20AllowanceAsync(
                contractAddress,
                _fixture.Address,
                _fixture.Address2);

            Assert.Equal(approvalAmount - transferAmount, remainingAllowance);
        }

        [Fact]
        public async Task TransferFrom_FailsWhenSpenderNotApproved()
        {
            var initialBalance = OneToken * 1000;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var result = await _fixture.TransferFromERC20AsSpenderAsync(
                contractAddress,
                _fixture.Address,
                _fixture.RecipientAddress,
                transferAmount);

            Assert.False(result.Success);
        }
    }
}
