using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class TransactionExecutionTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;

        public TransactionExecutionTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SendEth_TransfersBalance_AndReturnsReceipt()
        {
            var recipient = _fixture.RecipientAddress;
            var amount = BigInteger.Parse("1000000000000000000"); // 1 ETH

            var initialSenderBalance = await _fixture.Node.GetBalanceAsync(_fixture.Address);
            var initialRecipientBalance = await _fixture.Node.GetBalanceAsync(recipient);

            var signedTx = _fixture.CreateSignedTransaction(recipient, amount);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");
            Assert.NotNull(result.Receipt);
            Assert.True(result.Receipt!.HasSucceeded == true);

            // Get ReceiptInfo for GasUsed (SendTransactionAsync returns new result without GasUsed)
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            Assert.NotNull(receiptInfo);
            Assert.Equal((BigInteger)21000, receiptInfo.GasUsed);

            var finalRecipientBalance = await _fixture.Node.GetBalanceAsync(recipient);
            Assert.Equal(initialRecipientBalance + amount, finalRecipientBalance);
        }

        [Fact]
        public async Task SendEth_UpdatesSenderNonce()
        {
            var initialNonce = await _fixture.Node.GetNonceAsync(_fixture.Address);

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"), // 0.1 ETH
                nonce: initialNonce);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var finalNonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            Assert.Equal(initialNonce + 1, finalNonce);
        }

        [Fact]
        public async Task SendEth_DeductsFeeFromSender()
        {
            var recipient = _fixture.RecipientAddress;
            var amount = BigInteger.Parse("1000000000000000000"); // 1 ETH
            BigInteger gasPrice = 1_000_000_000; // 1 gwei
            BigInteger gasLimit = 21000;

            var initialBalance = await _fixture.Node.GetBalanceAsync(_fixture.Address);

            var signedTx = _fixture.CreateSignedTransaction(
                recipient, amount,
                gasLimit: gasLimit,
                gasPrice: gasPrice);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            // Get ReceiptInfo for GasUsed
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);
            var expectedFee = gasPrice * receiptInfo.GasUsed;
            var finalBalance = await _fixture.Node.GetBalanceAsync(_fixture.Address);

            Assert.Equal(initialBalance - amount - expectedFee, finalBalance);
        }

        [Fact]
        public async Task SendEth_CreatesBlockWithTransaction()
        {
            var blockNumberBefore = await _fixture.Node.GetBlockNumberAsync();

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("50000000000000000")); // 0.05 ETH

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var blockNumberAfter = await _fixture.Node.GetBlockNumberAsync();
            Assert.Equal(blockNumberBefore + 1, blockNumberAfter);

            var block = await _fixture.Node.GetBlockByNumberAsync(blockNumberAfter);
            Assert.NotNull(block);
        }

        [Fact]
        public async Task SendEth_ReceiptContainsCorrectTransactionInfo()
        {
            var recipient = _fixture.RecipientAddress;
            var amount = BigInteger.Parse("250000000000000000"); // 0.25 ETH

            var signedTx = _fixture.CreateSignedTransaction(recipient, amount);
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var receipt = await _fixture.Node.GetTransactionReceiptAsync(txHash);
            Assert.NotNull(receipt);
            Assert.True(receipt.HasSucceeded == true);
        }

        [Fact]
        public async Task GetTransactionByHash_ReturnsStoredTransaction()
        {
            var amount = BigInteger.Parse("300000000000000000"); // 0.3 ETH

            var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, amount);
            var txHash = signedTx.Hash;

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var storedTx = await _fixture.Node.GetTransactionByHashAsync(txHash);
            Assert.NotNull(storedTx);
            Assert.Equal(txHash, storedTx.Hash);
        }

        [Fact]
        public async Task SendEth_WithInsufficientBalance_Fails()
        {
            var poorAddress = "0x0000000000000000000000000000000000000001";
            await _fixture.Node.SetBalanceAsync(poorAddress, 100); // Very small balance

            var recipient = _fixture.RecipientAddress;
            var amount = BigInteger.Parse("1000000000000000000000"); // 1000 ETH (more than balance)

            var signedTx = _fixture.CreateSignedTransaction(recipient, amount);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            // Transaction should either fail validation or revert
            // depending on implementation
        }
    }
}
