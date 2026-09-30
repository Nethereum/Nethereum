using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainNodeTests
    {
        [Fact]
        public async Task StartAsync_CreatesGenesisBlock()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            Assert.Equal(0, blockNumber);

            var genesisBlock = await node.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);
            Assert.Equal(0, genesisBlock.BlockNumber);
        }

        [Fact]
        public async Task StartAsync_WithPrefundedAccounts_SetsBalance()
        {
            using var node = new DevChainNode();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };

            await node.StartAsync(addresses);

            var balance = await node.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(BigInteger.Parse("10000000000000000000000"), balance);
        }

        [Fact]
        public async Task StartAsync_WithCustomBalance_SetsCorrectBalance()
        {
            using var node = new DevChainNode();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };
            var customBalance = BigInteger.Parse("1000000000000000000");

            await node.StartAsync(addresses, customBalance);

            var balance = await node.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(customBalance, balance);
        }

        [Fact]
        public async Task SetBalance_UpdatesAccountBalance()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var balance = BigInteger.Parse("5000000000000000000");

            await node.SetBalanceAsync(address, balance);
            var retrieved = await node.GetBalanceAsync(address);

            Assert.Equal(balance, retrieved);
        }

        [Fact]
        public async Task SetNonce_UpdatesAccountNonce()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var nonce = new BigInteger(10);

            await node.SetNonceAsync(address, nonce);
            var retrieved = await node.GetNonceAsync(address);

            Assert.Equal(nonce, retrieved);
        }

        [Fact]
        public async Task SetCode_StoresAndRetrievesCode()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var code = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };

            await node.SetCodeAsync(address, code);
            var retrieved = await node.GetCodeAsync(address);

            Assert.Equal(code, retrieved);
        }

        [Fact]
        public async Task SetStorageAt_StoresAndRetrievesStorage()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var slot = BigInteger.Zero;
            var value = new byte[] { 0x01, 0x02, 0x03 };

            await node.SetStorageAtAsync(address, slot, value);
            var retrieved = await node.GetStorageAtAsync(address, slot);

            Assert.Equal(value, retrieved);
        }

        [Fact]
        public async Task MineBlock_IncreasesBlockNumber()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var initialBlockNumber = await node.GetBlockNumberAsync();
            await node.MineBlockAsync();
            var newBlockNumber = await node.GetBlockNumberAsync();

            Assert.Equal(initialBlockNumber + 1, newBlockNumber);
        }

        [Fact]
        public async Task MineBlock_ReturnsBlockHash()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var blockHash = await node.MineBlockAsync();

            Assert.NotNull(blockHash);
            Assert.Equal(32, blockHash.Length);
        }

        [Fact]
        public async Task GetBlockByHash_ReturnsCorrectBlock()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var blockHash = await node.MineBlockAsync();
            var block = await node.GetBlockByHashAsync(blockHash);

            Assert.NotNull(block);
            Assert.Equal(1, block.BlockNumber);
        }

        [Fact]
        public async Task TakeSnapshot_AllowsRevert()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            await node.SetBalanceAsync(address, 100);

            var snapshot = await node.TakeSnapshotAsync();

            await node.SetBalanceAsync(address, 500);
            Assert.Equal(500, await node.GetBalanceAsync(address));

            await node.RevertToSnapshotAsync(snapshot);
            Assert.Equal(100, await node.GetBalanceAsync(address));
        }

        [Fact]
        public async Task SendTransaction_ThrowsWhenNotInitialized()
        {
            using var node = new DevChainNode();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                node.SendTransactionAsync(null));
        }

        [Fact]
        public async Task GetPendingBlockContext_ReturnsNextBlockInfo()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var context = node.GetPendingBlockContext();

            Assert.Equal(1, context.BlockNumber);
        }

        [Fact]
        public async Task Config_ExposesConfiguration()
        {
            var config = new DevChainConfig { ChainId = 12345 };
            using var node = new DevChainNode(config);
            await node.StartAsync();

            Assert.Equal(12345, node.DevConfig.ChainId);
        }

        [Fact]
        public async Task GetNonce_ReturnsZeroForNonExistentAccount()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var nonce = await node.GetNonceAsync("0x1111111111111111111111111111111111111111");

            Assert.Equal(0, nonce);
        }

        [Fact]
        public async Task GetBalance_ReturnsZeroForNonExistentAccount()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var balance = await node.GetBalanceAsync("0x2222222222222222222222222222222222222222");

            Assert.Equal(0, balance);
        }

        [Fact]
        public async Task Dispose_CanBeCalledTwice_WithoutThrowing()
        {
            var node = new DevChainNode();
            await node.StartAsync();

            node.Dispose();
            node.Dispose();
        }

        [Fact]
        public async Task Dispose_AfterOperations_DoesNotThrow()
        {
            var node = new DevChainNode();
            await node.StartAsync(new[] { "0x1234567890123456789012345678901234567890" });
            await node.MineBlockAsync();

            node.Dispose();
        }

        [Fact]
        public async Task Given_MinedBlocks_When_SetHeadAsyncRewindsToAnEarlierBlock_Then_BlockNumberReportsTheNewHead()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            await node.MineBlockAsync();
            await node.MineBlockAsync();
            await node.MineBlockAsync();
            Assert.Equal(3, await node.GetBlockNumberAsync());

            await node.SetHeadAsync(1);

            Assert.Equal(1, await node.GetBlockNumberAsync());
        }

        [Fact]
        public async Task Given_MinedBlocks_When_SetHeadAsyncRewindsToAnEarlierBlock_Then_BlocksAboveTheNewHeadAreGone()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            await node.MineBlockAsync();
            await node.MineBlockAsync();
            await node.MineBlockAsync();

            await node.SetHeadAsync(1);

            Assert.NotNull(await node.GetBlockByNumberAsync(1));
            Assert.Null(await node.GetBlockByNumberAsync(2));
            Assert.Null(await node.GetBlockByNumberAsync(3));
        }

        [Fact]
        public async Task Given_ATransactionThatChangedABalance_When_SetHeadAsyncRewindsPastIt_Then_TheTransactionAndItsBalanceEffectAreGone()
        {
            const string senderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            const string senderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
            const string recipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
            var initialBalance = BigInteger.Parse("10000000000000000000000");
            var transferAmount = BigInteger.Parse("1000000000000000000");

            using var node = new DevChainNode();
            await node.StartAsync(new[] { senderAddress }, initialBalance);

            var blockBeforeTransfer = await node.GetBlockNumberAsync();
            var recipientBalanceBefore = await node.GetBalanceAsync(recipientAddress);

            var signer = new LegacyTransactionSigner();
            var signedTxHex = signer.SignTransaction(
                senderPrivateKey.HexToByteArray(), node.DevConfig.ChainId,
                recipientAddress, transferAmount, 0, 1_000_000_000, 21_000, "");
            var signedTx = TransactionFactory.CreateTransaction(signedTxHex);

            var result = await node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);
            Assert.Equal(recipientBalanceBefore + transferAmount, await node.GetBalanceAsync(recipientAddress));
            Assert.NotNull(await node.GetTransactionByHashAsync(signedTx.Hash));

            await node.SetHeadAsync(blockBeforeTransfer);

            Assert.Equal(recipientBalanceBefore, await node.GetBalanceAsync(recipientAddress));
            Assert.Null(await node.GetTransactionByHashAsync(signedTx.Hash));
            Assert.Null(await node.GetTransactionReceiptAsync(signedTx.Hash));
        }

        [Fact]
        public async Task Given_ATargetAtOrAboveTheCurrentHead_When_SetHeadAsyncIsCalled_Then_ItThrows()
        {
            using var node = new DevChainNode();
            await node.StartAsync();
            await node.MineBlockAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => node.SetHeadAsync(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => node.SetHeadAsync(5));
        }

        [Fact]
        public async Task Given_MinedBlocksThatChangedBalanceStorageAndCode_When_SetHeadAsyncRewindsToGenesis_Then_StateMatchesGenesisAndLaterBlocksAreGone()
        {
            using var node = new DevChainNode();
            await node.StartAsync();

            var address = "0x1234567890123456789012345678901234567890";
            var slot = BigInteger.Zero;

            var genesisBalance = await node.GetBalanceAsync(address);
            var genesisNonce = await node.GetNonceAsync(address);
            var genesisCode = await node.GetCodeAsync(address);
            var genesisStorage = await node.GetStorageAtAsync(address, slot);

            await node.SetBalanceAsync(address, genesisBalance + 500);
            await node.MineBlockAsync();

            await node.SetCodeAsync(address, new byte[] { 0x60, 0x60, 0x60, 0x40 });
            await node.MineBlockAsync();

            await node.SetStorageAtAsync(address, slot, new byte[] { 0x42 });
            await node.MineBlockAsync();

            Assert.Equal(3, await node.GetBlockNumberAsync());
            Assert.Equal(genesisBalance + 500, await node.GetBalanceAsync(address));
            Assert.Equal(new byte[] { 0x60, 0x60, 0x60, 0x40 }, await node.GetCodeAsync(address));
            Assert.Equal(new byte[] { 0x42 }, await node.GetStorageAtAsync(address, slot));

            await node.SetHeadAsync(0);

            Assert.Equal(0, await node.GetBlockNumberAsync());
            Assert.Null(await node.GetBlockByNumberAsync(1));
            Assert.Null(await node.GetBlockByNumberAsync(2));
            Assert.Null(await node.GetBlockByNumberAsync(3));

            Assert.Equal(genesisBalance, await node.GetBalanceAsync(address));
            Assert.Equal(genesisNonce, await node.GetNonceAsync(address));
            Assert.Equal(genesisCode, await node.GetCodeAsync(address));
            Assert.Equal(genesisStorage, await node.GetStorageAtAsync(address, slot));
        }
    }
}
