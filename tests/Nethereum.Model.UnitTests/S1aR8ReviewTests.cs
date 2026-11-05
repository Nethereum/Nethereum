using System;
using System.Linq;
using System.Numerics;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class S1aR8ReviewTests
    {
        private static byte[] E(params byte[] bytes) => RLP.RLP.EncodeElement(bytes);
        private static byte[] Filled(byte value, int length) => Enumerable.Repeat(value, length).ToArray();

        private static byte[] StatusWithTotalDifficulty(byte[] totalDifficulty) =>
            RLP.RLP.EncodeList(
                E(68), E(1), E(totalDifficulty), E(Filled(0x11, 32)), E(Filled(0x22, 32)),
                ForkIdEncoder.Encode(0u, 0));

        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public S1aR8ReviewTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        [Fact]
        public void Measure_RequestObjectsHeldWhileAHandlerRuns()
        {
            const int hashes = 300000;
            var hash = Filled(0x42, 32);
            var list = RLP.RLP.EncodeList(Enumerable.Repeat(E(hash), hashes).ToArray());

            var pooled = RLP.RLP.EncodeList(E(1), list);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var pooledMessage = GetPooledTransactionsMessageEncoder.Decode(pooled);
            var pooledBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            _out.WriteLine($"GetPooledTransactions: payload={pooled.Length} hashes={pooledMessage.Hashes.Count} allocated={pooledBytes} ratio={pooledBytes / (double)pooled.Length:F1}");

            var bodies = RLP.RLP.EncodeList(E(1), list);
            before = GC.GetAllocatedBytesForCurrentThread();
            var bodiesMessage = GetBlockBodiesMessageEncoder.Decode(bodies);
            var bodiesBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            _out.WriteLine($"GetBlockBodies: payload={bodies.Length} hashes={bodiesMessage.BlockHashes.Length} allocated={bodiesBytes} ratio={bodiesBytes / (double)bodies.Length:F1}");

            var codes = RLP.RLP.EncodeList(E(1), list, E(1));
            before = GC.GetAllocatedBytesForCurrentThread();
            var codesMessage = Nethereum.Model.P2P.Snap.GetByteCodesMessageEncoder.Decode(codes);
            var codesBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            _out.WriteLine($"GetByteCodes: payload={codes.Length} hashes={codesMessage.Hashes.Count} allocated={codesBytes} ratio={codesBytes / (double)codes.Length:F1}");
        }

        [Fact]
        public void S1aR8_04_Given_AnEth68StatusWhoseTotalDifficultyHasALeadingZero_When_Decoded_Then_ItIsRefused()
        {
            Assert.Equal(BigInteger.One, Eth68StatusMessageEncoder.Decode(StatusWithTotalDifficulty(new byte[] { 0x01 })).TotalDifficulty);

            Assert.ThrowsAny<InvalidOperationException>(
                () => Eth68StatusMessageEncoder.Decode(StatusWithTotalDifficulty(new byte[] { 0x00, 0x01 })));
        }

        [Fact]
        public void S1aR8_04_Given_ANewBlockWhoseTotalDifficultyHasALeadingZero_When_Decoded_Then_ItIsRefused()
        {
            var valid = NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = new BlockHeader
                {
                    BlockNumber = 1,
                    ParentHash = new byte[32],
                    UnclesHash = new byte[32],
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    MixHash = new byte[32],
                    ExtraData = new byte[0],
                    Nonce = new byte[8],
                    Coinbase = "0x0000000000000000000000000000000000000000"
                },
                TotalDifficulty = BigInteger.One
            });
            Assert.Equal(BigInteger.One, NewBlockMessageEncoder.Decode(valid).TotalDifficulty);

            var reader = new RlpReader(valid);
            reader.ReadListHeader();
            var block = reader.ReadRaw();
            var padded = RLP.RLP.EncodeList(block, E(0x00, 0x01));

            Assert.ThrowsAny<InvalidOperationException>(() => NewBlockMessageEncoder.Decode(padded));
        }
    }
}
