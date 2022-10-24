using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    /// <summary>
    /// EVM simulator 
    /// WIP, experimental, not all opcodes implemented yet (mainly call contracts / create)
    /// Gas calculation not implemented (this might be a future release)
    /// </summary>
    public class EVMSimulator
    {
        public async Task<List<ProgramTrace>> ExecuteAsync(Program program, bool traceEnabled = true)
        {
            var traceResult = new List<ProgramTrace>();
            var vmExecutionCounter = 0;
            while(program.Stopped != true)
            {
                var currentInstruction = program.GetCurrentInstruction();
                if (traceEnabled)
                {
                    
                    var trace = ProgramTrace.CreateTraceFromCurrentProgram(vmExecutionCounter, program, currentInstruction);
                    traceResult.Add(trace);
#if DEBUG
                    Debug.WriteLine(trace.ToString());
#endif
                }
                await StepAsync(program);
                vmExecutionCounter ++;
            }
            return traceResult;
        }
        public async Task StepAsync(Program program)
        {
            if (!program.Stopped) 
            { 
                var instruction = program.GetCurrentInstruction();
                if (instruction.Instruction != null)
                {
                    //var instructionInfo = GetInstructionInfo(instruction.Instruction.Value);
                    switch (instruction.Instruction.Value)
                    {
                        case Instruction.STOP:
                            program.Stop();
                            break;
                        case Instruction.ADD:
                            program.Add();
                            break;
                        case Instruction.MUL:
                            program.Mul();
                            break;
                        case Instruction.SUB:
                            program.Sub();
                            break;
                        case Instruction.DIV:
                            program.Div();
                            break;
                        case Instruction.SDIV:
                            program.SDiv();
                            break;
                        case Instruction.MOD:
                            program.Mod();
                            break;
                        case Instruction.SMOD:
                            program.SMod();
                            break;
                        case Instruction.ADDMOD:
                            program.AddMod();
                            break;
                        case Instruction.MULMOD:
                            program.MulMod();
                            break;
                        case Instruction.EXP:
                            program.Exp();
                            break;
                       
                        case Instruction.LT:
                            program.LT();
                            break;
                        case Instruction.GT:
                            program.GT();
                            break;
                        case Instruction.SLT:
                            program.SLT();
                            break;
                        case Instruction.SGT:
                            program.SGT();
                            break;
                        case Instruction.EQ:
                            program.EQ();
                            break;
                        case Instruction.ISZERO:
                            program.IsZero();
                            break;
                        case Instruction.AND:
                            program.And();
                            break;
                        case Instruction.OR:
                            program.Or();
                            break;
                        case Instruction.XOR:
                            program.Xor();
                            break;
                        case Instruction.NOT:
                            program.Not();
                            break;
                        case Instruction.BYTE:
                            program.Byte();
                            break;
                       
                        case Instruction.KECCAK256:
                            program.SHA3();
                            break;
                        case Instruction.ADDRESS:
                            program.Address();
                            break;
                        case Instruction.BALANCE:
                            await program.BalanceAsync();
                            break;
                        case Instruction.ORIGIN:
                            program.Origin();
                            break;
                        case Instruction.CALLER:
                            program.Caller();
                            break;
                        case Instruction.CALLVALUE:
                            program.CallValue();
                            break;
                        case Instruction.CALLDATALOAD:
                            program.CallDataLoad();
                            break;
                        case Instruction.CALLDATASIZE:
                            program.CallDataSize();
                            break;
                        case Instruction.CALLDATACOPY:
                            program.CallDataCopy();
                            break;
                        case Instruction.CODESIZE:
                            program.CodeSize();
                            break;
                        case Instruction.CODECOPY:
                            program.CodeCopy();
                            break;
                     
                        case Instruction.EXTCODESIZE:
                            await program.ExtCodeSizeAsync();
                            break;
                        case Instruction.EXTCODECOPY:
                            await program.ExtCodeCopyAsync();
                            break;
                       
                        case Instruction.COINBASE:
                            var coinbaseAddress = program.ProgramContext.AddressCoinbaseEncoded;
                            program.StackPush(coinbaseAddress);
                            program.Step();
                            break;
                        case Instruction.TIMESTAMP:
                            var timestamp = program.ProgramContext.Timestamp;
                            program.StackPush(timestamp);
                            program.Step();
                            break;
                        case Instruction.NUMBER:
                            var blockNumber = program.ProgramContext.BlockNumber;
                            program.StackPush(blockNumber);
                            program.Step();
                            break;
                 
                        case Instruction.POP:
                            program.StackPop();
                            program.Step();
                            break;
                        case Instruction.MLOAD:
                            program.MLoad();
                            break;
                        case Instruction.MSTORE:
                            program.MStore();
                            break;
                        case Instruction.MSTORE8:
                            program.MStore8();
                            break;
                        case Instruction.MSIZE:
                            program.StackPush(program.Memory.Count);
                            program.Step();
                            break;
                      
                        case Instruction.JUMP:
                            var dest = (int)program.StackPopAndConvertToBigInteger();
                            program.GoToJumpDestination(dest);
                            break;
                        case Instruction.JUMPI:
                            var desti = (int)program.StackPopAndConvertToBigInteger();
                            var valid = (int)program.StackPopAndConvertToBigInteger();
                            if (valid != 0)
                            {
                                program.GoToJumpDestination(desti);
                            }
                            else
                            {
                                program.Step();
                            }
                            
                            break;
                            
                        case Instruction.JUMPDEST:
                            program.Step();
                            break;
                        case Instruction.PC:
                            var pc = program.GetProgramCounter();
                            program.StackPush(pc);
                            program.Step();
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
                            var data = program.GetCurrentInstruction().Arguments;
                            program.StackPush(data.PadTo32Bytes());
                            program.Step();
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
                            int dupIndex = (int)instruction.Instruction.Value - (int)Instruction.DUP1 + 1;
                            program.StackDup(dupIndex);
                            program.Step();
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
                            int swapIndex = (int)instruction.Instruction.Value - (int)Instruction.SWAP1 + 1;
                            program.StackSwap(swapIndex);
                            program.Step();
                            break;
                        case Instruction.LOG0:
                        case Instruction.LOG1:
                        case Instruction.LOG2:
                        case Instruction.LOG3:
                        case Instruction.LOG4:
                            program.Log(instruction.Instruction.Value - Instruction.LOG0);
                            break;
                        case Instruction.REVERT:
                            program.Revert();
                            break;
                        case Instruction.RETURN:
                            program.Return();
                            break;
                        case Instruction.SHL:
                            program.ShiftLeft();
                            break;
                        case Instruction.SHR:
                            program.ShiftRight();
                            break;
                        case Instruction.SAR:
                            program.ShiftSignedRight();
                            break;
                        case Instruction.SLOAD:
                            await program.SLoad();
                            break;
                        case Instruction.SSTORE:
                            program.SStore();
                            break;
                        case Instruction.RETURNDATASIZE:
                            program.ReturnDataSize();
                            break;
                        case Instruction.RETURNDATACOPY:
                            program.ReturnDataCopy();
                            break;

                        case Instruction.SIGNEXTEND:
                            program.Stop();
                            throw new NotImplementedException();
                            break;
                        case Instruction.GASPRICE:
                            break;
                        case Instruction.BLOCKHASH:
                            break;
                        case Instruction.DIFFICULTY:
                            break;
                        case Instruction.GASLIMIT:
                            break;
                        case Instruction.GAS:
                            break;
                        case Instruction.SELFDESTRUCT:
                            break;
                       
                        case Instruction.EXTCODEHASH:
                            break;
                        case Instruction.CHAINID:
                            break;
                        case Instruction.SELFBALANCE:
                            break;
                        case Instruction.BASEFEE:
                            break;
                        case Instruction.INVALID:
                            break;
                        //Call / create
                        case Instruction.DELEGATECALL:
                            break;
                        case Instruction.CREATE:
                            break;
                        case Instruction.CALL:
                            break;
                        case Instruction.CALLCODE:
                            break;
                        case Instruction.CREATE2:
                            break;
                        case Instruction.STATICCALL:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }


        }

      
        public InstructionInfo GetInstructionInfo(Instruction instruction)
        {
            return InstructionInfoCollection.Instructions[instruction];
        }
    }
}