using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class SnapshotAndStateTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;

        public SnapshotAndStateTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TakeSnapshot_PreservesState()
        {
            var testAddress = "0x1111111111111111111111111111111111111111";
            var initialBalance = BigInteger.Parse("5000000000000000000");

            await _fixture.Node.SetBalanceAsync(testAddress, initialBalance);
            var snapshot = await _fixture.Node.TakeSnapshotAsync();

            // Modify state
            var newBalance = BigInteger.Parse("9000000000000000000");
            await _fixture.Node.SetBalanceAsync(testAddress, newBalance);

            // Verify modified
            var modifiedBalance = await _fixture.Node.GetBalanceAsync(testAddress);
            Assert.Equal(newBalance, modifiedBalance);

            // Revert
            await _fixture.Node.RevertToSnapshotAsync(snapshot);

            // Verify reverted
            var revertedBalance = await _fixture.Node.GetBalanceAsync(testAddress);
            Assert.Equal(initialBalance, revertedBalance);
        }

        [Fact]
        public async Task Snapshot_RevertsNonceChanges()
        {
            var testAddress = "0x2222222222222222222222222222222222222222";
            BigInteger initialNonce = 5;

            await _fixture.Node.SetNonceAsync(testAddress, initialNonce);
            var snapshot = await _fixture.Node.TakeSnapshotAsync();

            await _fixture.Node.SetNonceAsync(testAddress, 100);
            await _fixture.Node.RevertToSnapshotAsync(snapshot);

            var revertedNonce = await _fixture.Node.GetNonceAsync(testAddress);
            Assert.Equal(initialNonce, revertedNonce);
        }

        [Fact]
        public async Task Snapshot_RevertsCodeChanges()
        {
            var testAddress = "0x3333333333333333333333333333333333333333";
            var initialCode = new byte[] { 0x60, 0x80, 0x60, 0x40 };

            await _fixture.Node.SetCodeAsync(testAddress, initialCode);
            var snapshot = await _fixture.Node.TakeSnapshotAsync();

            var newCode = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 };
            await _fixture.Node.SetCodeAsync(testAddress, newCode);

            await _fixture.Node.RevertToSnapshotAsync(snapshot);

            var revertedCode = await _fixture.Node.GetCodeAsync(testAddress);
            Assert.Equal(initialCode, revertedCode);
        }

        [Fact]
        public async Task Snapshot_RevertsStorageChanges()
        {
            var testAddress = "0x4444444444444444444444444444444444444444";
            var slot = BigInteger.Zero;
            var initialValue = new byte[] { 0x01, 0x02, 0x03 };

            await _fixture.Node.SetStorageAtAsync(testAddress, slot, initialValue);
            var snapshot = await _fixture.Node.TakeSnapshotAsync();

            var newValue = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            await _fixture.Node.SetStorageAtAsync(testAddress, slot, newValue);

            await _fixture.Node.RevertToSnapshotAsync(snapshot);

            var revertedValue = await _fixture.Node.GetStorageAtAsync(testAddress, slot);
            Assert.Equal(initialValue, revertedValue);
        }

        [Fact]
        public async Task Snapshot_RevertsTransactionEffects()
        {
            var recipientBefore = await _fixture.Node.GetBalanceAsync(_fixture.RecipientAddress);

            var snapshot = await _fixture.Node.TakeSnapshotAsync();

            // Send ETH
            var amount = BigInteger.Parse("1000000000000000000"); // 1 ETH
            var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, amount);
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            // Verify transfer occurred
            var recipientAfter = await _fixture.Node.GetBalanceAsync(_fixture.RecipientAddress);
            Assert.Equal(recipientBefore + amount, recipientAfter);

            // Revert
            await _fixture.Node.RevertToSnapshotAsync(snapshot);

            // Verify reverted
            var recipientReverted = await _fixture.Node.GetBalanceAsync(_fixture.RecipientAddress);
            Assert.Equal(recipientBefore, recipientReverted);
        }

        [Fact]
        public async Task SetBalance_SetsCorrectValue()
        {
            var testAddress = "0x5555555555555555555555555555555555555555";
            var balance = BigInteger.Parse("123456789012345678901234567890");

            await _fixture.Node.SetBalanceAsync(testAddress, balance);

            var retrieved = await _fixture.Node.GetBalanceAsync(testAddress);
            Assert.Equal(balance, retrieved);
        }

        [Fact]
        public async Task SetNonce_SetsCorrectValue()
        {
            var testAddress = "0x6666666666666666666666666666666666666666";
            BigInteger nonce = 42;

            await _fixture.Node.SetNonceAsync(testAddress, nonce);

            var retrieved = await _fixture.Node.GetNonceAsync(testAddress);
            Assert.Equal(nonce, retrieved);
        }

        [Fact]
        public async Task SetCode_StoresAndRetrievesCode()
        {
            var testAddress = "0x7777777777777777777777777777777777777777";
            var code = SimpleStorageContract.GetDeploymentBytecode();

            await _fixture.Node.SetCodeAsync(testAddress, code);

            var retrieved = await _fixture.Node.GetCodeAsync(testAddress);
            Assert.Equal(code, retrieved);
        }

        [Fact]
        public async Task SetStorageAt_StoresAndRetrievesValue()
        {
            var testAddress = "0x8888888888888888888888888888888888888888";
            var slot = new BigInteger(10);
            var value = new byte[32];
            value[31] = 0x42; // Store value 66

            await _fixture.Node.SetStorageAtAsync(testAddress, slot, value);

            var retrieved = await _fixture.Node.GetStorageAtAsync(testAddress, slot);
            Assert.Equal(value, retrieved);
        }

        [Fact]
        public async Task MultipleSlots_StoredIndependently()
        {
            var testAddress = "0x9999999999999999999999999999999999999999";

            var slot0Value = new byte[] { 0x11 };
            var slot1Value = new byte[] { 0x22 };
            var slot2Value = new byte[] { 0x33 };

            await _fixture.Node.SetStorageAtAsync(testAddress, 0, slot0Value);
            await _fixture.Node.SetStorageAtAsync(testAddress, 1, slot1Value);
            await _fixture.Node.SetStorageAtAsync(testAddress, 2, slot2Value);

            Assert.Equal(slot0Value, await _fixture.Node.GetStorageAtAsync(testAddress, 0));
            Assert.Equal(slot1Value, await _fixture.Node.GetStorageAtAsync(testAddress, 1));
            Assert.Equal(slot2Value, await _fixture.Node.GetStorageAtAsync(testAddress, 2));
        }
    }
}
