using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FrozenBlockHashProjectorTests
    {
        private const int BlockCount = 40;

        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        [Fact]
        public void Given_RolledStoreWithSignedTxs_When_ProjectedInChunks_Then_MatchesSerialPerBlockHashAndTxHashes_AcrossSealedAndTail()
        {
            var dir = Path.Combine(Path.GetTempPath(), "byhash-projector-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var codecs = new FreezerCodecSet();
                WriteStore(dir, codecs);
                using var freezer = FreezerCore.Open(new FreezerLayout(dir, maxFileSize: 512), FreezerOpenMode.ReadOnly);
                var head = freezer.Items;

                var sealedHead = freezer.SealedHead("bodies");
                Assert.True(sealedHead > 8, "need several sealed body windows");
                Assert.True(sealedHead < head, "need an open tail so the committed-tail read path is exercised");

                var projected = new List<(byte[] BlockHash, IReadOnlyList<byte[]> TxHashes)>();
                for (var n = 0L; n < head;)
                {
                    var bodiesChunk = freezer.DecodeSealedChunk("bodies", n, targetBytes: 8 * 32, head, maxDegreeOfParallelism: 4);
                    Assert.NotEmpty(bodiesChunk);
                    var hashesChunk = freezer.DecodeSealedRange("hashes", n, bodiesChunk.Count, head, maxDegreeOfParallelism: 1);
                    var chunk = FrozenBlockHashProjector.ProjectChunk(bodiesChunk, hashesChunk, codecs, maxDegreeOfParallelism: 4);
                    Assert.NotEmpty(chunk);
                    projected.AddRange(chunk);
                    n += chunk.Count;
                }

                Assert.Equal(head, projected.Count);
                var sawTxs = false;
                for (var n = 0L; n < head; n++)
                {
                    var expectedHash = codecs.Hashes.Decode(freezer.ReadHash(n));
                    var expectedTxHashes = codecs.Bodies.Decode(freezer.ReadBody(n)).Txs.Select(tx => tx?.Hash).ToList();

                    Assert.Equal(expectedHash, projected[(int)n].BlockHash);
                    Assert.Equal(expectedTxHashes.Count, projected[(int)n].TxHashes.Count);
                    for (var t = 0; t < expectedTxHashes.Count; t++)
                        Assert.Equal(expectedTxHashes[t], projected[(int)n].TxHashes[t]);

                    if (expectedTxHashes.Count > 0) sawTxs = true;
                }
                Assert.True(sawTxs, "fixture must contain transactions so the tx-hash mapping is exercised");
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        private void WriteStore(string dir, FreezerCodecSet codecs)
        {
            var bal = Array.Empty<byte>();
            using var writer = FreezerCore.Open(new FreezerLayout(dir, maxFileSize: 512), FreezerOpenMode.Append);
            var batch = writer.BeginBatch();
            for (long b = 0; b < BlockCount; b++)
            {
                var hash = new byte[32];
                hash[0] = (byte)b;
                hash[31] = (byte)(b >> 8);
                var body = new BlockBodyCluster(TxsFor(b), new List<BlockHeader>(), null);
                batch.AppendCluster(b, new FrozenBlockCluster(
                    codecs.Headers.Encode(SyntheticHeader(100 + b)), hash,
                    codecs.Bodies.Encode(body), codecs.Receipts.Encode(new List<ReceiptForStorage>()), codecs.Bals.Encode(bal)));
            }
            batch.Commit();
        }

        private List<ISignedTransaction> TxsFor(long b)
        {
            var txs = new List<ISignedTransaction>();
            for (var index = 0; index < (int)(b % 3); index++)
                txs.Add(MakeTx(b, index));
            return txs;
        }

        private ISignedTransaction MakeTx(long b, int index)
        {
            var receiver = new byte[20];
            receiver[0] = (byte)b;
            receiver[19] = (byte)index;
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static BlockHeader SyntheticHeader(long blockNumber) => new()
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
            GasUsed = 0,
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
        };
    }
}
