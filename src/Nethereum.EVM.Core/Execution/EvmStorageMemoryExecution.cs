using System;
using System.Numerics;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Gas;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{

    public class EvmStorageMemoryExecution
    {
#if EVM_SYNC
        public void SLoad(Program program)
#else
        public async Task SLoad(Program program)
#endif
        {
            var key = program.StackPopU256();
#if EVM_SYNC
            var storageValue = program.ProgramContext.GetFromStorage(key);
#else
            var storageValue = await program.ProgramContext.GetFromStorageAsync(key);
#endif

            if (storageValue == null)
            {
                program.StackPush(0);
            }
            else
            {
                program.StackPush(storageValue.PadTo32Bytes());
            }
            program.Step();
        }

#if EVM_SYNC
        public void SStore(Program program)
#else
        public async Task SStore(Program program)
#endif
        {
            if (program.ProgramContext.IsStatic)
            {
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new StaticCallViolationException("SSTORE");
#endif
            }

            var key = program.StackPopU256();
#if EVM_SYNC
            var currentValue = program.ProgramContext.GetFromStorage(key);
#else
            var currentValue = await program.ProgramContext.GetFromStorageAsync(key);
#endif
            var newValue = program.StackPop();

            CalculateSStoreRefund(program, key, currentValue, newValue);

            program.ProgramContext.SaveToStorage(key, newValue);
            program.Step();
        }

        private void CalculateSStoreRefund(Program program, EvmUInt256 key, byte[] currentValue, byte[] newValue)
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

            var clearsSchedule = program.ProgramContext.SstoreClearsSchedule;
            var setRefund = GasConstants.SSTORE_SET - GasConstants.SSTORE_NOOP;
            var resetRefund = GasConstants.SSTORE_RESET - GasConstants.SSTORE_NOOP;

            if (ByteUtil.AreEqual(currentVal, origVal))
            {
                if (!ByteUtil.IsZero(origVal) && ByteUtil.IsZero(newVal))
                {
                    program.AddRefund(clearsSchedule);
                }
            }
            else
            {
                if (!ByteUtil.IsZero(origVal))
                {
                    if (ByteUtil.IsZero(currentVal))
                    {
                        program.AddRefund(-clearsSchedule);
                    }
                    else if (ByteUtil.IsZero(newVal))
                    {
                        program.AddRefund(clearsSchedule);
                    }
                }

                if (ByteUtil.AreEqual(newVal, origVal))
                {
                    if (ByteUtil.IsZero(origVal))
                    {
                        program.AddRefund(setRefund);
                    }
                    else
                    {
                        program.AddRefund(resetRefund);
                    }
                }
            }
        }

        public void MLoad(Program program)
        {
            var indexU256 = program.StackPopU256();
            var index = indexU256.ToInt();

            program.ExpandMemory(index + 32);
            var data = program.Memory.GetRange(index, 32).ToArray();

            program.StackPush(data);
            program.Step();
        }

        public void MCopy(Program program)
        {
            var destOffsetU256 = program.StackPopU256();
            var srcOffsetU256 = program.StackPopU256();
            var lengthU256 = program.StackPopU256();

            if (lengthU256.IsZero)
            {
                program.Step();
                return;
            }

            if (!destOffsetU256.FitsInInt || !srcOffsetU256.FitsInInt || !lengthU256.FitsInInt)
            {
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new Exceptions.OutOfGasException("Memory offset or length exceeds maximum");
#endif
            }

            var destOffset = destOffsetU256.ToInt();
            var srcOffset = srcOffsetU256.ToInt();
            var length = lengthU256.ToInt();

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
            var indexU256 = program.StackPopU256();
            var value = program.StackPop();
            program.WriteToMemory(indexU256.ToInt(), new byte[] { value[31] });
            program.Step();
        }

        public void MStore(Program program)
        {
            var indexU256 = program.StackPopU256();
            var value = program.StackPop();
            program.WriteToMemory(indexU256.ToInt(), value);
            program.Step();
        }

        public void MSize(Program program)
        {
            program.StackPush(program.Memory.Count);
            program.Step();
        }
    }
}
