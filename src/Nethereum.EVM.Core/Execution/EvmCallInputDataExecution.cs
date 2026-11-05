using System;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmCallInputDataExecution
    {
        public void CallDataCopy(Program program)
        {
            var indexInMemoryU256 = program.StackPopU256();
            var indexOfDataU256 = program.StackPopU256();
            var lengthDataToCopyU256 = program.StackPopU256();
            var dataInput = program.ProgramContext.DataInput;

            if (!indexInMemoryU256.FitsInInt || !lengthDataToCopyU256.FitsInInt)
            {
                program.Step();
                return;
            }

            var indexInMemory = indexInMemoryU256.ToInt();
            var lengthDataToCopy = lengthDataToCopyU256.ToInt();

            if (!indexOfDataU256.FitsInInt || indexOfDataU256.ToInt() >= dataInput.Length)
            {
                program.WriteToMemory(indexInMemory, lengthDataToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
            }
            else
            {
                var indexOfData = indexOfDataU256.ToInt();
                var size = dataInput.Length - indexOfData;

                if (lengthDataToCopy < size)
                {
                    size = lengthDataToCopy;
                }

                program.CopyToMemory(indexInMemory, lengthDataToCopy, dataInput, indexOfData, size);
            }

            program.Step();
        }



        public void CallDataLoad(Program program)
        {
            var indexU256 = program.StackPopU256();
            var dataInput = program.ProgramContext.DataInput;
            if (!indexU256.FitsInInt || indexU256.ToInt() >= dataInput.Length)
            {
                program.StackPush(0);
            }
            else
            {
                program.StackPush(EvmUInt256.FromBigEndian(dataInput, indexU256.ToInt(), 32));
            }
            program.Step();
        }



        public void CallDataSize(Program program)
        {
            program.StackPush(program.ProgramContext.DataInput.Length);
            program.Step();
        }


    }
}