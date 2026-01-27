using System;
using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmCallInputDataExecution
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CallDataCopy(Program program)
        {
            var indexInMemoryBig = program.StackPopAndConvertToUBigInteger();
            var indexOfDataBig = program.StackPopAndConvertToUBigInteger();
            var lengthDataToCopyBig = program.StackPopAndConvertToUBigInteger();
            var dataInput = program.ProgramContext.DataInput;

            if (indexInMemoryBig > int.MaxValue || lengthDataToCopyBig > int.MaxValue)
            {
                program.Step();
                return;
            }

            var indexInMemory = (int)indexInMemoryBig;
            var lengthDataToCopy = (int)lengthDataToCopyBig;

            if (indexOfDataBig > int.MaxValue || indexOfDataBig >= dataInput.Length)
            {
                program.WriteToMemory(indexInMemory, lengthDataToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
            }
            else
            {
                var indexOfData = (int)indexOfDataBig;
                var size = dataInput.Length - indexOfData;

                if (lengthDataToCopy < size)
                {
                    size = lengthDataToCopy;
                }

                var dataToCopy = new byte[size];
                Array.Copy(dataInput, indexOfData, dataToCopy, 0, size);
                program.WriteToMemory(indexInMemory, lengthDataToCopy, dataToCopy);
            }

            program.Step();
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CallDataLoad(Program program)
        {
            var indexBig = program.StackPopAndConvertToUBigInteger();
            var dataInput = program.ProgramContext.DataInput;
            if (indexBig > int.MaxValue || indexBig >= dataInput.Length)
            {
                program.StackPush(0);
            }
            else
            {
                var index = (int)indexBig;
                int size = Math.Min(dataInput.Length - index, 32);
                byte[] dataLoaded = new byte[32];
                Array.Copy(dataInput, index, dataLoaded, 0, size);
                program.StackPush(dataLoaded);
            }
            program.Step();
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CallDataSize(Program program)
        {
            program.StackPush(program.ProgramContext.DataInput.Length);
            program.Step();
        }


    }
}