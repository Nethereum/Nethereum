using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Nethereum.EVM.Exceptions;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{

    public class EvmStorageMemoryExecution
    {
        public async Task<BigInteger> SLoad(Program program)
        {
            var key = program.StackPopAndConvertToUBigInteger();
            var storageValue = await program.ProgramContext.GetFromStorageAsync(key);

            if (storageValue == null)
            {
                program.StackPush(0);
            }
            else
            {
                program.StackPush(storageValue.PadTo32Bytes());
            }
            program.Step();
            return key;
        }

        public async Task SStore(Program program)
        {
            if (program.ProgramContext.IsStatic)
                throw new StaticCallViolationException("SSTORE");

            var key = program.StackPopAndConvertToUBigInteger();
            //ensure we have the value in storage to track it
            //always first so we can track the original value
            await program.ProgramContext.GetFromStorageAsync(key);
            var storageValue = program.StackPop();
            program.ProgramContext.SaveToStorage(key, storageValue);
            program.Step();
        }

        public void MLoad(Program program)
        {
            var index = (int)program.StackPopAndConvertToUBigInteger();

            program.ExpandMemory(index + 32);
            var data = program.Memory.GetRange(index, 32).ToArray();

            program.StackPush(data);
            program.Step();
        }

        public void MCopy(Program program)
        {
            var destOffset = (int)program.StackPopAndConvertToUBigInteger(); 
            var srcOffset = (int)program.StackPopAndConvertToUBigInteger();  
            var length = (int)program.StackPopAndConvertToUBigInteger();     

            var srcData = new byte[length];

            if (srcOffset < program.Memory.Count)
            {
                var available = Math.Min(length, program.Memory.Count - srcOffset);
                program.Memory.CopyTo(srcOffset, srcData, 0, available);
            }

            program.WriteToMemory(destOffset, length, srcData);
            program.Step();
        }

        public void MStore8(Program program)
        {
            var index = program.StackPopAndConvertToUBigInteger();
            var value = program.StackPop();
            program.WriteToMemory((int)index, new byte[] { value[31] });
            program.Step();
        }

        public void MStore(Program program)
        {
            var index = program.StackPopAndConvertToUBigInteger();
            var value = program.StackPop();
            program.WriteToMemory((int)index, value);
            program.Step();
        }

        public void MSize(Program program)
        {
            program.StackPush(program.Memory.Count);
            program.Step();
        }
    }
}