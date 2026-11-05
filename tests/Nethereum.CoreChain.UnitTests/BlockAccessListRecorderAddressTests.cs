using System.Linq;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListRecorderAddressTests
    {
        private static readonly EvmAddress A = EvmAddress.FromHex("0x00000000000000000000000000000000000000aa");
        private static readonly EvmAddress B = EvmAddress.FromHex("0x00000000000000000000000000000000000000bb");

        [Fact]
        public void Given_reads_recorded_by_EvmAddress_with_repeats_When_the_unit_is_flushed_Then_each_account_and_slot_appears_once()
        {
            var recorder = new BlockAccessListRecorder(new InMemoryStateStore());
            recorder.BeginUnit(1);
            recorder.RecordAccountRead(B);
            recorder.RecordAccountRead(A);
            recorder.RecordAccountRead(B);
            recorder.RecordStorageRead(A, new EvmUInt256(3));
            recorder.RecordStorageRead(A, new EvmUInt256(3));
            recorder.RecordStorageRead(A, new EvmUInt256(2));

            recorder.FlushPendingReads();
            var list = recorder.Build();

            Assert.Equal(new[] { A.ToHexLower(), B.ToHexLower() }, list.Select(a => a.Address).ToArray());
            var a = list.Single(x => x.Address == A.ToHexLower());
            Assert.Equal(2, a.StorageReads.Count);
        }

        [Fact]
        public void Given_the_same_reads_recorded_as_hex_strings_When_the_unit_is_flushed_Then_the_built_list_is_identical_to_the_EvmAddress_route()
        {
            var byAddress = new BlockAccessListRecorder(new InMemoryStateStore());
            byAddress.BeginUnit(1);
            byAddress.RecordAccountRead(A);
            byAddress.RecordStorageRead(B, new EvmUInt256(9));
            byAddress.FlushPendingReads();

            var byString = new BlockAccessListRecorder(new InMemoryStateStore());
            byString.BeginUnit(1);
            byString.RecordAccountRead(A.ToHexLower());
            byString.RecordStorageRead(B.ToHexLower(), new EvmUInt256(9));
            byString.FlushPendingReads();

            var expected = byString.Build();
            var actual = byAddress.Build();
            Assert.Equal(expected.Select(x => x.Address), actual.Select(x => x.Address));
            Assert.Equal(expected.Sum(x => x.StorageReads.Count), actual.Sum(x => x.StorageReads.Count));
        }

        [Fact]
        public void Given_pending_reads_When_the_unit_is_discarded_Then_a_later_flush_records_only_what_was_read_after()
        {
            var recorder = new BlockAccessListRecorder(new InMemoryStateStore());
            recorder.BeginUnit(1);
            recorder.RecordAccountRead(A);
            recorder.DiscardUnit();
            recorder.RecordAccountRead(B);

            recorder.FlushPendingReads();

            Assert.Equal(new[] { B.ToHexLower() }, recorder.Build().Select(a => a.Address).ToArray());
        }
    }
}
