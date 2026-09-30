using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class OpcodeRegistrationForkWiringTests
    {
        private static readonly Instruction[] FrontierOpcodes = Union(
            new[]
            {
                Instruction.STOP, Instruction.ADD, Instruction.MUL, Instruction.SUB,
                Instruction.DIV, Instruction.SDIV, Instruction.MOD, Instruction.SMOD,
                Instruction.ADDMOD, Instruction.MULMOD, Instruction.EXP, Instruction.SIGNEXTEND,
                Instruction.LT, Instruction.GT, Instruction.SLT, Instruction.SGT,
                Instruction.EQ, Instruction.ISZERO, Instruction.AND, Instruction.OR,
                Instruction.XOR, Instruction.NOT, Instruction.BYTE,
                Instruction.KECCAK256,
                Instruction.ADDRESS, Instruction.BALANCE, Instruction.ORIGIN, Instruction.CALLER,
                Instruction.CALLVALUE, Instruction.CALLDATALOAD, Instruction.CALLDATASIZE,
                Instruction.CALLDATACOPY, Instruction.CODESIZE, Instruction.CODECOPY,
                Instruction.GASPRICE, Instruction.EXTCODESIZE, Instruction.EXTCODECOPY,
                Instruction.BLOCKHASH, Instruction.COINBASE, Instruction.TIMESTAMP,
                Instruction.NUMBER, Instruction.DIFFICULTY, Instruction.GASLIMIT,
                Instruction.POP, Instruction.MLOAD, Instruction.MSTORE, Instruction.MSTORE8,
                Instruction.SLOAD, Instruction.SSTORE, Instruction.JUMP, Instruction.JUMPI,
                Instruction.PC, Instruction.MSIZE, Instruction.GAS, Instruction.JUMPDEST,
                Instruction.CREATE, Instruction.CALL, Instruction.CALLCODE, Instruction.RETURN,
                Instruction.INVALID, Instruction.SELFDESTRUCT
            },
            Through(Instruction.PUSH1, Instruction.PUSH32),
            Through(Instruction.DUP1, Instruction.DUP16),
            Through(Instruction.SWAP1, Instruction.SWAP16),
            Through(Instruction.LOG0, Instruction.LOG4));

        private static readonly Instruction[] HomesteadOpcodes =
            Plus(FrontierOpcodes, Instruction.DELEGATECALL);

        private static readonly Instruction[] ByzantiumOpcodes = Plus(HomesteadOpcodes,
            Instruction.RETURNDATASIZE, Instruction.RETURNDATACOPY,
            Instruction.STATICCALL, Instruction.REVERT);

        private static readonly Instruction[] ConstantinopleOpcodes = Plus(ByzantiumOpcodes,
            Instruction.SHL, Instruction.SHR, Instruction.SAR,
            Instruction.CREATE2, Instruction.EXTCODEHASH);

        private static readonly Instruction[] IstanbulOpcodes = Plus(ConstantinopleOpcodes,
            Instruction.CHAINID, Instruction.SELFBALANCE);

        private static readonly Instruction[] LondonOpcodes =
            Plus(IstanbulOpcodes, Instruction.BASEFEE);

        private static readonly Instruction[] ShanghaiOpcodes =
            Plus(LondonOpcodes, Instruction.PUSH0);

        private static readonly Instruction[] CancunOpcodes = Plus(ShanghaiOpcodes,
            Instruction.TLOAD, Instruction.TSTORE, Instruction.MCOPY,
            Instruction.BLOBHASH, Instruction.BLOBBASEFEE);

        private static readonly Instruction[] OsakaOpcodes =
            Plus(CancunOpcodes, Instruction.CLZ);

        private static readonly Instruction[] AmsterdamOpcodes = Plus(OsakaOpcodes,
            Instruction.DUPN, Instruction.SWAPN, Instruction.EXCHANGE, Instruction.SLOTNUM);

        private static readonly Dictionary<HardforkName, Instruction[]> ExpectedOpcodesByFork =
            new Dictionary<HardforkName, Instruction[]>
            {
                [HardforkName.Frontier] = FrontierOpcodes,
                [HardforkName.Homestead] = HomesteadOpcodes,
                [HardforkName.TangerineWhistle] = HomesteadOpcodes,
                [HardforkName.SpuriousDragon] = HomesteadOpcodes,
                [HardforkName.Byzantium] = ByzantiumOpcodes,
                [HardforkName.Constantinople] = ConstantinopleOpcodes,
                [HardforkName.Petersburg] = ConstantinopleOpcodes,
                [HardforkName.Istanbul] = IstanbulOpcodes,
                [HardforkName.Berlin] = IstanbulOpcodes,
                [HardforkName.London] = LondonOpcodes,
                [HardforkName.Paris] = LondonOpcodes,
                [HardforkName.Shanghai] = ShanghaiOpcodes,
                [HardforkName.Cancun] = CancunOpcodes,
                [HardforkName.Prague] = CancunOpcodes,
                [HardforkName.Osaka] = OsakaOpcodes,
                [HardforkName.OsakaBpo1] = OsakaOpcodes,
                [HardforkName.OsakaBpo2] = OsakaOpcodes,
                [HardforkName.Amsterdam] = AmsterdamOpcodes,
            };

        [Fact]
        public void Given_EveryForkTable_When_ItsRegisteredOpcodeSetIsListed_Then_ItMatchesTheDeclaredSetForThatFork()
        {
            Assert.True(HardforkSpecRegistry.All.Length > 0,
                "HardforkSpecRegistry.All is empty — no fork tables were inspected.");

            var mismatches = FindMismatches(HardforkSpecRegistry.All, ExpectedOpcodesByFork);

            Assert.True(mismatches.Count == 0,
                "Registered-opcode sets drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }

        [Fact]
        public void Given_AForkWithNoDeclaredOpcodeSet_When_TheGateRuns_Then_ItFailsNamingTheFork()
        {
            var mismatches = FindMismatches(
                HardforkSpecRegistry.All,
                new Dictionary<HardforkName, Instruction[]>());

            Assert.Equal(HardforkSpecRegistry.All.Length, mismatches.Count);
            Assert.All(HardforkSpecRegistry.All,
                spec => Assert.Contains(mismatches, m => m.StartsWith(spec.Name + ":")));
        }

        private static List<string> FindMismatches(
            IEnumerable<HardforkSpec> specs,
            IReadOnlyDictionary<HardforkName, Instruction[]> expectedByFork)
        {
            var mismatches = new List<string>();

            foreach (var spec in specs)
            {
                if (!expectedByFork.TryGetValue(spec.Name, out var expected))
                {
                    mismatches.Add(
                        $"{spec.Name}: no expected registered-opcode set declared in this test's " +
                        "expectation map — add an entry naming the set this fork should register.");
                    continue;
                }

                var declared = new HashSet<Instruction>(expected);
                var registered = new HashSet<Instruction>(spec.Opcodes.GetRegisteredOpcodes());

                var unregistered = declared.Where(op => !registered.Contains(op)).ToList();
                if (unregistered.Count > 0)
                    mismatches.Add($"{spec.Name}: declared but not registered — {Describe(unregistered)}");

                var undeclared = registered.Where(op => !declared.Contains(op)).ToList();
                if (undeclared.Count > 0)
                    mismatches.Add($"{spec.Name}: registered but not declared — {Describe(undeclared)}");
            }

            return mismatches;
        }

        private static string Describe(IEnumerable<Instruction> opcodes) =>
            string.Join(", ", opcodes.OrderBy(op => (int)op).Select(op => op.ToString()));

        private static IEnumerable<Instruction> Through(Instruction first, Instruction last)
        {
            for (var op = (int)first; op <= (int)last; op++)
                yield return (Instruction)op;
        }

        private static Instruction[] Union(params IEnumerable<Instruction>[] parts) =>
            parts.SelectMany(part => part).Distinct().ToArray();

        private static Instruction[] Plus(Instruction[] inherited, params Instruction[] introduced) =>
            inherited.Concat(introduced).Distinct().ToArray();
    }
}
