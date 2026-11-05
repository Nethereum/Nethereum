using Nethereum.EVM.Execution.Opcodes;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution
{
    public class OpcodeHandlerTableFrameOpeningTests
    {
        public static TheoryData<string> Forks => new TheoryData<string>
        {
            "Frontier", "Homestead", "TangerineWhistle", "SpuriousDragon", "Byzantium", "Constantinople",
            "Petersburg", "Istanbul", "Berlin", "London", "Shanghai", "Cancun", "Osaka", "Amsterdam"
        };

        private static OpcodeHandlerTable Table(string fork)
        {
            switch (fork)
            {
                case "Frontier": return OpcodeHandlerSets.Frontier;
                case "Homestead": return OpcodeHandlerSets.Homestead;
                case "TangerineWhistle": return OpcodeHandlerSets.TangerineWhistle;
                case "SpuriousDragon": return OpcodeHandlerSets.SpuriousDragon;
                case "Byzantium": return OpcodeHandlerSets.Byzantium;
                case "Constantinople": return OpcodeHandlerSets.Constantinople;
                case "Petersburg": return OpcodeHandlerSets.Petersburg;
                case "Istanbul": return OpcodeHandlerSets.Istanbul;
                case "Berlin": return OpcodeHandlerSets.Berlin;
                case "London": return OpcodeHandlerSets.London;
                case "Shanghai": return OpcodeHandlerSets.Shanghai;
                case "Cancun": return OpcodeHandlerSets.Cancun;
                case "Osaka": return OpcodeHandlerSets.Osaka;
                default: return OpcodeHandlerSets.Amsterdam;
            }
        }

        [Theory]
        [MemberData(nameof(Forks))]
        public void Given_AnyForkTable_When_ItsFrameOpeningOpcodesAreInspected_Then_NoneHasAnExecutor(string fork)
        {
            var table = Table(fork);

            Assert.False(table.HasExecutor(Instruction.CALL));
            Assert.False(table.HasExecutor(Instruction.CALLCODE));
            Assert.False(table.HasExecutor(Instruction.DELEGATECALL));
            Assert.False(table.HasExecutor(Instruction.STATICCALL));
            Assert.False(table.HasExecutor(Instruction.CREATE));
            Assert.False(table.HasExecutor(Instruction.CREATE2));
        }
    }
}
