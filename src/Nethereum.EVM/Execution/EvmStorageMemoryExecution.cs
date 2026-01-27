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

            // EIP-2200: Sentry check - if gas_left <= 2300 before SSTORE, fail
            // This check is ALSO done in EVMSimulator for trace correctness,
            // but we keep it here for direct calls and backwards compatibility
            if (program.ProgramContext.EnforceGasSentry &&
                program.GasRemaining <= GasConstants.SSTORE_SENTRY)
            {
                throw new SStoreSentryException(program.GasRemaining, GasConstants.SSTORE_SENTRY);
            }

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