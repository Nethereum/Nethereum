using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class ReceiptFieldDeriverTests
    {
        private readonly HeaderItemCodec _headerCodec = new();
        private readonly BodyClusterItemCodec _bodyCodec = new();
        private readonly ReceiptsItemCodec _receiptsCodec = new();
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly ReceiptFieldDeriver _deriver;

        public ReceiptFieldDeriverTests()
        {
            _deriver = new ReceiptFieldDeriver(_signer, new CancunBlobBaseFeeFractionResolver());
        }


        private (BlockHeader Header, BlockBodyCluster Body, IReadOnlyList<ReceiptForStorage> Stored) LoadBlock(long item)
        {
            var header = _headerCodec.Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", true, item));
            var body = _bodyCodec.Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", true, item));
            var stored = _receiptsCodec.Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", true, item));
            return (header, body, stored);
        }

        private static long CorpusBlockCount() =>
            CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "headers", compressed: true);

        private long FindItemWithLogs()
        {
            var count = CorpusBlockCount();
            for (var item = 0L; item < count; item++)
            {
                var (_, _, stored) = LoadBlock(item);
                if (stored.Any(r => r.Logs.Count > 0))
                    return item;
            }

            Assert.Fail("expected at least one block with logs in the geth-ancient-txs corpus slice");
            return -1;
        }


        [Fact]
        public void Given_RealGethBlockReceipts_When_Derived_Then_ReceiptTrieRootMatchesHeaderReceiptsRoot()
        {
            var count = CorpusBlockCount();
            var blocksWithTxsChecked = 0;

            for (var item = 0L; item < count; item++)
            {
                var (header, body, stored) = LoadBlock(item);
                if (body.Txs.Count == 0) continue;
                blocksWithTxsChecked++;

                var derived = _deriver.Derive(header, body, stored);
                var root = ComputeReceiptsRoot(derived);

                Assert.Equal(header.ReceiptHash, root);
            }

            Assert.True(blocksWithTxsChecked > 0,
                "expected at least one block with transactions in the geth-ancient-txs corpus slice");
        }

        private static byte[] ComputeReceiptsRoot(IReadOnlyList<DerivedReceipt> derived)
        {
            var encoded = new List<byte[]>(derived.Count);
            foreach (var r in derived)
            {
                var receipt = new Receipt
                {
                    PostStateOrStatus = r.PostStateOrStatus,
                    CumulativeGasUsed = EvmUInt256BigIntegerExtensions.FromBigInteger(r.CumulativeGasUsed),
                    Bloom = r.Bloom,
                    Logs = r.Logs.Select(l => l.Log).ToList()
                };

                var isLegacy = r.TransactionType == TransactionType.LegacyTransaction ||
                               r.TransactionType == TransactionType.LegacyChainTransaction;

                encoded.Add(isLegacy
                    ? ReceiptEncoder.Current.Encode(receipt)
                    : ReceiptEncoder.Current.EncodeTyped(receipt, (byte)r.TransactionType));
            }

            return new Nethereum.CoreChain.PatriciaBlockRootCalculator().ComputeReceiptsRoot(encoded);
        }


        [Fact]
        public void Given_RealGethBlock_When_Derived_Then_BlockBloomMatchesHeaderLogsBloom()
        {
            var item = FindItemWithLogs();
            var (header, body, stored) = LoadBlock(item);

            var derived = _deriver.Derive(header, body, stored);

            var blockBloom = new byte[256];
            foreach (var receipt in derived)
                OrInto(blockBloom, receipt.Bloom);

            Assert.Equal(header.LogsBloom, blockBloom);
            Assert.Contains(derived, r => r.Bloom.Any(b => b != 0));
        }

        private static void OrInto(byte[] accumulator, byte[] bloom)
        {
            for (var i = 0; i < accumulator.Length; i++)
                accumulator[i] |= bloom[i];
        }

        [Fact]
        public void Given_RealGethBlock_When_Derived_Then_PerTxGasUsedSumsToHeaderGasUsed()
        {
            var (header, body, stored) = LoadBlock(0);
            Assert.NotEmpty(body.Txs);

            var derived = _deriver.Derive(header, body, stored);

            var totalGasUsed = derived.Aggregate(BigInteger.Zero, (acc, r) => acc + r.GasUsed);

            Assert.Equal((BigInteger)header.GasUsed, totalGasUsed);
            Assert.Equal(stored[stored.Count - 1].CumulativeGasUsed, totalGasUsed);
        }

        [Fact]
        public void Given_RealGethBlock_When_Derived_Then_LogIndexIsBlockCumulative()
        {
            var item = FindItemWithLogs();
            var (header, body, stored) = LoadBlock(item);

            var derived = _deriver.Derive(header, body, stored);

            var logIndexes = derived.SelectMany(r => r.Logs).Select(l => l.LogIndex).ToList();
            var expected = Enumerable.Range(0, logIndexes.Count).ToList();

            Assert.Equal(expected, logIndexes);
        }

        [Fact]
        public void Given_RealGethBlock_When_Derived_Then_TxHashMatchesSiblingBodyTx()
        {
            var (header, body, stored) = LoadBlock(0);
            Assert.NotEmpty(body.Txs);

            var derived = _deriver.Derive(header, body, stored);

            for (var i = 0; i < derived.Count; i++)
                Assert.Equal(body.Txs[i].Hash, derived[i].TxHash);
        }


        [Fact]
        public void Given_CreateTxBlock_When_Derived_Then_ContractAddressMatchesEthGetTxReceipt()
        {
            var found = FindCreateTxInCorpus();
            Assert.NotNull(found);
            var (header, body, stored, txIndex) = found.Value;
            var derived = _deriver.Derive(header, body, stored);

            const string gethAuthoritativeSender = "0x02af2459a93d0b3f4d062636236cd4b29e3bcecf";
            const string createdContractAddress = "0xf24efd695f90f8bd42f964ac993c662320eb2666";

            Assert.Equal(Normalize(gethAuthoritativeSender), Normalize(_signer.GetSenderAddress(body.Txs[txIndex])));
            Assert.Equal(Normalize(createdContractAddress), Normalize(derived[txIndex].ContractAddress));

            AssertSyntheticCreateTxContractAddress();
        }

        private static string Normalize(string address) =>
            (address ?? "").StartsWith("0x") ? address.Substring(2).ToLowerInvariant() : (address ?? "").ToLowerInvariant();

        private (BlockHeader Header, BlockBodyCluster Body, IReadOnlyList<ReceiptForStorage> Stored, int TxIndex)?
            FindCreateTxInCorpus()
        {
            var count = CorpusBlockCount();
            for (var item = 0L; item < count; item++)
            {
                var (header, body, stored) = LoadBlock(item);
                for (var i = 0; i < body.Txs.Count; i++)
                {
                    if (body.Txs[i].IsContractCreation())
                        return (header, body, stored, i);
                }
            }
            return null;
        }

        private void AssertSyntheticCreateTxContractAddress()
        {
            var privateKeyHex = "0x" + new string('0', 63) + "1";
            var key = new EthECKey(privateKeyHex);
            var nonce = new EvmUInt256(5UL);

            var rawTx = new LegacyTransaction(
                nonce.ToBytesForRLPEncoding(),
                new EvmUInt256(1_000_000_000UL).ToBytesForRLPEncoding(),
                new EvmUInt256(500_000UL).ToBytesForRLPEncoding(),
                null,
                EvmUInt256.Zero.ToBytesForRLPEncoding(),
                new byte[] { 0x60, 0x00 });

            var signature = key.SignAndCalculateV(rawTx.RawHash);
            rawTx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            var header = SyntheticHeader(blockNumber: 1);
            var body = new BlockBodyCluster(new List<ISignedTransaction> { rawTx }, new List<BlockHeader>(), null);
            var stored = new List<ReceiptForStorage> { new(new byte[] { 1 }, 21064, new List<Log>()) };

            var derived = _deriver.Derive(header, body, stored);

            var expectedSender = key.GetPublicAddress();
            var expectedAddress = ContractUtils.CalculateContractAddress(expectedSender, nonce.ToLong());

            Assert.True(rawTx.IsContractCreation());
            Assert.Equal(expectedSender, _signer.GetSenderAddress(rawTx));
            Assert.Equal(expectedAddress, derived[0].ContractAddress);
        }


        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Derive the EIP-1559 effective gas price")]
        public void Given_1559Tx_When_EffectiveGasPrice_Then_MinCapPlusBaseFee()
        {
            var maxFeePerGas = new EvmUInt256(100_000_000_000UL);
            var maxPriorityFeePerGas = new EvmUInt256(2_000_000_000UL);
            var baseFee = new EvmUInt256(50_000_000_000UL);
            var expected = new EvmUInt256(52_000_000_000UL);

            var tx = TransactionFactory.Create1559Transaction(
                chainId: 1, nonce: 0, maxPriorityFeePerGas: maxPriorityFeePerGas, maxFeePerGas: maxFeePerGas,
                gasLimit: 21000, to: "0x0000000000000000000000000000000000000001", amount: 0, data: "0x",
                accessList: null,
                r: "0x" + new string('1', 64), s: "0x" + new string('2', 64), v: "0x01");

            var header = SyntheticHeader(blockNumber: 20_000_000, baseFee: baseFee);
            var body = new BlockBodyCluster(new List<ISignedTransaction> { tx }, new List<BlockHeader>(), new List<Withdrawal>());
            var stored = new List<ReceiptForStorage> { new(new byte[] { 1 }, 21000, new List<Log>()) };

            var derived = _deriver.Derive(header, body, stored);

            Assert.Equal(expected, derived[0].EffectiveGasPrice);
        }


        [Fact]
        public void Given_BlobTxAtCancun_When_BlobGasPrice_Then_UsesForkFraction()
        {
            var excessBlobGas = 5_000_000L;
            var (header, body, stored) = SyntheticBlobBlock(excessBlobGas, blobCount: 2);

            var derived = _deriver.Derive(header, body, stored);

            var expectedBlobGasPrice = BlobGasCalculator.CalculateBlobBaseFee(
                new EvmUInt256(excessBlobGas), new EvmUInt256(BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION));

            Assert.Equal((BigInteger?)(2 * BlobGasCalculator.GAS_PER_BLOB), derived[0].BlobGasUsed);
            Assert.Equal((EvmUInt256?)expectedBlobGasPrice, derived[0].BlobGasPrice);
        }

        [Fact]
        public void Given_BlobTxWithWrongForkFraction_When_BlobGasPrice_Then_Differs()
        {
            var excessBlobGas = 5_000_000L;
            var (header, body, stored) = SyntheticBlobBlock(excessBlobGas, blobCount: 2);

            var wrongFractionDeriver = new ReceiptFieldDeriver(_signer, new FixedFractionResolver(1_000_000));

            var correct = _deriver.Derive(header, body, stored);
            var wrong = wrongFractionDeriver.Derive(header, body, stored);

            Assert.NotEqual(correct[0].BlobGasPrice, wrong[0].BlobGasPrice);
        }

        private sealed class FixedFractionResolver : IBlobBaseFeeFractionResolver
        {
            private readonly EvmUInt256 _fraction;
            public FixedFractionResolver(long fraction) => _fraction = new EvmUInt256(fraction);
            public EvmUInt256 FractionForBlock(BlockHeader header) => _fraction;
        }

        private (BlockHeader Header, BlockBodyCluster Body, IReadOnlyList<ReceiptForStorage> Stored) SyntheticBlobBlock(
            long excessBlobGas, int blobCount)
        {
            var blobHashes = Enumerable.Range(0, blobCount).Select(i => new byte[32]).Cast<byte[]>().ToList();

            var tx = TransactionFactory.Create4844Transaction(
                chainId: 1, nonce: 0, maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 50_000_000_000,
                gasLimit: 21000, to: "0x0000000000000000000000000000000000000001", amount: 0, data: "0x",
                accessList: null, maxFeePerBlobGas: 10_000_000_000, blobVersionedHashes: blobHashes,
                r: "0x" + new string('1', 64), s: "0x" + new string('2', 64), v: "0x01");

            var header = SyntheticHeader(blockNumber: 20_000_000, baseFee: new EvmUInt256(30_000_000_000UL));
            header.ExcessBlobGas = excessBlobGas;

            var body = new BlockBodyCluster(new List<ISignedTransaction> { tx }, new List<BlockHeader>(), new List<Withdrawal>());
            var stored = new List<ReceiptForStorage> { new(new byte[] { 1 }, 21000, new List<Log>()) };

            return (header, body, stored);
        }

        private static BlockHeader SyntheticHeader(long blockNumber, EvmUInt256? baseFee = null)
        {
            return new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                BlockNumber = new EvmUInt256((ulong)blockNumber),
                LogsBloom = new byte[256],
                Difficulty = EvmUInt256.Zero,
                Timestamp = 0,
                GasLimit = 30_000_000,
                GasUsed = 21000,
                MixHash = new byte[32],
                ExtraData = System.Array.Empty<byte>(),
                Nonce = new byte[8],
                BaseFee = baseFee
            };
        }
    }
}
