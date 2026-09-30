using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Types;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class AmsterdamIsNotPragueTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xabcdef0123456789abcdef0123456789abcdef01";

        [Fact]
        [Trait("Category", "Amsterdam")]
        public void Given_AmsterdamAndPrague_When_SstoreIsPricedForASlotTheyDisagreeAbout_Then_TheChargeDiffers()
        {
            Assert.NotEqual(
                PriceASstoreThatFillsAColdSlotFromZero(HardforkConfig.Prague.OpcodeHandlers),
                PriceASstoreThatFillsAColdSlotFromZero(HardforkConfig.Amsterdam.OpcodeHandlers));
        }

        [Fact]
        [Trait("Category", "Amsterdam")]
        public void Given_PragueRules_When_AmsterdamBytecodeRuns_Then_TheNewOpcodesAreUndispatchable()
        {
            var prague = HardforkConfig.Prague.OpcodeHandlers;
            var amsterdam = HardforkConfig.Amsterdam.OpcodeHandlers;

            foreach (var op in new[] { Instruction.DUPN, Instruction.SWAPN, Instruction.EXCHANGE, Instruction.SLOTNUM })
            {
                Assert.False(prague.IsRegistered(op), $"{op} must not exist before Amsterdam");
                Assert.True(amsterdam.IsRegistered(op), $"{op} must exist at Amsterdam");
            }
        }

        private static long PriceASstoreThatFillsAColdSlotFromZero(OpcodeHandlerTable handlers)
        {
            var program = ProgramWithAnEmptyContract();
            program.StackPush(Word(1));
            program.StackPush(Word(7));
            return handlers.GetGasCost(Instruction.SSTORE, program);
        }

        private static Program ProgramWithAnEmptyContract()
        {
            var stateService = new ExecutionStateService(
                new InMemoryStateReader(new Dictionary<string, AccountState>()));

            var callContext = new EvmCallContext
            {
                From = CallerAddress,
                To = ContractAddress,
                Data = new byte[0],
                Gas = 1_000_000
            };

            var program = new Program(new byte[] { 0x00 }, new ProgramContext(callContext, stateService));
            program.GasRemaining = 1_000_000;
            return program;
        }

        private static byte[] Word(byte value)
        {
            var word = new byte[32];
            word[31] = value;
            return word;
        }
    }
}
