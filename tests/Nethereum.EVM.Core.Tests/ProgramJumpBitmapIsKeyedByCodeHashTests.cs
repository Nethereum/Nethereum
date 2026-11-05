using System;
using System.Linq;
using Nethereum.EVM.Exceptions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class ProgramJumpBitmapIsKeyedByCodeHashTests
    {
        private static readonly byte[] PlainJumpDestinationAtZero = { 0x5b, 0x00 };
        private static readonly byte[] JumpDestinationInsidePushData = { 0x60, 0x5b, 0x5b };

        private static byte[] UniqueHash()
        {
            return Sha3Keccack.Current.CalculateHash(Guid.NewGuid().ToByteArray());
        }

        private static bool JumpIsAccepted(Program program, int destination)
        {
            try
            {
                program.GoToJumpDestination(destination);
            }
            catch (EvmHaltException)
            {
                return false;
            }
            return program.HaltReason == EvmHaltReason.None;
        }

        [Fact]
        public void Given_code_that_pushes_a_jumpdest_byte_When_a_frame_jumps_into_the_push_data_Then_the_jump_is_rejected()
        {
            Assert.False(JumpIsAccepted(new Program(JumpDestinationInsidePushData, UniqueHash()), 1));
            Assert.True(JumpIsAccepted(new Program(JumpDestinationInsidePushData, UniqueHash()), 2));
        }

        [Fact]
        public void Given_a_frame_that_already_jumped_under_a_code_hash_When_another_frame_jumps_under_the_same_hash_Then_the_cached_bitmap_is_reused()
        {
            var hash = UniqueHash();
            Assert.True(JumpIsAccepted(new Program(PlainJumpDestinationAtZero, hash), 0));

            Assert.True(JumpIsAccepted(new Program(JumpDestinationInsidePushData, hash), 1));
        }

        [Fact]
        public void Given_two_frames_of_the_same_code_When_both_jump_Then_both_agree_with_the_analysis()
        {
            var code = (byte[])JumpDestinationInsidePushData.Clone();
            var hash = Sha3Keccack.Current.CalculateHash(code);

            Assert.False(JumpIsAccepted(new Program(code, hash), 1));
            Assert.False(JumpIsAccepted(new Program((byte[])code.Clone(), hash), 1));
            Assert.False(JumpIsAccepted(new Program((byte[])code.Clone()), 1));
            Assert.True(JumpIsAccepted(new Program((byte[])code.Clone()), 2));
        }

        [Fact]
        public void Given_a_bitmap_cached_by_an_eip8024_frame_When_a_standard_frame_jumps_under_the_same_hash_Then_the_one_bitmap_serves_both()
        {
            var hash = UniqueHash();
            var eip8024Frame = new Program(PlainJumpDestinationAtZero, hash);
            eip8024Frame.ApplyEip8024Decoding();
            Assert.True(JumpIsAccepted(eip8024Frame, 0));

            Assert.True(JumpIsAccepted(new Program(JumpDestinationInsidePushData, hash), 1));
        }

        [Fact]
        public void Given_a_bitmap_cached_under_a_hash_When_a_frame_reads_its_instructions_Then_the_list_is_built_from_that_frames_own_code()
        {
            var hash = UniqueHash();
            Assert.True(JumpIsAccepted(new Program(PlainJumpDestinationAtZero, hash), 0));

            var steps = new Program(JumpDestinationInsidePushData, hash).Instructions.Select(i => i.Step).ToArray();

            Assert.Equal(new[] { 0, 2 }, steps);
        }

        [Fact]
        public void Given_init_code_When_it_jumps_Then_its_bitmap_is_not_published_under_any_code_hash()
        {
            var initCode = new byte[] { 0x5b }.Concat(Guid.NewGuid().ToByteArray().Select(b => (byte)((b & 0x0f) + 1))).ToArray();
            var hashOfInitCode = Sha3Keccack.Current.CalculateHash(initCode);
            Assert.True(JumpIsAccepted(Program.ForInitCode(initCode), 0));

            Assert.False(JumpIsAccepted(new Program(JumpDestinationInsidePushData, hashOfInitCode), 1));
        }

        [Fact]
        public void Given_init_code_with_a_jumpdest_inside_push_data_When_it_jumps_Then_the_analysis_is_the_same_as_for_deployed_code()
        {
            Assert.False(JumpIsAccepted(Program.ForInitCode((byte[])JumpDestinationInsidePushData.Clone()), 1));
            Assert.True(JumpIsAccepted(Program.ForInitCode((byte[])JumpDestinationInsidePushData.Clone()), 2));
        }
    }
}
