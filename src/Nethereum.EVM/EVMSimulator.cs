using Nethereum.EVM.Execution;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    /// <summary>
    /// EVM simulator 
    /// Experimental needs more testing
    /// Gas calculation not implemented (this might be a future release)
    /// </summary>
    public class EVMSimulator
    {
#if DEBUG
        public EVMSimulator(EvmProgramExecution evmProgramExecution, bool enableTraceToDebugOuptput = false) :this(evmProgramExecution)
        {
            EnableTraceToDebugOuptput = enableTraceToDebugOuptput;
        }
        public bool EnableTraceToDebugOuptput { get; }
        public EvmProgramExecution EvmProgramExecution { get; }
#endif
        public EVMSimulator(EvmProgramExecution evmProgramExecution = null)
       {
            EvmProgramExecution = evmProgramExecution ?? new EvmProgramExecution();
        }
      
        public async Task<Program> ExecuteAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true, bool staticCall = false)
        {
           
            var programExecutionCounter = 0;
            program.ProgramContext.InitialiaseContractBalanceFromCallInputValue();

            while(program.Stopped != true)
            {
                var currentInstruction = program.GetCurrentInstruction();
                if (traceEnabled)
                {
                    
                    var trace = ProgramTrace.CreateTraceFromCurrentProgram(program.ProgramContext.AddressContract, vmExecutionCounter, programExecutionCounter, depth, program, currentInstruction);
                     program.Trace.Add(trace);
#if DEBUG
                    if (EnableTraceToDebugOuptput)
                    {
                        Debug.WriteLine(trace.ToString());
                    }
#endif
                }
                var innerTrace = await StepAsync(program, vmExecutionCounter, depth, traceEnabled);
                programExecutionCounter++;
                vmExecutionCounter = vmExecutionCounter + 1 + innerTrace.Count;
                program.Trace.AddRange(innerTrace);
            }
            return program;
        }
        public async Task<List<ProgramTrace>> StepAsync(Program program, int vmExecutionCounter, int depth = 0, bool traceEnabled = true)
        {
            var innerTraceResult = new List<ProgramTrace>();
            if (!program.Stopped) 
            { 
                var instruction = program.GetCurrentInstruction();
                if (instruction.Instruction != null)
                {
                    switch (instruction.Instruction.Value)
                    {
                        case Instruction.STOP:
                            program.Stop();
                            break;
                        case Instruction.ADD:
                            EvmProgramExecution.Arithmetic.Add(program);
                            break;
                        case Instruction.MUL:
                            EvmProgramExecution.Arithmetic.Mul(program);
                            break;
                        case Instruction.SUB:
                            EvmProgramExecution.Arithmetic.Sub(program);
                            break;
                        case Instruction.DIV:
                            EvmProgramExecution.Arithmetic.Div(program);
                            break;
                        case Instruction.SDIV:
                            EvmProgramExecution.Arithmetic.SDiv(program);
                            break;
                        case Instruction.MOD:
                            EvmProgramExecution.Arithmetic.Mod(program);
                            break;
                        case Instruction.SMOD:
                            EvmProgramExecution.Arithmetic.SMod(program);
                            
                            break;
                        case Instruction.ADDMOD:
                            EvmProgramExecution.Arithmetic.AddMod(program);
                            break;
                        case Instruction.MULMOD:
                            EvmProgramExecution.Arithmetic.MulMod(program);
                            break;
                        case Instruction.EXP:
                            EvmProgramExecution.Arithmetic.Exp(program);
                            break;
                   //------------------------///    
                        case Instruction.LT:
                            EvmProgramExecution.Bitwise.LT(program);
                            break;
                        case Instruction.GT:
                            EvmProgramExecution.Bitwise.GT(program);
                            break;
                        case Instruction.SLT:
                            EvmProgramExecution.Bitwise.SLT(program);
                            break;
                        case Instruction.SGT:
                            EvmProgramExecution.Bitwise.SGT(program);
                            break;
                        case Instruction.EQ:
                            EvmProgramExecution.Bitwise.EQ(program);
                            break;
                        case Instruction.ISZERO:
                            EvmProgramExecution.Bitwise.IsZero(program);
                            break;
                        case Instruction.AND:
                            EvmProgramExecution.Bitwise.And(program);
                            break;
                        case Instruction.OR:
                            EvmProgramExecution.Bitwise.Or(program);
                            break;
                        case Instruction.XOR:
                            EvmProgramExecution.Bitwise.Xor(program);
                            break;
                        case Instruction.NOT:
                            EvmProgramExecution.Bitwise.Not(program);
                            break;
                        case Instruction.BYTE:
                            EvmProgramExecution.Bitwise.Byte(program);
                            break;
                        case Instruction.SHL:
                            EvmProgramExecution.Bitwise.ShiftLeft(program);
                            break;
                        case Instruction.SHR:
                            EvmProgramExecution.Bitwise.ShiftRight(program);
                            break;
                        case Instruction.SAR:
                            EvmProgramExecution.Bitwise.ShiftSignedRight(program);
                            break;
                        case Instruction.SIGNEXTEND:
                            EvmProgramExecution.Bitwise.SignExtend(program);
                            break;
                    //-------------------///
                        case Instruction.ORIGIN:
                            EvmProgramExecution.CallInput.Origin(program);
                            break;
                        case Instruction.CALLER:
                            EvmProgramExecution.CallInput.Caller(program);
                            break;
                        case Instruction.CALLVALUE:
                            EvmProgramExecution.CallInput.CallValue(program);
                      //--------------------//      
                            break;
                        case Instruction.CALLDATALOAD:
                            EvmProgramExecution.CallData.CallDataLoad(program);
                            break;
                        case Instruction.CALLDATASIZE:
                            EvmProgramExecution.CallData.CallDataSize(program);
                            break;
                        case Instruction.CALLDATACOPY:
                            EvmProgramExecution.CallData.CallDataCopy(program);
                            break;
                        //--------------------//      
                        case Instruction.CODESIZE:
                            EvmProgramExecution.Code.CodeSize(program);
                            break;
                        case Instruction.CODECOPY:
                            EvmProgramExecution.Code.CodeCopy(program);
                            break;
                        case Instruction.EXTCODESIZE:
                            await EvmProgramExecution.Code.ExtCodeSizeAsync(program);
                            break;
                        case Instruction.EXTCODECOPY:
                            await EvmProgramExecution.Code.ExtCodeCopyAsync(program);
                            break;
                        case Instruction.EXTCODEHASH:
                            await EvmProgramExecution.Code.ExtCodeHashAsync(program);
                            break;
                        //--------------------//      
                        case Instruction.KECCAK256:
                            EvmProgramExecution.BlockchainCurrentContractContext.SHA3(program);
                            break;
                        case Instruction.ADDRESS:
                            EvmProgramExecution.BlockchainCurrentContractContext.Address(program);
                            break;
                        case Instruction.BALANCE:
                            await EvmProgramExecution.BlockchainCurrentContractContext.BalanceAsync(program);
                            break;
                        case Instruction.COINBASE:
                            EvmProgramExecution.BlockchainCurrentContractContext.Coinbase(program);
                            break;
                        case Instruction.TIMESTAMP:
                            EvmProgramExecution.BlockchainCurrentContractContext.TimeStamp(program);
                            break;
                        case Instruction.NUMBER:
                            EvmProgramExecution.BlockchainCurrentContractContext.BlockNumber(program);
                            break;
                        case Instruction.SELFBALANCE:
                            await EvmProgramExecution.BlockchainCurrentContractContext.SelfBalanceAsync(program);
                            break;
                        case Instruction.GASPRICE:
                            EvmProgramExecution.BlockchainCurrentContractContext.GasPrice(program);
                            break;
                        case Instruction.GASLIMIT:
                            EvmProgramExecution.BlockchainCurrentContractContext.GasLimit(program);
                            break;
                        case Instruction.GAS:
                            EvmProgramExecution.BlockchainCurrentContractContext.Gas(program);
                            
                            break;
                        case Instruction.DIFFICULTY:
                            EvmProgramExecution.BlockchainCurrentContractContext.Difficulty(program);
                            break;
                       
                        case Instruction.CHAINID:
                            EvmProgramExecution.BlockchainCurrentContractContext.ChainId(program);
                            
                            break;
                        case Instruction.BASEFEE:
                            EvmProgramExecution.BlockchainCurrentContractContext.BaseFee(program);
                            break;
                        case Instruction.BLOCKHASH:
                            await EvmProgramExecution.BlockchainCurrentContractContext.BlockHashAsync(program);
                            break;
                        //--------------------//      
                        case Instruction.POP:
                             EvmProgramExecution.StackFlowExecution.Pop(program);
                            break;
                        case Instruction.JUMP:
                            EvmProgramExecution.StackFlowExecution.Jump(program);
                            break;
                        case Instruction.JUMPI:
                            EvmProgramExecution.StackFlowExecution.Jumpi(program);
                            break;

                        case Instruction.JUMPDEST:
                            EvmProgramExecution.StackFlowExecution.JumpDest(program);
                            
                            break;
                        case Instruction.PC:
                            EvmProgramExecution.StackFlowExecution.PC(program);
                            break;
                        case Instruction.PUSH1:
                        case Instruction.PUSH2:
                        case Instruction.PUSH3:
                        case Instruction.PUSH4:
                        case Instruction.PUSH5:
                        case Instruction.PUSH6:
                        case Instruction.PUSH7:
                        case Instruction.PUSH8:
                        case Instruction.PUSH9:
                        case Instruction.PUSH10:
                        case Instruction.PUSH11:
                        case Instruction.PUSH12:
                        case Instruction.PUSH13:
                        case Instruction.PUSH14:
                        case Instruction.PUSH15:
                        case Instruction.PUSH16:
                        case Instruction.PUSH17:
                        case Instruction.PUSH18:
                        case Instruction.PUSH19:
                        case Instruction.PUSH20:
                        case Instruction.PUSH21:
                        case Instruction.PUSH22:
                        case Instruction.PUSH23:
                        case Instruction.PUSH24:
                        case Instruction.PUSH25:
                        case Instruction.PUSH26:
                        case Instruction.PUSH27:
                        case Instruction.PUSH28:
                        case Instruction.PUSH29:
                        case Instruction.PUSH30:
                        case Instruction.PUSH31:
                        case Instruction.PUSH32:
                            EvmProgramExecution.StackFlowExecution.Push(program);
                            break;
                        case Instruction.DUP1:
                        case Instruction.DUP2:
                        case Instruction.DUP3:
                        case Instruction.DUP4:
                        case Instruction.DUP5:
                        case Instruction.DUP6:
                        case Instruction.DUP7:
                        case Instruction.DUP8:
                        case Instruction.DUP9:
                        case Instruction.DUP10:
                        case Instruction.DUP11:
                        case Instruction.DUP12:
                        case Instruction.DUP13:
                        case Instruction.DUP14:
                        case Instruction.DUP15:
                        case Instruction.DUP16:
                            EvmProgramExecution.StackFlowExecution.Dup(program);
                            break;
                        case Instruction.SWAP1:
                        case Instruction.SWAP2:
                        case Instruction.SWAP3:
                        case Instruction.SWAP4:
                        case Instruction.SWAP5:
                        case Instruction.SWAP6:
                        case Instruction.SWAP7:
                        case Instruction.SWAP8:
                        case Instruction.SWAP9:
                        case Instruction.SWAP10:
                        case Instruction.SWAP11:
                        case Instruction.SWAP12:
                        case Instruction.SWAP13:
                        case Instruction.SWAP14:
                        case Instruction.SWAP15:
                        case Instruction.SWAP16:
                            EvmProgramExecution.StackFlowExecution.Swap(program);
                            break;
                        //--------------------//      
                        case Instruction.MLOAD:
                            EvmProgramExecution.StorageMemory.MLoad(program);
                            break;
                        case Instruction.MSTORE:
                            EvmProgramExecution.StorageMemory.MStore(program);
                            break;
                        case Instruction.MSTORE8:
                            EvmProgramExecution.StorageMemory.MStore8(program);
                            break;
                        case Instruction.MSIZE:
                            EvmProgramExecution.StorageMemory.MSize(program);
                            break;
                        case Instruction.SLOAD:
                            await EvmProgramExecution.StorageMemory.SLoad(program);
                            break;
                        case Instruction.SSTORE:
                            EvmProgramExecution.StorageMemory.SStore(program);
                            break;
                        //--------------------//      
                        case Instruction.LOG0:
                        case Instruction.LOG1:
                        case Instruction.LOG2:
                        case Instruction.LOG3:
                        case Instruction.LOG4:
                            EvmProgramExecution.ReturnRevertLogExecution.Log(program);
                            break;
                        case Instruction.REVERT:
                            EvmProgramExecution.ReturnRevertLogExecution.Revert(program);
                            break;
                        case Instruction.RETURN:
                            EvmProgramExecution.ReturnRevertLogExecution.Return(program);
                            break;
                       
                        case Instruction.RETURNDATASIZE:
                            EvmProgramExecution.ReturnRevertLogExecution.ReturnDataSize(program);
                            break;
                        case Instruction.RETURNDATACOPY:
                            EvmProgramExecution.ReturnRevertLogExecution.ReturnDataCopy(program);
                            break;
                        //--------------------//      
                        case Instruction.DELEGATECALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.DelegateCallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CALLCODE:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CallCodeAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.STATICCALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.StaticCallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.SELFDESTRUCT:
                            await EvmProgramExecution.CallingCreation.SelfDestructAsync(program);
                            break;
                        case Instruction.CREATE:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CreateAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CREATE2:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.Create2Async(program, vmExecutionCounter, depth, traceEnabled);
                            break;

                        case Instruction.INVALID:
                            program.Stop();
                            break;
                      
                       
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                
            }
            return innerTraceResult;

        }
    }
}