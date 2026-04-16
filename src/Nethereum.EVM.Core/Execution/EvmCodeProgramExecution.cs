using System;
using System.Runtime.CompilerServices;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif
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

            var indexInMemoryU256 = program.StackPopU256();
            var indexOfByteCodeU256 = program.StackPopU256();
            var lengthOfByteCodeToCopyU256 = program.StackPopU256();

            if (!indexInMemoryU256.FitsInInt || !lengthOfByteCodeToCopyU256.FitsInInt)
            {
                program.Step();
                return;
            }

            var indexInMemory = indexInMemoryU256.ToInt();
            var lengthOfByteCodeToCopy = lengthOfByteCodeToCopyU256.ToInt();

            if (!indexOfByteCodeU256.FitsInInt || indexOfByteCodeU256.ToInt() >= byteCodeLength)
            {
                program.WriteToMemory(indexInMemory, lengthOfByteCodeToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
            }
            else
            {
                var indexOfByteCode = indexOfByteCodeU256.ToInt();
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

#if EVM_SYNC
        public void ExtCodeCopy(Program program)
#else
        public async Task ExtCodeCopyAsync(Program program)
#endif
        {
            var address = program.StackPop();
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
            {
                var trimmed = new byte[20];
                Array.Copy(address, address.Length - 20, trimmed, 0, 20);
                address = trimmed;
            }
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);
#if EVM_SYNC
            var byteCode = program.ProgramContext.ExecutionStateService.GetCode(addressString);
#else
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
#endif
            if (byteCode == null)
            {
                byteCode = ByteUtil.EMPTY_BYTE_ARRAY;
            }

            var byteCodeLength = byteCode.Length;
            var indexInMemoryU256 = program.StackPopU256();
            var indexOfByteCodeU256 = program.StackPopU256();
            var lengthOfByteCodeToCopyU256 = program.StackPopU256();

            if (!indexInMemoryU256.FitsInInt || !lengthOfByteCodeToCopyU256.FitsInInt)
            {
                program.Step();
                return;
            }

            var indexInMemory = indexInMemoryU256.ToInt();
            var lengthOfByteCodeToCopy = lengthOfByteCodeToCopyU256.ToInt();

            if (!indexOfByteCodeU256.FitsInInt || indexOfByteCodeU256.ToInt() >= byteCodeLength)
            {
                program.WriteToMemory(indexInMemory, lengthOfByteCodeToCopy, ByteUtil.EMPTY_BYTE_ARRAY);
                program.Step();
            }
            else
            {
                var indexOfByteCode = indexOfByteCodeU256.ToInt();
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

#if EVM_SYNC
        public void ExtCodeSize(Program program)
#else
        public async Task ExtCodeSizeAsync(Program program)
#endif
        {
            var address = program.StackPop();
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
            {
                var trimmed = new byte[20];
                Array.Copy(address, address.Length - 20, trimmed, 0, 20);
                address = trimmed;
            }
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);
#if EVM_SYNC
            var code = program.ProgramContext.ExecutionStateService.GetCode(addressString);
#else
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
#endif
            var codeSize = code?.Length ?? 0;
            program.StackPush(codeSize);
            program.Step();
        }

#if EVM_SYNC
        public void ExtCodeHash(Program program)
#else
        public async Task ExtCodeHashAsync(Program program)
#endif
        {
            var address = program.StackPop();
            // Take last 20 bytes if longer (stack stores 32-byte values)
            if (address.Length > 20)
            {
                var trimmed = new byte[20];
                Array.Copy(address, address.Length - 20, trimmed, 0, 20);
                address = trimmed;
            }
            var addressString = AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
            program.ProgramContext.RecordAddressAccess(addressString);

            // EIP-1052: For non-existent accounts, EXTCODEHASH returns 0
#if EVM_SYNC
            var accountExists = program.ProgramContext.ExecutionStateService.AccountExists(addressString);
#else
            var accountExists = await program.ProgramContext.ExecutionStateService.AccountExistsAsync(addressString);
#endif
            if (!accountExists)
            {
                program.StackPush(0);
                program.Step();
                return;
            }

#if EVM_SYNC
            var code = program.ProgramContext.ExecutionStateService.GetCode(addressString);
#else
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(addressString);
#endif
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
