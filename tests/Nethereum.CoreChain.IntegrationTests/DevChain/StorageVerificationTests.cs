using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class StorageVerificationTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        private const int BALANCES_SLOT = 0;
        private const int ALLOWANCES_SLOT = 1;
        private const int TOTAL_SUPPLY_SLOT = 2;

        public StorageVerificationTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetStorageAt_ReadsTotalSupplySlot()
        {
            var initialSupply = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async(initialSupply);

            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, TOTAL_SUPPLY_SLOT);

            Assert.NotNull(storageValue);
            var storedSupply = storageValue.ToBigIntegerFromRLPDecoded();
            Assert.Equal(initialSupply, storedSupply);
        }

        [Fact]
        public async Task GetStorageAt_ReadsBalanceMappingSlot()
        {
            var initialBalance = OneToken * 1000;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var slot = CalculateMappingSlot(_fixture.Address, BALANCES_SLOT);
            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);

            Assert.NotNull(storageValue);
            var storedBalance = storageValue.ToBigIntegerFromRLPDecoded();
            Assert.Equal(initialBalance, storedBalance);
        }

        [Fact]
        public async Task GetStorageAt_ReadsAllowanceMappingSlot()
        {
            var approvalAmount = OneToken * 300;
            var contractAddress = await _fixture.DeployERC20Async();

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            var slot = CalculateNestedMappingSlot(_fixture.Address, _fixture.RecipientAddress, ALLOWANCES_SLOT);
            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);

            Assert.NotNull(storageValue);
            var storedAllowance = storageValue.ToBigIntegerFromRLPDecoded();
            Assert.Equal(approvalAmount, storedAllowance);
        }

        [Fact]
        public async Task MappingSlot_CalculatesCorrectly()
        {
            var initialBalance = OneToken * 777;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var slot = CalculateMappingSlot(_fixture.Address, BALANCES_SLOT);
            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);

            var balanceFromCall = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var balanceFromStorage = storageValue.ToBigIntegerFromRLPDecoded();

            Assert.Equal(balanceFromCall, balanceFromStorage);
        }

        [Fact]
        public async Task NestedMappingSlot_CalculatesCorrectly()
        {
            var approvalAmount = OneToken * 999;
            var contractAddress = await _fixture.DeployERC20Async();

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            var slot = CalculateNestedMappingSlot(_fixture.Address, _fixture.RecipientAddress, ALLOWANCES_SLOT);
            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);

            var allowanceFromCall = await _fixture.GetERC20AllowanceAsync(
                contractAddress, _fixture.Address, _fixture.RecipientAddress);
            var allowanceFromStorage = storageValue.ToBigIntegerFromRLPDecoded();

            Assert.Equal(allowanceFromCall, allowanceFromStorage);
        }

        [Fact]
        public async Task Transfer_UpdatesStorageSlots()
        {
            var initialBalance = OneToken * 1000;
            var transferAmount = OneToken * 100;
            var contractAddress = await _fixture.DeployERC20Async(initialBalance);

            var senderSlot = CalculateMappingSlot(_fixture.Address, BALANCES_SLOT);
            var recipientSlot = CalculateMappingSlot(_fixture.RecipientAddress, BALANCES_SLOT);

            var senderStorageBefore = await _fixture.Node.GetStorageAtAsync(contractAddress, senderSlot);
            var recipientStorageBefore = await _fixture.Node.GetStorageAtAsync(contractAddress, recipientSlot);

            var senderBalanceBefore = senderStorageBefore?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;
            var recipientBalanceBefore = recipientStorageBefore?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;

            Assert.Equal(initialBalance, senderBalanceBefore);
            Assert.Equal(BigInteger.Zero, recipientBalanceBefore);

            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            var senderStorageAfter = await _fixture.Node.GetStorageAtAsync(contractAddress, senderSlot);
            var recipientStorageAfter = await _fixture.Node.GetStorageAtAsync(contractAddress, recipientSlot);

            var senderBalanceAfter = senderStorageAfter.ToBigIntegerFromRLPDecoded();
            var recipientBalanceAfter = recipientStorageAfter.ToBigIntegerFromRLPDecoded();

            Assert.Equal(initialBalance - transferAmount, senderBalanceAfter);
            Assert.Equal(transferAmount, recipientBalanceAfter);
        }

        [Fact]
        public async Task Approve_UpdatesAllowanceSlot()
        {
            var firstApproval = OneToken * 100;
            var secondApproval = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async();

            var slot = CalculateNestedMappingSlot(_fixture.Address, _fixture.RecipientAddress, ALLOWANCES_SLOT);

            var storageBefore = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);
            var allowanceBefore = storageBefore?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;
            Assert.Equal(BigInteger.Zero, allowanceBefore);

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, firstApproval);

            var storageAfterFirst = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);
            var allowanceAfterFirst = storageAfterFirst.ToBigIntegerFromRLPDecoded();
            Assert.Equal(firstApproval, allowanceAfterFirst);

            await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, secondApproval);

            var storageAfterSecond = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);
            var allowanceAfterSecond = storageAfterSecond.ToBigIntegerFromRLPDecoded();
            Assert.Equal(secondApproval, allowanceAfterSecond);
        }

        [Fact]
        public async Task StorageValue_MatchesBalanceOfResult()
        {
            var amounts = new[] { OneToken * 100, OneToken * 50, OneToken * 25 };
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            foreach (var amount in amounts)
            {
                await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, amount);
            }

            var senderSlot = CalculateMappingSlot(_fixture.Address, BALANCES_SLOT);
            var recipientSlot = CalculateMappingSlot(_fixture.RecipientAddress, BALANCES_SLOT);

            var senderStorage = await _fixture.Node.GetStorageAtAsync(contractAddress, senderSlot);
            var recipientStorage = await _fixture.Node.GetStorageAtAsync(contractAddress, recipientSlot);

            var senderBalanceFromStorage = senderStorage.ToBigIntegerFromRLPDecoded();
            var recipientBalanceFromStorage = recipientStorage.ToBigIntegerFromRLPDecoded();

            var senderBalanceFromCall = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.Address);
            var recipientBalanceFromCall = await _fixture.GetERC20BalanceAsync(contractAddress, _fixture.RecipientAddress);

            Assert.Equal(senderBalanceFromCall, senderBalanceFromStorage);
            Assert.Equal(recipientBalanceFromCall, recipientBalanceFromStorage);
        }

        [Fact]
        public async Task Mint_UpdatesTotalSupplyStorage()
        {
            var firstMint = OneToken * 100;
            var secondMint = OneToken * 200;
            var contractAddress = await _fixture.DeployERC20Async(firstMint);

            var supplyStorageBefore = await _fixture.Node.GetStorageAtAsync(contractAddress, TOTAL_SUPPLY_SLOT);
            Assert.Equal(firstMint, supplyStorageBefore.ToBigIntegerFromRLPDecoded());

            await _fixture.MintERC20Async(contractAddress, _fixture.Address, secondMint);

            var supplyStorageAfter = await _fixture.Node.GetStorageAtAsync(contractAddress, TOTAL_SUPPLY_SLOT);
            Assert.Equal(firstMint + secondMint, supplyStorageAfter.ToBigIntegerFromRLPDecoded());
        }

        [Fact]
        public async Task GetStorageAt_ReturnsEmptyForUnusedSlot()
        {
            var contractAddress = await _fixture.DeployERC20Async();

            var unusedAddress = "0x9999999999999999999999999999999999999999";
            var slot = CalculateMappingSlot(unusedAddress, BALANCES_SLOT);

            var storageValue = await _fixture.Node.GetStorageAtAsync(contractAddress, slot);

            var balance = storageValue?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;
            Assert.Equal(BigInteger.Zero, balance);
        }

        private static BigInteger CalculateMappingSlot(string key, int mappingSlot)
        {
            var keyBytes = key.HexToByteArray();
            var paddedKey = new byte[32];
            Array.Copy(keyBytes, 0, paddedKey, 32 - keyBytes.Length, keyBytes.Length);

            var slotBytes = new byte[32];
            slotBytes[31] = (byte)mappingSlot;

            var combined = new byte[64];
            Array.Copy(paddedKey, 0, combined, 0, 32);
            Array.Copy(slotBytes, 0, combined, 32, 32);

            var hash = new Sha3Keccack().CalculateHash(combined);
            return hash.ToBigIntegerFromRLPDecoded();
        }

        private static BigInteger CalculateNestedMappingSlot(string key1, string key2, int mappingSlot)
        {
            var innerSlot = CalculateMappingSlot(key1, mappingSlot);

            var key2Bytes = key2.HexToByteArray();
            var paddedKey2 = new byte[32];
            Array.Copy(key2Bytes, 0, paddedKey2, 32 - key2Bytes.Length, key2Bytes.Length);

            var innerSlotBytes = innerSlot.ToByteArray(isUnsigned: true, isBigEndian: true);
            var paddedInnerSlot = new byte[32];
            if (innerSlotBytes.Length <= 32)
            {
                Array.Copy(innerSlotBytes, 0, paddedInnerSlot, 32 - innerSlotBytes.Length, innerSlotBytes.Length);
            }

            var combined = new byte[64];
            Array.Copy(paddedKey2, 0, combined, 0, 32);
            Array.Copy(paddedInnerSlot, 0, combined, 32, 32);

            var hash = new Sha3Keccack().CalculateHash(combined);
            return hash.ToBigIntegerFromRLPDecoded();
        }
    }
}
