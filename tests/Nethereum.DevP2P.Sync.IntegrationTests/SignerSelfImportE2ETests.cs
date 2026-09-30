using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class SignerSelfImportE2ETests
    {
        private static readonly TimeSpan ImportTimeout = TimeSpan.FromSeconds(20);

        private readonly ITestOutputHelper _output;

        public SignerSelfImportE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_ASignerThatSealedBlockN_When_ASiblingPushesBlockNPlusOneOnTopOfIt_Then_TheSignerImportsItAndSealsNPlusTwoOnTopOfThat()
        {
            var (signer, sibling) = await DevChainNetwork.TwoConnectedNodesAsync();
            DevChainFollower signerFollower = null;
            DevChainFollower siblingFollower = null;
            try
            {
                var signerProducer = await DevChainProducer.AttachAsync(signer);
                var siblingProducer = await DevChainProducer.AttachAsync(sibling);

                signerFollower = await DevChainFollower.AttachAsync(signer);
                await signerFollower.StartAsync();

                siblingFollower = await DevChainFollower.AttachAsync(sibling);
                await siblingFollower.StartAsync();

                var blockN = await signerProducer.ProduceAsync();
                Assert.NotNull(blockN?.Header);
                _output.WriteLine($"signer sealed block {blockN.Header.BlockNumber} (N) 0x{blockN.BlockHash.ToHex()}");

                var siblingImportedN = await DevChainNetwork.WaitUntilAsync(
                    () => siblingFollower.ExecutedHeight >= (long)blockN.Header.BlockNumber,
                    ImportTimeout);
                Assert.True(siblingImportedN,
                    $"the sibling never imported the signer's block N (executed={siblingFollower.ExecutedHeight})");

                var blockNPlus1 = await siblingProducer.ProduceAsync();
                Assert.NotNull(blockNPlus1?.Header);
                Assert.Equal(blockN.Header.BlockNumber + 1, blockNPlus1.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(blockNPlus1.Header.ParentHash, blockN.BlockHash),
                    "the sibling did not seal N+1 on top of the signer's block N");
                _output.WriteLine($"sibling sealed block {blockNPlus1.Header.BlockNumber} (N+1) on top of N");

                var signerImportedNPlus1 = await DevChainNetwork.WaitUntilAsync(
                    () => signerFollower.ExecutedHeight >= (long)blockNPlus1.Header.BlockNumber,
                    ImportTimeout);
                Assert.True(signerImportedNPlus1,
                    $"the signer never imported the sibling's block N+1 (executed={signerFollower.ExecutedHeight}); " +
                    "a signer that also seals must still import its siblings' blocks (S2 un-fork)");

                var signerBlockNPlus1Hash = await signer.Bundle.Blocks.GetHashByNumberAsync((long)blockNPlus1.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(signerBlockNPlus1Hash, blockNPlus1.BlockHash),
                    "the signer's block N+1 is not the sibling's sealed block, so this was not a real import");

                var blockNPlus2 = await signerProducer.ProduceAsync();
                Assert.NotNull(blockNPlus2?.Header);
                Assert.Equal(blockNPlus1.Header.BlockNumber + 1, blockNPlus2.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(blockNPlus2.Header.ParentHash, blockNPlus1.BlockHash),
                    "the signer sealed its next block on something other than the block it just imported");
                _output.WriteLine($"signer sealed block {blockNPlus2.Header.BlockNumber} (N+2) on top of the imported N+1");
            }
            finally
            {
                if (signerFollower != null) await signerFollower.DisposeAsync();
                if (siblingFollower != null) await siblingFollower.DisposeAsync();
                await signer.DisposeAsync();
                await sibling.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ASignerThatSealedBlockN_When_ItsOwnBlockNIsEchoedBackByASibling_Then_ItIsNotReExecuted()
        {
            var (signer, sibling) = await DevChainNetwork.TwoConnectedNodesAsync();
            DevChainFollower signerFollower = null;
            try
            {
                var signerProducer = await DevChainProducer.AttachAsync(signer);

                var sender = ChainAccounts.Sender;
                var recipient = ChainAccounts.Recipient;
                var tx = DevChainTransactions.SignEip1559(sender.PrivateKey, signer.ChainId, recipient.Address, 0, 1000);
                var submitted = await signer.Mempool.SubmitAsync(tx);
                Assert.True(submitted.Accepted, submitted.RejectMessage);

                signerFollower = await DevChainFollower.AttachAsync(signer);
                await signerFollower.StartAsync();

                var blockN = await signerProducer.ProduceAsync();
                Assert.NotNull(blockN?.Header);
                Assert.Contains(blockN.TransactionResults, r => ByteUtil.AreEqual(r.TxHash, tx.Hash));
                _output.WriteLine($"signer sealed block {blockN.Header.BlockNumber} (N) 0x{blockN.BlockHash.ToHex()} carrying the sender's tx");

                var nonceAfterSeal = (await signer.Bundle.State.GetAccountAsync(sender.Address.ToLowerInvariant())).Nonce;
                Assert.Equal(new BigInteger(1), (BigInteger)nonceAfterSeal);

                var sealedTransactions = (await signer.Bundle.Transactions.GetByBlockHashAsync(blockN.BlockHash))?.ToList()
                    ?? new System.Collections.Generic.List<Nethereum.Model.ISignedTransaction>();

                var echoedFromSibling = new Nethereum.Model.P2P.NewBlockMessage
                {
                    Header = blockN.Header,
                    Transactions = sealedTransactions,
                    Uncles = new System.Collections.Generic.List<Nethereum.Model.BlockHeader>(),
                    Withdrawals = new System.Collections.Generic.List<Nethereum.Model.Withdrawal>(),
                    TotalDifficulty = blockN.Header.Difficulty
                };
                signer.PushedBlocks.OnNewBlock(echoedFromSibling);

                await DevChainNetwork.StaysFalseAsync(
                    () => signerFollower.ExecutedHeight > (long)blockN.Header.BlockNumber,
                    TimeSpan.FromSeconds(5));
                _output.WriteLine($"signerFollower.ExecutedHeight after echo = {signerFollower.ExecutedHeight}; " +
                    $"pending in PushedBlocks = {signer.PushedBlocks.PendingCount}");

                var nonceAfterEcho = (await signer.Bundle.State.GetAccountAsync(sender.Address.ToLowerInvariant())).Nonce;
                Assert.Equal(new BigInteger(1), (BigInteger)nonceAfterEcho);

                var headHash = await signer.Bundle.Blocks.GetHashByNumberAsync((long)blockN.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(headHash, blockN.BlockHash),
                    "block N's stored hash changed after the echo, meaning it WAS re-executed and re-persisted");

                var tx2 = DevChainTransactions.SignEip1559(sender.PrivateKey, signer.ChainId, recipient.Address, 1, 1000);
                var submitted2 = await signer.Mempool.SubmitAsync(tx2);
                Assert.True(submitted2.Accepted, submitted2.RejectMessage);

                var blockNPlus1 = await signerProducer.ProduceAsync();
                Assert.NotNull(blockNPlus1?.Header);
                Assert.Equal(blockN.Header.BlockNumber + 1, blockNPlus1.Header.BlockNumber);
                Assert.True(ByteUtil.AreEqual(blockNPlus1.Header.ParentHash, blockN.BlockHash),
                    "the signer could not seal its next block cleanly after the echo");
                Assert.Contains(blockNPlus1.TransactionResults, r => ByteUtil.AreEqual(r.TxHash, tx2.Hash) && r.Success);
            }
            finally
            {
                if (signerFollower != null) await signerFollower.DisposeAsync();
                await signer.DisposeAsync();
                await sibling.DisposeAsync();
            }
        }
    }
}
