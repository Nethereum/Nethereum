using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbBigEndianCodecTests : IDisposable
    {
        private const string Address = "0x1111111111111111111111111111111111111111";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bigendian_{Guid.NewGuid():N}");
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateDiffStore _diffs;

        public RocksDbBigEndianCodecTests()
        {
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _diffs = new RocksDbStateDiffStore(_manager);
        }

        public void Dispose()
        {
            _manager.Dispose();
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static BlockStateDiff DiffAt(BigInteger block, ulong balance) => new BlockStateDiff
        {
            BlockNumber = block,
            AccountDiffs = new List<AccountDiffEntry>
            {
                new AccountDiffEntry
                {
                    Address = Address,
                    PreValue = new Account { Balance = balance, Nonce = 1, CodeHash = new byte[32], StateRoot = new byte[32] }
                }
            }
        };

        public static IEnumerable<object[]> Boundaries() => new[]
        {
            new object[] { 0UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 } },
            new object[] { 1UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 } },
            new object[] { 255UL, new byte[] { 0, 0, 0, 0, 0, 0, 0, 255 } },
            new object[] { 256UL, new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 } },
            new object[] { 0x0102030405060708UL, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
            new object[] { 1UL << 63, new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 } },
            new object[] { ulong.MaxValue, new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 } }
        };

        [Theory]
        [MemberData(nameof(Boundaries))]
        public void Given_UlongMaxAndZeroAndBoundaryValues_When_Write64BEIsRead64BE_Then_RoundTripAndBytesAreBigEndian(
            ulong value, byte[] expectedBytes)
        {
            var bytes = RocksDbManager.Write64BE(value);

            Assert.Equal(expectedBytes, bytes);
            Assert.Equal(value, RocksDbManager.Read64BE(bytes));
        }

        [Fact]
        public async Task Given_ANegativeBlockNumber_When_AStateDiffIsDeletedAbove_Then_AnOverflowExceptionIsThrown()
        {
            await Assert.ThrowsAsync<OverflowException>(() => _diffs.DeleteDiffsAboveBlockAsync(new BigInteger(-5)));
        }

        [Fact]
        public async Task Given_AStateDiffAtBlockUlongMax_When_QueriedAndDeleted_Then_TheBigEndianIndexKeyOrdersAfterEveryLowerBlock()
        {
            var lowest = new BigInteger(5);
            var highest = new BigInteger(ulong.MaxValue);
            await _diffs.SaveBlockDiffAsync(DiffAt(lowest, 3));
            await _diffs.SaveBlockDiffAsync(DiffAt(highest, 7));

            Assert.Equal(highest, await _diffs.GetNewestDiffBlockAsync());
            Assert.Equal(lowest, await _diffs.GetOldestDiffBlockAsync());
            var (found, preValue) = await _diffs.GetFirstAccountPreValueAfterBlockAsync(Address, lowest);
            Assert.True(found);
            Assert.Equal(7, preValue.Balance);

            await _diffs.DeleteDiffsAboveBlockAsync(lowest);

            Assert.Equal(lowest, await _diffs.GetNewestDiffBlockAsync());
            Assert.NotNull(await _diffs.GetBlockDiffAsync(lowest));
            Assert.Empty((await _diffs.GetBlockDiffAsync(highest))?.AccountDiffs ?? new List<AccountDiffEntry>());
        }
    }
}
