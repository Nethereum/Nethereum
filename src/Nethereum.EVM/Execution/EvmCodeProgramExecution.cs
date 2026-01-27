using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmCodeExecution
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CodeCopy(Program program)
        {
            var byteCode = program.ByteCode;
            var byteCodeLength = byteCode.Length;

            var indexInMemoryBig = program.StackPopAndConvertToUBigInteger();
            var indexOfByteCodeBig = program.StackPopAndConvertToUBigInteger();
            var lengthOfByteCodeToCopyBig = program.StackPopAndConvertToUBigInteger();

            if (indexInMemoryBig > int.MaxValue || lengthOfByteCodeToCopyBig > int.MaxValue)
            {
                program.Step();
                return;
            }

            var indexInMemory = (int)indexInMemoryBig;
            var lengthOfByteCodeToCopy = (int)lengthOfByteCodeToCopyBig;

            if (indexOfByteCodeBig > int.MaxValue || indexOfByteCodeBig >= byteCodeLength)
            {
                program.WriteToMemory(indexInMemory, lengthOfByteCodeToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
            }
            else
            {
                var indexOfByteCode = (int)indexOfByteCodeBig;
                CodeCopyInternal(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
                return;
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CodeCopyInternal(Program program, byte[] byteCode, int byteCodeLength, int indexInMemory, int indexOfByteCode, int lengthOfByteCodeToCopy)
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
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
                address = address.Skip(address.Length - 20).ToArray();
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
            if (byteCode == null)
            {
                byteCode = ByteUtil.EMPTY_BYTE_ARRAY;
            }

            var byteCodeLength = byteCode.Length;
            var indexInMemoryBig = program.StackPopAndConvertToUBigInteger();
            var indexOfByteCodeBig = program.StackPopAndConvertToUBigInteger();
            var lengthOfByteCodeToCopyBig = program.StackPopAndConvertToUBigInteger();

            if (indexInMemoryBig > int.MaxValue || lengthOfByteCodeToCopyBig > int.MaxValue)
            {
                program.Step();
                return;
            }

            var indexInMemory = (int)indexInMemoryBig;
            var lengthOfByteCodeToCopy = (int)lengthOfByteCodeToCopyBig;

            if (indexOfByteCodeBig > int.MaxValue || indexOfByteCodeBig >= byteCodeLength)
            {
                program.WriteToMemory(indexInMemory, lengthOfByteCodeToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
                program.Step();
            }
            else
            {
                var indexOfByteCode = (int)indexOfByteCodeBig;
                CodeCopyInternal(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CodeSize(Program program)
        {
            var size = program.ByteCode.Length;
            program.StackPush(size);
            program.Step();
        }

        public async Task ExtCodeSizeAsync(Program program)
        {
            var address = program.StackPop();
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
                address = address.Skip(address.Length - 20).ToArray();
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
            var codeSize = code?.Length ?? 0;
            program.StackPush(codeSize);
            program.Step();
        }

        public async Task ExtCodeHashAsync(Program program)
        {
            var address = program.StackPop();
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
                address = address.Skip(address.Length - 20).ToArray();
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
            if (code == null)
            {
                code = ByteUtil.EMPTY_BYTE_ARRAY;
            }
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            program.StackPush(codeHash);
            program.Step();
        }
    }
}