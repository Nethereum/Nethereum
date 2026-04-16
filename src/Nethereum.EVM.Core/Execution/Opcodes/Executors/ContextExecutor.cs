namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class ContextExecutor : IOpcodeExecutor
    {
        private readonly EvmCallInputExecution _callInput;
        private readonly EvmCallInputDataExecution _callData;
        private readonly EvmCodeExecution _code;
        private readonly EvmBlockchainCurrentContractContextExecution _context;
        private readonly bool _hasChainId;
        private readonly bool _hasBaseFee;
        private readonly bool _hasBlobOps;

        public ContextExecutor(
            EvmCallInputExecution callInput,
            EvmCallInputDataExecution callData,
            EvmCodeExecution code,
            EvmBlockchainCurrentContractContextExecution context,
            bool hasChainId = true,
            bool hasBaseFee = true,
            bool hasBlobOps = true)
        {
            _callInput = callInput;
            _callData = callData;
            _code = code;
            _context = context;
            _hasChainId = hasChainId;
            _hasBaseFee = hasBaseFee;
            _hasBlobOps = hasBlobOps;
        }

        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.ADDRESS:
                    _context.Address(program);
                    return true;
                case Instruction.ORIGIN:
                    _callInput.Origin(program);
                    return true;
                case Instruction.CALLER:
                    _callInput.Caller(program);
                    return true;
                case Instruction.CALLVALUE:
                    _callInput.CallValue(program);
                    return true;
                case Instruction.CALLDATALOAD:
                    _callData.CallDataLoad(program);
                    return true;
                case Instruction.CALLDATASIZE:
                    _callData.CallDataSize(program);
                    return true;
                case Instruction.CALLDATACOPY:
                    _callData.CallDataCopy(program);
                    return true;
                case Instruction.CODESIZE:
                    _code.CodeSize(program);
                    return true;
                case Instruction.CODECOPY:
                    _code.CodeCopy(program);
                    return true;
                case Instruction.KECCAK256:
                    _context.SHA3(program);
                    return true;
                case Instruction.COINBASE:
                    _context.Coinbase(program);
                    return true;
                case Instruction.TIMESTAMP:
                    _context.TimeStamp(program);
                    return true;
                case Instruction.NUMBER:
                    _context.BlockNumber(program);
                    return true;
                case Instruction.DIFFICULTY:
                    _context.Difficulty(program);
                    return true;
                case Instruction.GASLIMIT:
                    _context.GasLimit(program);
                    return true;
                case Instruction.GAS:
                    _context.Gas(program);
                    return true;
                case Instruction.GASPRICE:
                    _context.GasPrice(program);
                    return true;
                case Instruction.CHAINID:
                    if (!_hasChainId) return false;
                    _context.ChainId(program);
                    return true;
                case Instruction.BASEFEE:
                    if (!_hasBaseFee) return false;
                    _context.BaseFee(program);
                    return true;
                case Instruction.BLOBBASEFEE:
                    if (!_hasBlobOps) return false;
                    _context.BlobBaseFee(program);
                    return true;
                case Instruction.BLOBHASH:
                    if (!_hasBlobOps) return false;
                    _context.BlobHash(program);
                    return true;
                default:
                    return false;
            }
        }
    }
}
