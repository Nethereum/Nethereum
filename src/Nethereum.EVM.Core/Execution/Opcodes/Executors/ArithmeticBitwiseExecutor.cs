namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class ArithmeticBitwiseExecutor : IOpcodeExecutor
    {
        private readonly EvmArithmeticExecution _arithmetic;
        private readonly EvmBitwiseExecution _bitwise;
        private readonly bool _hasShifts;
        private readonly bool _hasClz;

        public ArithmeticBitwiseExecutor(
            EvmArithmeticExecution arithmetic,
            EvmBitwiseExecution bitwise,
            bool hasShifts = true,
            bool hasClz = false)
        {
            _arithmetic = arithmetic;
            _bitwise = bitwise;
            _hasShifts = hasShifts;
            _hasClz = hasClz;
        }

        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.ADD:
                    _arithmetic.Add(program);
                    return true;
                case Instruction.MUL:
                    _arithmetic.Mul(program);
                    return true;
                case Instruction.SUB:
                    _arithmetic.Sub(program);
                    return true;
                case Instruction.DIV:
                    _arithmetic.Div(program);
                    return true;
                case Instruction.SDIV:
                    _arithmetic.SDiv(program);
                    return true;
                case Instruction.MOD:
                    _arithmetic.Mod(program);
                    return true;
                case Instruction.SMOD:
                    _arithmetic.SMod(program);
                    return true;
                case Instruction.ADDMOD:
                    _arithmetic.AddMod(program);
                    return true;
                case Instruction.MULMOD:
                    _arithmetic.MulMod(program);
                    return true;
                case Instruction.EXP:
                    _arithmetic.Exp(program);
                    return true;
                case Instruction.SIGNEXTEND:
                    _bitwise.SignExtend(program);
                    return true;
                case Instruction.LT:
                    _bitwise.LT(program);
                    return true;
                case Instruction.GT:
                    _bitwise.GT(program);
                    return true;
                case Instruction.SLT:
                    _bitwise.SLT(program);
                    return true;
                case Instruction.SGT:
                    _bitwise.SGT(program);
                    return true;
                case Instruction.EQ:
                    _bitwise.EQ(program);
                    return true;
                case Instruction.ISZERO:
                    _bitwise.IsZero(program);
                    return true;
                case Instruction.AND:
                    _bitwise.And(program);
                    return true;
                case Instruction.OR:
                    _bitwise.Or(program);
                    return true;
                case Instruction.XOR:
                    _bitwise.Xor(program);
                    return true;
                case Instruction.NOT:
                    _bitwise.Not(program);
                    return true;
                case Instruction.BYTE:
                    _bitwise.Byte(program);
                    return true;
                case Instruction.SHL:
                    if (!_hasShifts) return false;
                    _bitwise.ShiftLeft(program);
                    return true;
                case Instruction.SHR:
                    if (!_hasShifts) return false;
                    _bitwise.ShiftRight(program);
                    return true;
                case Instruction.SAR:
                    if (!_hasShifts) return false;
                    _bitwise.ShiftSignedRight(program);
                    return true;
                case Instruction.CLZ:
                    if (!_hasClz) return false;
                    _bitwise.CountLeadingZeros(program);
                    return true;
                default:
                    return false;
            }
        }
    }
}
