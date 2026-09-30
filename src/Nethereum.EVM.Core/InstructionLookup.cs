using System;
using System.Collections.Generic;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Nethereum.EVM
{
    public static class InstructionLookup
    {
#if NET7_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Instruction enum values are statically known and preserved via DynamicDependency")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Instruction))]
#endif
        private static HashSet<int> CreateOpcodeSet()
        {
            var values = Enum.GetValues(typeof(Instruction));
            var set = new HashSet<int>();
            foreach (var val in values)
            {
                set.Add((int)val);
            }
            return set;
        }

        private static readonly HashSet<int> ValidOpcodes = CreateOpcodeSet();

        public static bool IsValid(byte opcode)
        {
            return ValidOpcodes.Contains(opcode);
        }
    }
}
