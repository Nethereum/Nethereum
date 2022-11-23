using System;

namespace Nethereum.EVM.Execution
{
    public class EvmCallInputDataExecution
    {
        public void CallDataCopy(Program program)
        {
            var indexInMemory = (int)program.StackPopAndConvertToUBigInteger();
            var indexOfData = (int)program.StackPopAndConvertToUBigInteger();
            var lengthDataToCopy = (int)program.StackPopAndConvertToUBigInteger();
            var dataInput = program.ProgramContext.DataInput;

            if (indexOfData > dataInput.Length)
            {
                program.WriteToMemory(indexInMemory, lengthDataToCopy, new byte[0]);
            }
            else
            {
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



        public void CallDataLoad(Program program)
        {
            var index = (int)program.StackPopAndConvertToBigInteger();
            var dataInput = program.ProgramContext.DataInput;
            if (index > dataInput.Length)
            {
                program.StackPush(0);
            }
            else
            {
                //ensure only 32 bytes
                int size = Math.Min(dataInput.Length - index, 32);
                byte[] dataLoaded = new byte[32];
                Array.Copy(dataInput, index, dataLoaded, 0, size);
                program.StackPush(dataLoaded);
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