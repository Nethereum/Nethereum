using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Gas;
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

            // Note: EIP-2200 sentry check is done in EVMSimulator BEFORE gas deduction.
            // Do NOT add a sentry check here - gas has already been deducted at this point,
            // so GasRemaining would be the wrong value to check against the 2300 threshold.

            var key = program.StackPopAndConvertToUBigInteger();
            var currentValue = await program.ProgramContext.GetFromStorageAsync(key);
            var newValue = program.StackPop();

            CalculateSStoreRefund(program, key, currentValue, newValue);

            program.ProgramContext.SaveToStorage(key, newValue);
            program.Step();
        }

        private void CalculateSStoreRefund(Program program, BigInteger key, byte[] currentValue, byte[] newValue)
        {
            var contextAddress = program.ProgramContext.AddressContract;
            var state = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(contextAddress);

            var currentVal = currentValue?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
            var newVal = newValue?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
            var origVal = state.OriginalStorageValues.ContainsKey(key)
                ? state.OriginalStorageValues[key]?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32)
                : currentVal;

            if (ByteUtil.AreEqual(newVal, currentVal))
                return;

            if (ByteUtil.AreEqual(currentVal, origVal))
            {
                if (!ByteUtil.IsZero(origVal) && ByteUtil.IsZero(newVal))
                {
                    program.AddRefund(GasConstants.SSTORE_CLEARS_SCHEDULE);
                }
            }
            else
            {
                if (!ByteUtil.IsZero(origVal))
                {
                    if (ByteUtil.IsZero(currentVal))
                    {
                        program.AddRefund(-GasConstants.SSTORE_CLEARS_SCHEDULE);
                    }
                    else if (ByteUtil.IsZero(newVal))
                    {
                        program.AddRefund(GasConstants.SSTORE_CLEARS_SCHEDULE);
                    }
                }

                if (ByteUtil.AreEqual(newVal, origVal))
                {
                    if (ByteUtil.IsZero(origVal))
                    {
                        program.AddRefund(GasConstants.SSTORE_SET_REFUND);
                    }
                    else
                    {
                        program.AddRefund(GasConstants.SSTORE_RESET_REFUND);
                    }
                }
            }
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
            var destOffsetBig = program.StackPopAndConvertToUBigInteger();
            var srcOffsetBig = program.StackPopAndConvertToUBigInteger();
            var lengthBig = program.StackPopAndConvertToUBigInteger();

            if (lengthBig == 0)
            {
                program.Step();
                return;
            }

            // For huge values that exceed int.MaxValue, memory expansion gas would be astronomical
            // and would have already caused OOG. If we get here with such values, something is wrong.
            if (destOffsetBig > int.MaxValue || srcOffsetBig > int.MaxValue || lengthBig > int.MaxValue)
            {
                throw new Exceptions.OutOfGasException("Memory offset or length exceeds maximum");
            }

            var destOffset = (int)destOffsetBig;
            var srcOffset = (int)srcOffsetBig;
            var length = (int)lengthBig;

            // EIP-5656: Memory must expand to max(srcOffset + length, destOffset + length)
            var maxMemoryEnd = Math.Max(srcOffset + length, destOffset + length);
            program.ExpandMemory(maxMemoryEnd);

            var srcData = new byte[length];
            program.Memory.CopyTo(srcOffset, srcData, 0, length);

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