using System;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmCodeExecution
    {
        public void CodeCopy(Program program)
        {
            var byteCode = program.ByteCode;
            var byteCodeLength = byteCode.Length;

            int indexInMemory = (int)program.StackPopAndConvertToUBigInteger();
            int indexOfByteCode = (int)program.StackPopAndConvertToUBigInteger();
            int lengthOfByteCodeToCopy = (int)program.StackPopAndConvertToUBigInteger();
            CodeCopy(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
        }

        private void CodeCopy(Program program, byte[] byteCode, int byteCodeLength, int indexInMemory, int indexOfByteCode, int lengthOfByteCodeToCopy)
        {
            byte[] byteCodeCopy = new byte[lengthOfByteCodeToCopy];

            if (indexOfByteCode < byteCodeLength)
            {
                var totalSizeToBeCopied = lengthOfByteCodeToCopy;
                if (indexOfByteCode + lengthOfByteCodeToCopy > byteCodeLength)
                {
                    totalSizeToBeCopied = byteCodeLength - indexOfByteCode;
                }

                Array.Copy(byteCode, indexOfByteCode, byteCodeCopy, 0, totalSizeToBeCopied);
            }

            program.WriteToMemory(indexInMemory, lengthOfByteCodeToCopy, byteCodeCopy);
            program.Step();
        }

        public async Task ExtCodeCopyAsync(Program program)
        {
            var address = program.StackPop();
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(address.ConvertToEthereumChecksumAddress());

            var byteCodeLength = byteCode.Length;
            int indexInMemory = (int)program.StackPopAndConvertToUBigInteger();
            int indexOfByteCode = (int)program.StackPopAndConvertToUBigInteger();
            int lengthOfByteCodeToCopy = (int)program.StackPopAndConvertToUBigInteger();

            CodeCopy(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
        }

        public void CodeSize(Program program)
        {
            var size = program.ByteCode.Length;
            program.StackPush(size);
            program.Step();
        }

        public async Task ExtCodeSizeAsync(Program program)
        {
            var address = program.StackPop();
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(address.ConvertToEthereumChecksumAddress());
            program.StackPush(code.Length);
            program.Step();
        }

        public async Task ExtCodeHashAsync(Program program)
        {
            var address = program.StackPop();
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(address.ConvertToEthereumChecksumAddress());
            if (code == null)
            {
                code = new byte[] { };
            }
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            program.StackPush(codeHash);
            program.Step();
        }
    }
}