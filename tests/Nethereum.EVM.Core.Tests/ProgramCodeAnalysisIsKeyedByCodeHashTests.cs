using System;
using System.Linq;
using Nethereum.EVM.SourceInfo;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class ProgramCodeAnalysisIsKeyedByCodeHashTests
    {
        private const long BuildingAnAnalysisAllocatesAtLeast = 100_000;
        private const long ReusingAnAnalysisAllocatesLessThan = 16_000;

        private static byte[] CodeUniqueTo(int seed, params byte[] tail)
        {
            var seedBytes = BitConverter.GetBytes(seed);
            return new byte[] { 0x63, seedBytes[0], seedBytes[1], seedBytes[2], seedBytes[3], 0x50, 0x5b }.Concat(tail).ToArray();
        }

        private static byte[] LargeCodeUniqueTo(int seed)
        {
            var filler = Enumerable.Repeat(new byte[] { 0x60, 0x01 }, 12_000).SelectMany(pair => pair).ToArray();
            return CodeUniqueTo(seed, filler);
        }

        private static long AllocatedByAnalysing(Program program)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            program.GetCurrentInstructionIndex();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static Program WarmedUp()
        {
            var program = new Program(LargeCodeUniqueTo(0x5000));
            program.GetCurrentInstructionIndex();
            return program;
        }

        [Fact]
        public void Given_TwoDistinctArraysWithTheSameBytes_When_ProgramsAreBuilt_Then_TheyShareOneAnalysis()
        {
            WarmedUp();
            var first = LargeCodeUniqueTo(0x51);
            var second = (byte[])first.Clone();

            Assert.True(AllocatedByAnalysing(new Program(first)) >= BuildingAnAnalysisAllocatesAtLeast);
            Assert.True(AllocatedByAnalysing(new Program(second)) < ReusingAnAnalysisAllocatesLessThan);
        }

        [Fact]
        public void Given_TwoDifferentContracts_When_ProgramsAreBuilt_Then_TheyDoNotShareAnalysis()
        {
            WarmedUp();

            Assert.True(AllocatedByAnalysing(new Program(LargeCodeUniqueTo(0x52))) >= BuildingAnAnalysisAllocatesAtLeast);
            Assert.True(AllocatedByAnalysing(new Program(LargeCodeUniqueTo(0x53))) >= BuildingAnAnalysisAllocatesAtLeast);
        }

        [Fact]
        public void Given_AKnownCodeHash_When_AFreshArrayIsSuppliedWithIt_Then_TheCachedAnalysisIsReused()
        {
            WarmedUp();
            var code = LargeCodeUniqueTo(0x54);
            var hash = Sha3Keccack.Current.CalculateHash(code);

            Assert.True(AllocatedByAnalysing(new Program((byte[])code.Clone(), hash)) >= BuildingAnAnalysisAllocatesAtLeast);
            Assert.True(AllocatedByAnalysing(new Program((byte[])code.Clone(), hash)) < ReusingAnAnalysisAllocatesLessThan);
            Assert.True(AllocatedByAnalysing(new Program((byte[])code.Clone())) < ReusingAnAnalysisAllocatesLessThan);
        }

        [Fact]
        public void Given_ACodeHashThatIsNotThirtyTwoBytes_When_AProgramIsBuilt_Then_ItIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new Program(CodeUniqueTo(0x55), new byte[20]));
        }

        [Fact]
        public void Given_Eip8024OnAndOff_When_TheSameCodeIsAnalysed_Then_TheTwoVariantsStaySeparate()
        {
            WarmedUp();
            var code = LargeCodeUniqueTo(0x56);
            Assert.True(AllocatedByAnalysing(new Program(code)) >= BuildingAnAnalysisAllocatesAtLeast);

            var other = new Program((byte[])code.Clone());
            other.ApplyEip8024Decoding();
            Assert.True(AllocatedByAnalysing(other) >= BuildingAnAnalysisAllocatesAtLeast);

            var again = new Program((byte[])code.Clone());
            again.ApplyEip8024Decoding();
            Assert.True(AllocatedByAnalysing(again) < ReusingAnAnalysisAllocatesLessThan);
            Assert.True(AllocatedByAnalysing(new Program((byte[])code.Clone())) < ReusingAnAnalysisAllocatesLessThan);
        }

        [Fact]
        public void Given_MoreCodeThanTheByteBudgetHolds_When_ProgramsAreBuilt_Then_TheLeastRecentlyUsedIsEvicted()
        {
            WarmedUp();
            var oldest = LargeCodeUniqueTo(0x1000);
            AllocatedByAnalysing(new Program(oldest));

            for (var i = 0; i < MoreContractsThanTheCacheHolds; i++)
                AllocatedByAnalysing(new Program(LargeCodeUniqueTo(0x2000 + i)));

            Assert.True(AllocatedByAnalysing(new Program((byte[])oldest.Clone())) >= BuildingAnAnalysisAllocatesAtLeast);
        }

        [Fact]
        public void Given_AContractKeptInUse_When_TheBoundIsExceeded_Then_ItSurvivesEviction()
        {
            WarmedUp();
            var hot = LargeCodeUniqueTo(0x3000);
            AllocatedByAnalysing(new Program(hot));

            for (var i = 0; i < MoreContractsThanTheCacheHolds; i++)
            {
                AllocatedByAnalysing(new Program(LargeCodeUniqueTo(0x4000 + i)));
                AllocatedByAnalysing(new Program((byte[])hot.Clone()));
            }

            Assert.True(AllocatedByAnalysing(new Program((byte[])hot.Clone())) < ReusingAnAnalysisAllocatesLessThan);
        }

        [Fact]
        public void Given_ProgramsBuiltFromManyThreads_When_TheSameContractIsAnalysed_Then_TheyEndUpSharingOneAnalysis()
        {
            WarmedUp();
            var code = LargeCodeUniqueTo(0x57);
            Enumerable.Range(0, 64).AsParallel().ForAll(_ => new Program((byte[])code.Clone()).GetCurrentInstructionIndex());

            Assert.True(AllocatedByAnalysing(new Program((byte[])code.Clone())) < ReusingAnAnalysisAllocatesLessThan);
        }

        [Fact]
        public void Given_two_programs_with_the_same_code_When_one_assigns_source_maps_Then_the_other_sees_none()
        {
            var code = CodeUniqueTo(0x58);
            var assigning = new Program(code);
            var untouched = new Program((byte[])code.Clone());

            foreach (var instruction in assigning.Instructions) instruction.SourceMap = new SourceMap { Position = 7, Length = 3 };

            Assert.All(assigning.Instructions, instruction => Assert.NotNull(instruction.SourceMap));
            Assert.All(untouched.Instructions, instruction => Assert.Null(instruction.SourceMap));
            Assert.All(new Program((byte[])code.Clone()).Instructions, instruction => Assert.Null(instruction.SourceMap));
        }

        [Fact]
        public void Given_two_programs_with_the_same_code_When_one_changes_an_instruction_argument_Then_the_other_keeps_the_decoded_bytes()
        {
            var code = CodeUniqueTo(0x59);
            var changing = new Program(code);
            var untouched = new Program((byte[])code.Clone());

            changing.Instructions[0].Arguments[0] = 0xff;

            Assert.NotEqual(0xff, untouched.Instructions[0].Arguments[0]);
        }

        private const int MoreContractsThanTheCacheHolds = 150;
    }
}
