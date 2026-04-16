namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class LogReturnExecutor : IOpcodeExecutor
    {
        private readonly EvmReturnRevertLogExecution _returnRevertLog;
        private readonly bool _hasRevert;
        private readonly bool _hasReturnData;

        public LogReturnExecutor(
            EvmReturnRevertLogExecution returnRevertLog,
            bool hasRevert = true,
            bool hasReturnData = true)
        {
            _returnRevertLog = returnRevertLog;
            _hasRevert = hasRevert;
            _hasReturnData = hasReturnData;
        }

        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.RETURN:
                    _returnRevertLog.Return(program);
                    return true;
                case Instruction.REVERT:
                    if (!_hasRevert) return false;
                    _returnRevertLog.Revert(program);
                    return true;
                case Instruction.RETURNDATASIZE:
                    if (!_hasReturnData) return false;
                    _returnRevertLog.ReturnDataSize(program);
                    return true;
                case Instruction.RETURNDATACOPY:
                    if (!_hasReturnData) return false;
                    _returnRevertLog.ReturnDataCopy(program);
                    return true;
                case Instruction.LOG0:
                case Instruction.LOG1:
                case Instruction.LOG2:
                case Instruction.LOG3:
                case Instruction.LOG4:
                    _returnRevertLog.Log(program);
                    return true;
                default:
                    return false;
            }
        }
    }
}
