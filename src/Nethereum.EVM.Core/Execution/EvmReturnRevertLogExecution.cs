using System;
using System.Collections.Generic;
using Nethereum.EVM.Exceptions;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmReturnRevertLogExecution
    {
        public void Log(Program program)
        {
            Log(program, program.CurrentOpcodeByte - (int)Instruction.LOG0);
        }
        public void Log(Program program, int numberTopics)
        {
            var address = program.ProgramContext.AddressContract;

            var memStartU256 = program.StackPopU256();
            var memLengthU256 = program.StackPopU256();


            var topics = new string[numberTopics];
            for (int i = 0; i < numberTopics; ++i)
            {
                topics[i] = HexText.FromWord(program.StackPopU256());
            }

            string data;
            if (memLengthU256.IsZero)
            {
                data = string.Empty;
            }
            else
            {
                var memStart = memStartU256.ToInt();
                var memLength = memLengthU256.ToInt();

                if (memStart + memLength > program.Memory.Count)
                {
                    program.ExpandMemory(memStart + memLength);
                }
                data = program.ReadMemoryHex(memStart, memLength);
            }

#if EVM_SYNC
            var filterLog = new EvmLog
            {
                Address = address,
                Topics = topics,
                Data = data
            };
#else
            var filterLog = new FilterLog
            {
                Address = address,
                Topics = topics,
                Data = data
            };
#endif
#if !EVM_SYNC
            if (program.ProgramContext.LogIndexCounter != null)
                filterLog.LogIndex = new Nethereum.Hex.HexTypes.HexBigInteger(program.ProgramContext.LogIndexCounter.Next());
#endif
            program.ProgramResult.Logs.Add(filterLog);
            program.Step();
        }

        public  void ReturnDataCopy(Program program)
        {
            var memoryIndexU = program.StackPopU256();
            var resultIndexU = program.StackPopU256();
            var lengthResultU = program.StackPopU256();
            var result = program.ProgramResult.LastCallReturnData ?? new byte[0];

            var endU = resultIndexU + lengthResultU;
            if (endU < resultIndexU || !endU.FitsInInt || endU.ToInt() > result.Length)
            {
                program.Halt(EvmHaltReason.OutOfBoundsRead);
                return;
            }

            var memoryIndex = memoryIndexU.ToInt();
            var resultIndex = resultIndexU.ToInt();
            var lengthResult = lengthResultU.ToInt();

            if (lengthResult > 0)
            {
                program.CopyToMemory(memoryIndex, lengthResult, result, resultIndex, lengthResult);
            }
            program.Step();
        }

        public  void ReturnDataSize(Program program)
        {
            var result = program.ProgramResult.LastCallReturnData;
            var length = 0;
            if (result != null)
            {
                length = result.Length;
            }
            program.StackPush(length);
            program.Step();
        }

        public void Revert(Program program)
        {
            program.ProgramResult.IsRevert = true;
            Return(program);
        }

        public  void Return(Program program)
        {
            var indexU256 = program.StackPopU256();
            var sizeU256 = program.StackPopU256();

            if (sizeU256.IsZero)
            {
                program.ProgramResult.Result = new byte[0];
                program.Stop();
                return;
            }

            var index = indexU256.ToInt();
            var size = sizeU256.ToInt();

            if (index >= program.Memory.Count)
            {
                program.ProgramResult.Result = new byte[size];
            }
            else if (index + size > program.Memory.Count)
            {
                var returnByte = new byte[size];
                var available = program.Memory.Count - index;
                program.Memory.CopyTo(index, returnByte, 0, available);
                program.ProgramResult.Result = returnByte;
            }
            else
            {
                var result = new byte[size];
                program.Memory.CopyTo(index, result, 0, size);
                program.ProgramResult.Result = result;
            }

            program.Stop();
        }
    }
}
