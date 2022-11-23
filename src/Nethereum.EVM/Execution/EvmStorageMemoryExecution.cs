using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;

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

        public void SStore(Program program)
        {
            var key = program.StackPopAndConvertToUBigInteger();
            var storageValue = program.StackPop();
            program.ProgramContext.SaveToStorage(key, storageValue);
            program.Step();
        }

        public void MLoad(Program program)
        {
            var index = (int)program.StackPopAndConvertToUBigInteger();

            var data = new byte[32];
            if (index + 32 > program.Memory.Count)
            {
                var dataToCopy = program.Memory.Skip(index).ToArray();
                Array.Copy(dataToCopy, data, dataToCopy.Length);
            }
            else
            {
                data = program.Memory.GetRange(index, 32).ToArray();
            }

            program.StackPush(data);
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