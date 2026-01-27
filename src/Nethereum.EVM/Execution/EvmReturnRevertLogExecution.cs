using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Exceptions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

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
                throw new StaticCallViolationException("LOG" + numberTopics);

            var address = program.ProgramContext.AddressContract;

            var memStart = (int)program.StackPopAndConvertToUBigInteger();
            var memLength = (int)program.StackPopAndConvertToUBigInteger();


            var topics = new List<string>();
            for (int i = 0; i < numberTopics; ++i)
            {
                var topic = program.StackPop();
                topics.Add(topic.ToHex());
            }

            byte[] data;
            if (memLength == 0)
            {
                // When memLength is 0, no actual memory read is needed
                data = new byte[0];
            }
            else if (memStart + memLength > program.Memory.Count)
            {
                // Memory needs to be expanded - expand it before reading
                program.ExpandMemory(memStart + memLength);
                data = program.Memory.GetRange(memStart, memLength).ToArray();
            }
            else
            {
                data = program.Memory.GetRange(memStart, memLength).ToArray();
            }

            var filterLog = new FilterLog
            {
                Address = address,
                Topics = topics.ToArray(),
                Data = data.ToHex()
            };
            program.ProgramResult.Logs.Add(filterLog);
            program.Step();
        }

        public  void ReturnDataCopy(Program program)
        {
            var memoryIndex = (int)program.StackPopAndConvertToUBigInteger();
            var resultIndex = (int)program.StackPopAndConvertToUBigInteger();
            var lengthResult = (int)program.StackPopAndConvertToUBigInteger();
            var result = program.ProgramResult.LastCallReturnData;
            if (result == null)
            {
                program.WriteToMemory(memoryIndex, lengthResult, new byte[] { });
            }
            else
            {

                var size = result.Length - resultIndex;

                if (lengthResult < size)
                {
                    size = lengthResult;
                }

                var dataToCopy = new byte[size];
                Array.Copy(result, resultIndex, dataToCopy, 0, size);

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
            var index = (int)program.StackPopAndConvertToUBigInteger();
            var size = (int)program.StackPopAndConvertToUBigInteger();

            if (size == 0)
            {
                program.ProgramResult.Result = new byte[0];
            }
            else if (index >= program.Memory.Count)
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