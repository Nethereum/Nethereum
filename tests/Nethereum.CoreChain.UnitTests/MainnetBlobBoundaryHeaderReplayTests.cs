using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class MainnetBlobBoundaryHeaderReplayTests
    {
        public sealed class HeaderRow
        {
            public HeaderRow(long number, long timestamp, ulong baseFee, long blobGasUsed, long excessBlobGas)
            {
                Number = number;
                Timestamp = timestamp;
                BaseFee = baseFee;
                BlobGasUsed = blobGasUsed;
                ExcessBlobGas = excessBlobGas;
            }

            public long Number { get; }
            public long Timestamp { get; }
            public ulong BaseFee { get; }
            public long BlobGasUsed { get; }
            public long ExcessBlobGas { get; }

            public BlockHeader ToHeader() => new BlockHeader
            {
                BlockNumber = Number,
                Timestamp = Timestamp,
                BaseFee = new EvmUInt256(BaseFee),
                BlobGasUsed = BlobGasUsed,
                ExcessBlobGas = ExcessBlobGas
            };
        }

        private static readonly HeaderRow[] MainnetHeaders =
        {
            new HeaderRow(23935691, 1764798515, 33038810, 1179648, 0),
            new HeaderRow(23935692, 1764798527, 32176367, 0, 393216),
            new HeaderRow(23935693, 1764798539, 33413480, 1179648, 0),
            new HeaderRow(23935694, 1764798563, 31400124, 1179648, 393216),
            new HeaderRow(23935695, 1764798575, 31853945, 0, 786432),
            new HeaderRow(23935696, 1764798587, 34755102, 1179648, 786432),
            new HeaderRow(23935697, 1764798599, 30711148, 393216, 1179648),
            new HeaderRow(23975775, 1765290035, 221207824, 786432, 82485040),
            new HeaderRow(23975776, 1765290047, 212651838, 1048576, 82485040),
            new HeaderRow(23975777, 1765290059, 237807771, 917504, 82747184),
            new HeaderRow(23975778, 1765290071, 227355166, 655360, 83053018),
            new HeaderRow(23975779, 1765290083, 218750581, 524288, 83271471),
            new HeaderRow(23975780, 1765290095, 205247976, 393216, 83446233),
            new HeaderRow(23975781, 1765290107, 186358643, 0, 83577305),
            new HeaderRow(24179380, 1767747635, 36745968, 655360, 121591385),
            new HeaderRow(24179381, 1767747647, 32969686, 917504, 121809838),
            new HeaderRow(24179382, 1767747659, 36361111, 0, 121416622),
            new HeaderRow(24179383, 1767747671, 32870555, 1048576, 121416622),
            new HeaderRow(24179384, 1767747683, 32751168, 393216, 121766147),
            new HeaderRow(24179385, 1767747695, 34699781, 0, 121897219),
            new HeaderRow(24179386, 1767747707, 38203289, 1966080, 121897219),
        };

        public static IEnumerable<object[]> ChildHeaders() =>
            Enumerable.Range(1, MainnetHeaders.Length - 1)
                .Where(i => MainnetHeaders[i].Number == MainnetHeaders[i - 1].Number + 1)
                .Select(i => new object[] { MainnetHeaders[i].Number });

        private static ChainConfig Mainnet() => new ChainConfig { Activations = MainnetChainActivations.Instance };

        [Theory]
        [MemberData(nameof(ChildHeaders))]
        public void Given_ARealMainnetHeaderAcrossTheOsakaAndBpoBoundaries_When_ItsBlobFieldsAreValidated_Then_TheyMatchTheBlockGethProduced(
            long childNumber)
        {
            var child = MainnetHeaders.Single(h => h.Number == childNumber);
            var parent = MainnetHeaders.Single(h => h.Number == childNumber - 1);
            var config = Mainnet().GetHardforkConfigAt(child.Number, (ulong)child.Timestamp);

            Assert.Equal((ulong)child.ExcessBlobGas, BlockExecutor.ExpectedExcessBlobGas(parent.ToHeader(), config));
            Assert.True(child.BlobGasUsed / BlobGasCalculator.GAS_PER_BLOB <= config.MaxBlobsPerBlock);
        }
    }
}
