namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-214: "Any attempts to make state-changing operations inside an execution
    /// instance with <c>STATIC</c> set to <c>true</c> will instead throw an exception.
    /// These operations include <c>CREATE</c>, <c>CREATE2</c>, <c>LOG0</c>, <c>LOG1</c>,
    /// <c>LOG2</c>, <c>LOG3</c>, <c>LOG4</c>, <c>SSTORE</c>, and <c>SELFDESTRUCT</c>.
    /// They also include <c>CALL</c> with a non-zero value."
    ///
    /// <para>EIP-214: "As an exception, <c>CALLCODE</c> is not considered state-changing,
    /// even with a non-zero value."</para>
    ///
    /// <para>Asked before the opcode is priced rather than inside each opcode's executor;
    /// <c>EVMSimulator.RefusedBeforePricing</c> carries the rule that requires that. For
    /// SSTORE and SELFDESTRUCT the reference also refuses ahead of its gas section (EELS
    /// <c>forks/amsterdam/vm/instructions/storage.py:78</c>,
    /// <c>vm/instructions/system.py:493,725</c>); for LOG0-4 it charges gas first and
    /// refuses after (<c>vm/instructions/log.py:72</c>). Answering early is unobservable
    /// there, because LOG pricing reads no state and either order is an exceptional halt
    /// that forfeits the frame's gas.</para>
    /// </summary>
    public static class StaticContextWriteProtection
    {
        private const int CallValueStackOffset = 2;

        public static bool Forbids(Program program, Instruction opcode)
        {
            if (program.ProgramContext?.IsStatic != true) return false;

            switch (opcode)
            {
                case Instruction.CREATE:
                case Instruction.CREATE2:
                case Instruction.LOG0:
                case Instruction.LOG1:
                case Instruction.LOG2:
                case Instruction.LOG3:
                case Instruction.LOG4:
                case Instruction.SSTORE:
                case Instruction.SELFDESTRUCT:
                    return true;
                case Instruction.CALL:
                    return !program.StackPeekAtU256(CallValueStackOffset).IsZero;
                default:
                    return false;
            }
        }
    }
}
