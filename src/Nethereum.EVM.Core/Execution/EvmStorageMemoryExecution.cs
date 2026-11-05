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
            var key = program.StackPopU256();
#if EVM_SYNC
            var currentValue = program.ProgramContext.GetFromStorage(key);
#else
            var currentValue = await program.ProgramContext.GetFromStorageAsync(key);
#endif
            var newValue = program.StackPop();

            CalculateSStoreRefund(program, key, currentValue, newValue);

            ApplyEip8037StorageSetStateGas(program, key, currentValue, newValue);
#if EVM_SYNC
            if (program.HasExecutionError) return;
#endif

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

            program.ProgramContext.SstoreRefundRule.Apply(program, currentVal, newVal, origVal);
        }

        private static void ApplyEip8037StorageSetStateGas(Program program, EvmUInt256 key, byte[] currentValue, byte[] newValue)
        {
            if (!program.ProgramContext.StateGasActive) return;

            var contextAddress = program.ProgramContext.AddressContract;
            var state = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(contextAddress);

            var currentVal = currentValue?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
            var newVal = newValue?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
            var origVal = state.OriginalStorageValues.ContainsKey(key)
                ? state.OriginalStorageValues[key]?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32)
                : currentVal;

            if (ByteUtil.AreEqual(newVal, currentVal))
                return;

            var originalIsZero = ByteUtil.IsZero(origVal);
            long stateGas = 0;

            if (ByteUtil.AreEqual(origVal, currentVal) && originalIsZero)
            {
                stateGas = GasConstants.EIP8037_STORAGE_SET_STATE_GAS;
            }

            if (ByteUtil.AreEqual(origVal, newVal) && originalIsZero)
            {
                StateGasMeter.CreditStateGasRefund(program, GasConstants.EIP8037_STORAGE_SET_STATE_GAS);
            }

            StateGasMeter.ChargeStateGas(program, stateGas);
        }

        public void MLoad(Program program)
        {
            var indexU256 = program.StackPopU256();
            program.StackPush(program.LoadWordFromMemory(indexU256.ToInt()));
            program.Advance(1);
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
                program.Halt(EvmHaltReason.OutOfGas);
                return;
            }

            var destOffset = destOffsetU256.ToInt();
            var srcOffset = srcOffsetU256.ToInt();
            var length = lengthU256.ToInt();

            var maxMemoryEnd = Math.Max(srcOffset + length, destOffset + length);
            program.ExpandMemory(maxMemoryEnd);

            program.MoveWithinMemory(destOffset, srcOffset, length);
            program.Step();
        }

        public void MStore8(Program program)
        {
            var indexU256 = program.StackPopU256();
            var value = program.StackPopU256();
            program.StoreByteInMemory(indexU256.ToInt(), (byte)value.U0);
            program.Advance(1);
        }

        public void MStore(Program program)
        {
            var indexU256 = program.StackPopU256();
            var value = program.StackPopU256();
            program.StoreWordInMemory(indexU256.ToInt(), value);
            program.Advance(1);
        }

        public void MSize(Program program)
        {
            program.StackPush(program.Memory.Count);
            program.Step();
        }
    }
}
