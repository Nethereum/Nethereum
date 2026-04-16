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
            Log(program, (int)program.GetCurrentInstruction().Value - (int)Instruction.LOG0);
        }
        public void Log(Program program, int numberTopics)
        {
            if (program.ProgramContext.IsStatic)
            {
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new StaticCallViolationException("LOG" + numberTopics);
#endif
            }

            var address = program.ProgramContext.AddressContract;

            var memStartU256 = program.StackPopU256();
            var memLengthU256 = program.StackPopU256();


            var topics = new List<string>();
            for (int i = 0; i < numberTopics; ++i)
            {
                var topic = program.StackPop();
                topics.Add(topic.ToHex());
            }

            byte[] data;
            if (memLengthU256.IsZero)
            {
                data = new byte[0];
            }
            else
            {
                var memStart = memStartU256.ToInt();
                var memLength = memLengthU256.ToInt();

                if (memStart + memLength > program.Memory.Count)
                {
                    program.ExpandMemory(memStart + memLength);
                }
                data = program.Memory.GetRange(memStart, memLength).ToArray();
            }

#if EVM_SYNC
            var filterLog = new EvmLog
            {
                Address = address,
                Topics = topics.ToArray(),
                Data = data.ToHex()
            };
#else
            var filterLog = new FilterLog
            {
                Address = address,
                Topics = topics.ToArray(),
                Data = data.ToHex()
            };
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
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new OutOfGasException(program.GasRemaining, 0);
#endif
            }

            var memoryIndex = memoryIndexU.ToInt();
            var resultIndex = resultIndexU.ToInt();
            var lengthResult = lengthResultU.ToInt();

            if (lengthResult > 0)
            {
                var dataToCopy = new byte[lengthResult];
                Array.Copy(result, resultIndex, dataToCopy, 0, lengthResult);
                program.WriteToMemory(memoryIndex, lengthResult, dataToCopy);
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
            try
            {
                program.ProgramResult.IsRevert = true;
                Return(program);
            }
            catch
            {
                program.Stop();
            }
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
                byte[] result = program.Memory.GetRange(index, available).ToArray();
                Array.Copy(result, returnByte, result.Length);
                program.ProgramResult.Result = returnByte;
            }
            else
            {
                byte[] result = program.Memory.GetRange(index, size).ToArray();
                program.ProgramResult.Result = result;
            }

            program.Stop();
        }
    }
}
