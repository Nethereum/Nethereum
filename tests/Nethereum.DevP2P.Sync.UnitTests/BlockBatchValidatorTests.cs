using System;
using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BlockBatchValidatorTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static readonly IBlockRootsProvider RootsProvider = PatriciaBlockRootsProvider.Instance;
        private static readonly Sha3Keccack Keccak = new();

        [Fact]
        public void ValidateParentChain_ThreeContiguousHeaders_ReturnsTrue()
        {
            var (headers, hashes) = BuildChain(blocks: 3, parentHash: new byte[32]);

            var ok = BlockBatchValidator.ValidateParentChain(
                headers, hashes, anchorHash: new byte[32], out var brokenAt);

            Assert.True(ok);
            Assert.Equal(-1, brokenAt);
        }

        [Fact]
        public void ValidateParentChain_BrokenLinkInMiddle_ReturnsFalseAtBrokenIndex()
        {
            var (headers, hashes) = BuildChain(blocks: 4, parentHash: new byte[32]);
            // Corrupt headers[2].ParentHash so the link from index 1→2 breaks.
            headers[2].ParentHash = new byte[32];

            var ok = BlockBatchValidator.ValidateParentChain(
                headers, hashes, anchorHash: new byte[32], out var brokenAt);

            Assert.False(ok);
            Assert.Equal(2, brokenAt);
        }

        [Fact]
        public void ValidateParentChain_AnchorMismatch_ReturnsFalseAtIndexZero()
        {
            var (headers, hashes) = BuildChain(blocks: 3, parentHash: new byte[32]);
            var wrongAnchor = new byte[32];
            wrongAnchor[0] = 0xff;

            var ok = BlockBatchValidator.ValidateParentChain(
                headers, hashes, anchorHash: wrongAnchor, out var brokenAt);

            Assert.False(ok);
            Assert.Equal(0, brokenAt);
        }

        [Fact]
        public void ValidateParentChain_NullAnchor_SkipsAnchorCheck()
        {
            var (headers, hashes) = BuildChain(blocks: 2, parentHash: new byte[32]);

            var ok = BlockBatchValidator.ValidateParentChain(
                headers, hashes, anchorHash: null, out var brokenAt);

            Assert.True(ok);
            Assert.Equal(-1, brokenAt);
        }

        [Fact]
        public void ValidateBodies_HappyPath_ReturnsTrue()
        {
            var (headers, _) = BuildChain(blocks: 2, parentHash: new byte[32]);
            var bodies = new List<BlockBody>
            {
                new() { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() },
                new() { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() },
            };

            var ok = BlockBatchValidator.ValidateBodies(headers, bodies, paired: 2, RootsProvider);

            Assert.True(ok);
        }

        [Fact]
        public void ValidateBodies_WrongTxRoot_ReturnsFalseAndFiresMismatchHandler()
        {
            var (headers, _) = BuildChain(blocks: 2, parentHash: new byte[32]);
            // Mutate header[1].TransactionsHash so the empty-tx-root no longer matches.
            headers[1].TransactionsHash = new byte[32];
            var bodies = new List<BlockBody>
            {
                new() { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() },
                new() { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() },
            };

            BlockBatchValidator.BodyMismatch captured = null;
            var ok = BlockBatchValidator.ValidateBodies(
                headers, bodies, paired: 2, RootsProvider, m => captured = m);

            Assert.False(ok);
            Assert.NotNull(captured);
            Assert.Equal(1, captured.Index);
            Assert.False(captured.TxRootOk);
            Assert.True(captured.UnclesOk);
        }

        [Fact]
        public void ValidateBodies_WrongUnclesHash_ReturnsFalse()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            headers[0].UnclesHash = new byte[32];
            var bodies = new List<BlockBody>
            {
                new() { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() },
            };

            var ok = BlockBatchValidator.ValidateBodies(headers, bodies, paired: 1, RootsProvider);

            Assert.False(ok);
        }

        [Fact]
        public void ValidateReceipts_WrongRoot_ReturnsFalseAndFiresHandler()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            headers[0].ReceiptHash = new byte[32];
            var receipts = new List<List<Receipt>>
            {
                // Non-empty receipt list so the computed root is something
                // other than the EmptyTrieRoot baked into the header.
                new List<Receipt> { Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>()) },
            };

            BlockBatchValidator.ReceiptMismatch captured = null;
            var ok = BlockBatchValidator.ValidateReceipts(
                headers, receipts, paired: 1, RootsProvider,
                shouldValidateBlock: null,
                onMismatch: m => captured = m);

            Assert.False(ok);
            Assert.NotNull(captured);
            Assert.Equal(0, captured.Index);
        }

        [Fact]
        public void ValidateReceipts_PreByzantiumPredicateSkips_ReturnsTrueWithoutCheck()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            // Deliberately corrupt the header receipts-root so a hard check would fail.
            headers[0].ReceiptHash = new byte[32];
            var receipts = new List<List<Receipt>>
            {
                new List<Receipt> { Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>()) },
            };

            var ok = BlockBatchValidator.ValidateReceipts(
                headers, receipts, paired: 1, RootsProvider,
                shouldValidateBlock: _ => false);

            Assert.True(ok);
        }

        [Fact]
        public void RealignBodies_OutOfOrder_ReturnsBodiesInHeaderOrder()
        {
            var (headers, _) = BuildChain(blocks: 3, parentHash: new byte[32]);
            // Stamp distinct TransactionsHash on each header so reordering is detectable.
            var txsA = new List<ISignedTransaction> { MakeTx(seed: 0xA) };
            var txsB = new List<ISignedTransaction> { MakeTx(seed: 0xB) };
            var txsC = new List<ISignedTransaction> { MakeTx(seed: 0xC) };
            headers[0].TransactionsHash = RootsProvider.CalculateTransactionsRoot(txsA);
            headers[1].TransactionsHash = RootsProvider.CalculateTransactionsRoot(txsB);
            headers[2].TransactionsHash = RootsProvider.CalculateTransactionsRoot(txsC);

            var bodyA = new BlockBody { Transactions = txsA, Uncles = new List<BlockHeader>() };
            var bodyB = new BlockBody { Transactions = txsB, Uncles = new List<BlockHeader>() };
            var bodyC = new BlockBody { Transactions = txsC, Uncles = new List<BlockHeader>() };

            var shuffled = new List<BlockBody> { bodyC, bodyA, bodyB };

            var realigned = BlockBatchValidator.RealignBodies(
                headers, shuffled, RootsProvider, out var unmatchedAt);

            Assert.Equal(3, realigned.Count);
            Assert.Equal(3, unmatchedAt);
            Assert.Same(bodyA, realigned[0]);
            Assert.Same(bodyB, realigned[1]);
            Assert.Same(bodyC, realigned[2]);
        }

        [Fact]
        public void RealignBodies_MissingBody_ReturnsShortListWithUnmatchedIndex()
        {
            var (headers, _) = BuildChain(blocks: 3, parentHash: new byte[32]);
            var txsA = new List<ISignedTransaction> { MakeTx(seed: 0xA) };
            var txsC = new List<ISignedTransaction> { MakeTx(seed: 0xC) };
            headers[0].TransactionsHash = RootsProvider.CalculateTransactionsRoot(txsA);
            // header[1] keeps its empty TransactionsHash — peer drops this body.
            headers[2].TransactionsHash = RootsProvider.CalculateTransactionsRoot(txsC);

            var bodyA = new BlockBody { Transactions = txsA, Uncles = new List<BlockHeader>() };
            var bodyC = new BlockBody { Transactions = txsC, Uncles = new List<BlockHeader>() };

            // Peer omits the empty body for header[1] entirely.
            var partial = new List<BlockBody> { bodyA, bodyC };

            var realigned = BlockBatchValidator.RealignBodies(
                headers, partial, RootsProvider, out var unmatchedAt);

            Assert.Equal(1, realigned.Count);
            Assert.Equal(1, unmatchedAt);
        }

        [Fact]
        public void RealignReceipts_PostByzantiumOutOfOrder_RecoversByRoot()
        {
            var (headers, _) = BuildChain(blocks: 2, parentHash: new byte[32]);
            var rcptA = new List<Receipt>
            {
                Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>())
            };
            var rcptB = new List<Receipt>
            {
                Receipt.CreateStatusReceipt(true, 42000, new byte[256], new List<Log>())
            };
            headers[0].ReceiptHash = RootsProvider.CalculateReceiptsRoot(rcptA);
            headers[1].ReceiptHash = RootsProvider.CalculateReceiptsRoot(rcptB);

            var shuffled = new List<List<Receipt>> { rcptB, rcptA };

            var realigned = BlockBatchValidator.RealignReceipts(
                headers, shuffled, paired: 2, RootsProvider,
                isPostByzantium: _ => true, out var unmatchedAt);

            Assert.Equal(2, realigned.Count);
            Assert.Equal(2, unmatchedAt);
            Assert.Same(rcptA, realigned[0]);
            Assert.Same(rcptB, realigned[1]);
        }

        private static (List<BlockHeader> headers, List<byte[]> hashes) BuildChain(
            int blocks, byte[] parentHash)
        {
            var headers = new List<BlockHeader>();
            var hashes = new List<byte[]>();
            var prevHash = parentHash;
            for (int n = 0; n < blocks; n++)
            {
                var header = MakeEmptyHeader(n, prevHash);
                var hash = Keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                headers.Add(header);
                hashes.Add(hash);
                prevHash = hash;
            }
            return (headers, hashes);
        }

        private static BlockHeader MakeEmptyHeader(long blockNumber, byte[] parentHash) =>
            new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)blockNumber),
                ParentHash = (byte[])parentHash.Clone(),
                TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
                UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            };

        private static ISignedTransaction MakeTx(byte seed)
        {
            // Distinct legacy tx so the tx-root differs per seed. Receiver
            // address discriminates; signatures left at default zero.
            var receiver = new byte[20];
            receiver[0] = seed;
            return new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { 0x00 },
                data: new byte[0]);
        }

        [Fact]
        public void ValidateBodies_ShanghaiHeaderWrongWithdrawalsRoot_ReturnsFalse()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 0, ValidatorIndex = 1, Address = new byte[20], AmountInGwei = 100 }
            };
            // Pretend Shanghai+: set wrong withdrawalsRoot so the recomputed root mismatches.
            headers[0].WithdrawalsRoot = new byte[32];
            var bodies = new List<BlockBody>
            {
                new() {
                    Transactions = new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = withdrawals,
                },
            };

            BlockBatchValidator.BodyMismatch captured = null;
            var ok = BlockBatchValidator.ValidateBodies(
                headers, bodies, paired: 1, RootsProvider, m => captured = m);

            Assert.False(ok);
            Assert.NotNull(captured);
            Assert.False(captured.WithdrawalsOk);
        }

        [Fact]
        public void ValidateBodies_ShanghaiHeaderMissingBodyWithdrawals_ReturnsFalse()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            // Header asserts withdrawals present but body has Withdrawals=null.
            headers[0].WithdrawalsRoot = RootsProvider.CalculateWithdrawalsRoot(new List<Withdrawal>());
            var bodies = new List<BlockBody>
            {
                new() {
                    Transactions = new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = null,
                },
            };

            var ok = BlockBatchValidator.ValidateBodies(headers, bodies, paired: 1, RootsProvider);

            Assert.False(ok);
        }

        [Fact]
        public void ValidateBodies_PreShanghaiHeaderBodyHasWithdrawals_ReturnsFalse()
        {
            var (headers, _) = BuildChain(blocks: 1, parentHash: new byte[32]);
            // headers[0].WithdrawalsRoot is null (pre-Shanghai) — body has withdrawals → invalid.
            var bodies = new List<BlockBody>
            {
                new() {
                    Transactions = new List<ISignedTransaction>(),
                    Uncles = new List<BlockHeader>(),
                    Withdrawals = new List<Withdrawal>
                    {
                        new Withdrawal { Index = 0, ValidatorIndex = 0, Address = new byte[20], AmountInGwei = 0 }
                    },
                },
            };

            var ok = BlockBatchValidator.ValidateBodies(headers, bodies, paired: 1, RootsProvider);

            Assert.False(ok);
        }

        [Fact]
        public void RealignBodies_ShanghaiBlocksDistinguishedByWithdrawalsRoot()
        {
            var (headers, _) = BuildChain(blocks: 2, parentHash: new byte[32]);
            var withdrawalsA = new List<Withdrawal>
            {
                new Withdrawal { Index = 0, ValidatorIndex = 1, Address = new byte[20], AmountInGwei = 100 }
            };
            var withdrawalsB = new List<Withdrawal>
            {
                new Withdrawal { Index = 1, ValidatorIndex = 2, Address = new byte[20], AmountInGwei = 200 }
            };
            // Same empty txs + uncles on both headers but different withdrawals.
            headers[0].WithdrawalsRoot = RootsProvider.CalculateWithdrawalsRoot(withdrawalsA);
            headers[1].WithdrawalsRoot = RootsProvider.CalculateWithdrawalsRoot(withdrawalsB);

            var bodyA = new BlockBody
            {
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = withdrawalsA,
            };
            var bodyB = new BlockBody
            {
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = withdrawalsB,
            };
            // Peer returns in reverse order.
            var shuffled = new List<BlockBody> { bodyB, bodyA };

            var realigned = BlockBatchValidator.RealignBodies(
                headers, shuffled, RootsProvider, out var unmatchedAt);

            Assert.Equal(2, realigned.Count);
            Assert.Equal(2, unmatchedAt);
            Assert.Same(bodyA, realigned[0]);
            Assert.Same(bodyB, realigned[1]);
        }
    }
}
