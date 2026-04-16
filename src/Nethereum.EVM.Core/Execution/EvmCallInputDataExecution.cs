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

                var dataToCopy = new byte[size];
                Array.Copy(dataInput, indexOfData, dataToCopy, 0, size);
                program.WriteToMemory(indexInMemory, lengthDataToCopy, dataToCopy);
            }

            program.Step();
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                var index = indexU256.ToInt();
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