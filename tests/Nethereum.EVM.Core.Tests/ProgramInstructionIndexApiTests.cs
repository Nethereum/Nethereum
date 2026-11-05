using System.Linq;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class ProgramInstructionIndexApiTests
    {
        private static readonly byte[] PushPushAddStop = { 0x60, 0x01, 0x60, 0x02, 0x01, 0x00 };

        [Fact]
        public void Given_a_new_program_When_the_instruction_list_is_read_Then_it_is_the_decoded_code_and_the_same_list_each_time()
        {
            var program = new Program(PushPushAddStop);

            Assert.Equal(new[] { 0, 2, 4, 5 }, program.Instructions.Select(i => i.Step).ToArray());
            Assert.Same(program.Instructions, program.Instructions);
            Assert.Equal(0, program.GetCurrentInstructionIndex());
            Assert.Equal(Instruction.PUSH1, program.GetCurrentInstruction().Instruction);
            Assert.Same(program.Instructions[0], program.GetCurrentInstruction());
        }

        [Fact]
        public void Given_step_counts_When_the_program_steps_Then_the_instruction_index_and_program_counter_follow_instructions_not_bytes()
        {
            var program = new Program(PushPushAddStop);

            program.Step();
            Assert.Equal(1, program.GetCurrentInstructionIndex());
            Assert.Equal(2, program.GetProgramCounter());

            program.Step(2);
            Assert.Equal(3, program.GetCurrentInstructionIndex());
            Assert.Equal(5, program.GetProgramCounter());
            Assert.Equal(Instruction.STOP, program.GetCurrentInstruction().Instruction);
            Assert.False(program.Stopped);
        }

        [Fact]
        public void Given_an_instruction_index_When_it_is_set_Then_the_program_counter_moves_there_and_an_index_past_the_end_stops_implicitly()
        {
            var program = new Program(PushPushAddStop);

            program.SetInstrunctionIndex(2);
            Assert.Equal(4, program.GetProgramCounter());
            Assert.Equal(Instruction.ADD, program.GetCurrentInstruction().Instruction);

            program.SetInstrunctionIndex(4);
            Assert.True(program.Stopped);
            Assert.True(program.StoppedImplicitly);
        }

        [Fact]
        public void Given_eip8024_decoding_applied_before_the_first_step_When_the_list_is_read_Then_the_immediate_is_swallowed_and_stepping_skips_it()
        {
            var program = new Program(new byte[] { 0xe6, 0x80, 0x00 });
            program.ApplyEip8024Decoding();

            Assert.Equal(new[] { 0, 2 }, program.Instructions.Select(i => i.Step).ToArray());
            program.Step();
            Assert.Equal(2, program.GetProgramCounter());
            Assert.Equal(1, program.GetCurrentInstructionIndex());
        }
    }
}
