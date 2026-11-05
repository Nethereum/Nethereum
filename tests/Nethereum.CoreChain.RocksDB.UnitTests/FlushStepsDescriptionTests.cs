using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlushStepsDescriptionTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"flushsteps_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        [Fact]
        public async Task Given_ARocksDbBundle_When_TwoBlocksAreFlushed_Then_TheDescriptionNamesAwaitStageAndPreviousWrite()
        {
            var options = new RocksDbStorageOptions { PathKeyedState = true };
            using var bundle = RocksDbChainStoreBundle.Open(DataDir(_root), null, false, options, FreezerBlockFactory.Signer);
            var flush = (IAtomicBlockFlush)bundle;

            await flush.FlushBlockAsync(null, 1, Fill32(1)).WaitAsync(TimeSpan.FromSeconds(30));
            await flush.FlushBlockAsync(null, 2, Fill32(2)).WaitAsync(TimeSpan.FromSeconds(30));
            await flush.DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Matches(new Regex(@"^await_prev_ms=\d+ stage_ms=\d+ prev_write_ms=\d+ prune_ms=\d+$"), flush.DescribeLastFlush());
        }
    }
}
