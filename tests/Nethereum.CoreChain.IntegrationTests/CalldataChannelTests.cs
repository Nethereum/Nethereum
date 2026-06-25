using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.Proving;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class CalldataChannelTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public CalldataChannelTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void ShouldCompressAndDecompressBrotli()
        {
            var data = new byte[10_000];
            new Random(42).NextBytes(data);

            var compressed = ChannelCompressor.Compress(data, CompressionAlgo.Brotli);
            var decompressed = ChannelCompressor.Decompress(compressed, CompressionAlgo.Brotli);

            Assert.Equal(data, decompressed);
            _output.WriteLine($"Brotli: {data.Length} → {compressed.Length} bytes ({(double)compressed.Length / data.Length:P1})");
        }

        [Fact]
        public void ShouldCompressAndDecompressZlib()
        {
            var data = new byte[10_000];
            new Random(42).NextBytes(data);

            var compressed = ChannelCompressor.Compress(data, CompressionAlgo.Zlib);
            var decompressed = ChannelCompressor.Decompress(compressed, CompressionAlgo.Zlib);

            Assert.Equal(data, decompressed);
            _output.WriteLine($"Zlib: {data.Length} → {compressed.Length} bytes ({(double)compressed.Length / data.Length:P1})");
        }

        [Fact]
        public void ShouldPassthroughNoCompression()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var result = ChannelCompressor.Compress(data, CompressionAlgo.None);
            Assert.Equal(data, result);

            var decompressed = ChannelCompressor.Decompress(result, CompressionAlgo.None);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void ShouldHandleEmptyData()
        {
            foreach (var algo in new[] { CompressionAlgo.None, CompressionAlgo.Brotli, CompressionAlgo.Zlib })
            {
                var result = ChannelCompressor.Compress(Array.Empty<byte>(), algo);
                Assert.Empty(result);
                _output.WriteLine($"{algo}: empty → empty");
            }
        }

        [Fact]
        public void ShouldCompressRepetitiveDataEfficiently()
        {
            var data = new byte[100_000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            var brotli = ChannelCompressor.Compress(data, CompressionAlgo.Brotli);
            var zlib = ChannelCompressor.Compress(data, CompressionAlgo.Zlib);

            Assert.True(brotli.Length < data.Length / 2, "Repetitive data should compress well with brotli");
            Assert.True(zlib.Length < data.Length / 2, "Repetitive data should compress well with zlib");

            var brotliDecomp = ChannelCompressor.Decompress(brotli, CompressionAlgo.Brotli);
            var zlibDecomp = ChannelCompressor.Decompress(zlib, CompressionAlgo.Zlib);
            Assert.Equal(data, brotliDecomp);
            Assert.Equal(data, zlibDecomp);

            _output.WriteLine($"100KB repetitive: brotli={brotli.Length}b ({(double)brotli.Length / data.Length:P1}), " +
                $"zlib={zlib.Length}b ({(double)zlib.Length / data.Length:P1})");
        }

        [Fact]
        public void ShouldRoundtripCalldataChannel()
        {
            var channel = new CalldataChannel(CompressionAlgo.Brotli);

            var blocks = new List<byte[]>();
            for (int i = 0; i < 10; i++)
            {
                var block = new byte[500];
                new Random(i).NextBytes(block);
                blocks.Add(block);
                channel.AddBlock(100 + i, block);
            }

            Assert.Equal(10, channel.BlockCount);
            Assert.Equal(100, channel.StartBlock);
            Assert.Equal(109, channel.EndBlock);

            var channelData = channel.Close();
            Assert.True(channelData.Length > 0);

            var (startBlock, endBlock, blockCount, decodedBlocks) = CalldataChannel.Decode(channelData);

            Assert.Equal(100, startBlock);
            Assert.Equal(109, endBlock);
            Assert.Equal(10, blockCount);
            Assert.Equal(10, decodedBlocks.Count);

            for (int i = 0; i < blocks.Count; i++)
                Assert.Equal(blocks[i], decodedBlocks[i]);

            _output.WriteLine($"Channel: {channel.RawSize}b raw → {channelData.Length}b compressed " +
                $"({(double)channelData.Length / channel.RawSize:P1}), {blockCount} blocks [{startBlock}-{endBlock}]");
        }

        [Fact]
        public void ShouldFrameChannelData()
        {
            var channel = new CalldataChannel(CompressionAlgo.Brotli);
            for (int i = 0; i < 50; i++)
            {
                var block = new byte[5000];
                new Random(i).NextBytes(block);
                channel.AddBlock(i, block);
            }

            var frames = channel.CloseAndFrame(maxFrameSize: 10_000);

            Assert.True(frames.Count >= 1);
            Assert.True(frames.Last().IsLast);
            Assert.All(frames.Where(f => !f.IsLast), f => Assert.Equal(10_000, f.Data.Length));

            for (int i = 0; i < frames.Count; i++)
                Assert.Equal(i, frames[i].FrameNumber);

            var reassembled = new byte[frames.Sum(f => f.Data.Length)];
            int offset = 0;
            foreach (var frame in frames)
            {
                Array.Copy(frame.Data, 0, reassembled, offset, frame.Data.Length);
                offset += frame.Data.Length;
            }

            var (_, _, blockCount, _) = CalldataChannel.Decode(reassembled);
            Assert.Equal(50, blockCount);

            _output.WriteLine($"50 blocks (5KB each): {frames.Count} frames, " +
                $"total={reassembled.Length}b, compression={((double)reassembled.Length / (50 * 5000)):P1}");
        }

        [Fact]
        public void ShouldRejectAddAfterClose()
        {
            var channel = new CalldataChannel();
            channel.AddBlock(1, new byte[] { 1, 2, 3 });
            channel.Close();

            Assert.Throws<InvalidOperationException>(() => channel.AddBlock(2, new byte[] { 4, 5, 6 }));
        }

        [Fact]
        public void ShouldRejectDoubleClose()
        {
            var channel = new CalldataChannel();
            channel.AddBlock(1, new byte[] { 1, 2, 3 });
            channel.Close();

            Assert.Throws<InvalidOperationException>(() => channel.Close());
        }

        [Fact]
        public void ShouldRoundtripCalldataEnvelope()
        {
            var data = new byte[5000];
            new Random(42).NextBytes(data);

            var publisher = new CalldataDataAvailabilityPublisher(CompressionAlgo.Brotli);
            var payload = new DaPayload { Data = data, Kind = DaPayloadKind.Block };
            var scope = new AnchorScope
            {
                ChainId = 420420,
                Kind = AnchorKind.Block,
                StartBlock = 1,
                EndBlock = 1,
                StateRoot = new byte[32]
            };

            var publication = publisher.PublishAsync(payload, scope).Result;

            Assert.NotNull(publication.Commitment);
            Assert.Equal(DaMode.Calldata, publication.Commitment.Type);
            Assert.NotNull(publication.Commitment.CommitmentHash);
            Assert.Equal(32, publication.Commitment.CommitmentHash.Length);
            Assert.NotNull(publication.CompressedPayload);
            Assert.True(publication.CompressedPayload.Length > 0);

            var decompressed = CompressedEnvelope.Unwrap(publication.CompressedPayload);
            Assert.Equal(data, decompressed);

            _output.WriteLine($"Envelope: {data.Length}b → {publication.CompressedPayload.Length}b compressed, " +
                $"commitment={publication.Commitment.CommitmentHash.ToHex(true)[..18]}");
        }

        [Fact]
        public async Task ShouldIntegrateCompressedCalldataInPipeline()
        {
            BigInteger chainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = ProofCadence.Continuous;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 3; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            var pipeline = new AnchorPublicationPipeline(
                    StateModel.MptKeccak, witnessStore)
                .AddContributor(new CalldataDataContributor(witnessStore, CompressionAlgo.Brotli));

            for (int b = 1; b <= 3; b++)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)chainId,
                    Kind = AnchorKind.Block,
                    StartBlock = b,
                    EndBlock = b,
                    StateRoot = block.StateRoot,
                    TransactionsRoot = block.TransactionsHash,
                    ReceiptsRoot = block.ReceiptHash
                };

                var result = await pipeline.ExecuteAsync(scope);
                Assert.NotNull(result.EncodedPayload);

                var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);

                var compressedSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.CompressedCalldata);
                Assert.NotNull(compressedSection);
                Assert.True(compressedSection.Bytes.Length > 2, "Compressed calldata should have version + algo + data");

                Assert.Equal((byte)1, compressedSection.Bytes[0]);
                Assert.Equal((byte)CompressionAlgo.Brotli, compressedSection.Bytes[1]);

                var decompressed = CompressedEnvelope.Unwrap(compressedSection.Bytes);
                var originalWitness = await witnessStore.GetWitnessAsync(b);
                Assert.Equal(originalWitness, decompressed);

                var uncompressedSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa);
                Assert.Null(uncompressedSection);

                _output.WriteLine($"Block {b}: witness={originalWitness.Length}b → compressed={compressedSection.Bytes.Length}b " +
                    $"({(double)compressedSection.Bytes.Length / originalWitness.Length:P1})");
            }

            _output.WriteLine("Compressed calldata in pipeline: PASS");
            node.Dispose();
        }

        [Fact]
        public async Task ShouldUseCalldataPublisherAsExternalDA()
        {
            BigInteger chainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.ProofCadence = ProofCadence.Off;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 3; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            var calldataPublisher = new CalldataDataAvailabilityPublisher(CompressionAlgo.Brotli);
            var pipeline = new AnchorPublicationPipeline(
                    StateModel.MptKeccak, witnessStore)
                .WithDaPublisher(calldataPublisher);

            var block = await node.GetBlockByNumberAsync(3);
            var scope = new AnchorScope
            {
                ChainId = (long)chainId,
                Kind = AnchorKind.Batch,
                StartBlock = 1,
                EndBlock = 3,
                StateRoot = block.StateRoot,
                TransactionsRoot = block.TransactionsHash,
                ReceiptsRoot = block.ReceiptHash
            };

            var result = await pipeline.ExecuteAsync(scope);

            Assert.NotNull(result.DaPublication);
            Assert.NotNull(result.DaPublication.Commitment);
            Assert.Equal(DaMode.Calldata, result.DaPublication.Commitment.Type);
            Assert.NotNull(result.DaPublication.CompressedPayload);

            var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);
            var daCommitment = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.DaCommitment);
            Assert.NotNull(daCommitment);
            Assert.Equal(32, daCommitment.Bytes.Length);

            var originalWitness = await witnessStore.GetWitnessAsync(3);
            var decompressed = CompressedEnvelope.Unwrap(result.DaPublication.CompressedPayload);
            Assert.Equal(originalWitness, decompressed);

            _output.WriteLine($"Batch 1-3: witness={originalWitness.Length}b → compressed={result.DaPublication.CompressedPayload.Length}b, " +
                $"commitment={daCommitment.Bytes.ToHex(true)[..18]}");
            _output.WriteLine("CalldataDataAvailabilityPublisher as external DA: PASS");

            node.Dispose();
        }

        [Fact]
        public void ShouldRoundtripChannelWithAllCompressionAlgos()
        {
            foreach (var algo in new[] { CompressionAlgo.None, CompressionAlgo.Brotli, CompressionAlgo.Zlib })
            {
                var channel = new CalldataChannel(algo);
                for (int i = 0; i < 5; i++)
                {
                    var block = new byte[1000];
                    new Random(i).NextBytes(block);
                    channel.AddBlock(i, block);
                }

                var channelData = channel.Close();
                var (startBlock, endBlock, blockCount, blocks) = CalldataChannel.Decode(channelData);

                Assert.Equal(0, startBlock);
                Assert.Equal(4, endBlock);
                Assert.Equal(5, blockCount);
                Assert.Equal(5, blocks.Count);

                _output.WriteLine($"{algo}: raw={channel.RawSize}b → encoded={channelData.Length}b");
            }
        }
    }
}
