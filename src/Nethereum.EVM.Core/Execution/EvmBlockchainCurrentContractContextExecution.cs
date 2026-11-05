using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution
{

    public class EvmBlockchainCurrentContractContextExecution
    {
#if EVM_SYNC
        public void Balance(Program program)
        {
            var address = program.StackPop();
            BalanceStep(program, address);
        }

        public void SelfBalance(Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            BalanceStep(program, address);
        }

        private void BalanceStep(Program program, byte[] address)
        {
            var addressString = ToLookupAddress(address);
            program.ProgramContext.RecordAddressAccess(addressString);
            EvmUInt256 balance = program.ProgramContext.ExecutionStateService.GetTotalBalance(addressString);
            program.StackPush(balance);
            program.Step();
        }

        public EvmUInt256 GetTotalBalance(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalance(ToLookupAddress(address));
        }
#else
        public async Task BalanceAsync(Program program)
        {
            var address = program.StackPop();
            await BalanceStepAsync(program, address);
        }

        public async Task SelfBalanceAsync(Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            await BalanceStepAsync(program, address);
        }

        private async Task BalanceStepAsync(Program program, byte[] address)
        {
            var addressString = ToLookupAddress(address);
            program.ProgramContext.RecordAddressAccess(addressString);
            EvmUInt256 balance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(addressString);
            program.StackPush(balance);
            program.Step();
        }

        public Task<EvmUInt256> GetTotalBalanceAsync(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(ToLookupAddress(address));
        }
#endif

        private static string ToLookupAddress(byte[] address)
        {
            if (address.Length > 20)
            {
                var trimmed = new byte[20];
                Array.Copy(address, address.Length - 20, trimmed, 0, 20);
                address = trimmed;
            }
            return AddressUtil.Current.ConvertToValid20ByteAddress(address.ToHex()).ToLower();
        }

        public void Address(Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            program.StackPush(address);
            program.Step();
        }

        public void SHA3(Program program)
        {
            var indexU256 = program.StackPopU256();
            var lengthU256 = program.StackPopU256();

            byte[] data;
            if (lengthU256.IsZero)
            {
                data = new byte[0];
            }
            else
            {
                var index = indexU256.ToInt();
                var length = lengthU256.ToInt();
                int memoryEnd = index + length;
                if (memoryEnd > program.Memory.Count)
                {
                    program.ExpandMemory(memoryEnd);
                }
                data = program.ReadMemory(index, length);
            }

            var encoded = Sha3Keccack.Current.CalculateHash(data);
            program.StackPush(encoded);
            program.Step();
        }

        public void Coinbase(Program program)
        {
            var coinbaseAddress = program.ProgramContext.AddressCoinbaseEncoded;
            program.StackPush(coinbaseAddress);
            program.Step();
        }

        public void TimeStamp(Program program)
        {
            program.StackPush(program.ProgramContext.Timestamp);
            program.Step();
        }

        public void BlockNumber(Program program)
        {
            program.StackPush(program.ProgramContext.BlockNumber);
            program.Step();
        }

        public void GasPrice(Program program)
        {
            program.StackPush(program.ProgramContext.GasPrice);
            program.Step();
        }

        public void GasLimit(Program program)
        {
            program.StackPush(program.ProgramContext.GasLimit);
            program.Step();
        }

        public void Gas(Program program)
        {
            var gas = Math.Max(program.GasRemaining, 0L);
            program.StackPush(gas);
            program.Step();
        }
        public void Difficulty(Program program)
        {
            var difficulty = program.ProgramContext.Difficulty;
            program.StackPush(difficulty);
            program.Step();
        }
        public void ChainId(Program program)
        {
            var chainId = program.ProgramContext.ChainId;
            program.StackPush(chainId);
            program.Step();
        }
        public void BaseFee(Program program)
        {
            var baseFee = program.ProgramContext.BaseFee;
            program.StackPush(baseFee);
            program.Step();
        }

        public void BlobBaseFee(Program program)
        {
            program.StackPush(program.ProgramContext.BlobBaseFee);
            program.Step();
        }

        public void SlotNumber(Program program)
        {
            program.StackPush(program.ProgramContext.SlotNumber);
            program.Step();
        }

        public void BlobHash(Program program)
        {
            var indexU256 = program.StackPopU256();

            if (program.ProgramContext.BlobHashes == null || !indexU256.FitsInInt || indexU256.ToInt() >= program.ProgramContext.BlobHashes.Length)
            {
                program.StackPush(new byte[32]);
            }
            else
            {
                program.StackPush(program.ProgramContext.BlobHashes[indexU256.ToInt()].PadTo32Bytes());
            }

            program.Step();
        }

        public void TLoad(Program program)
        {
            var key = program.StackPopU256();
            var address = EvmAddress.From(program.ProgramContext.AddressContractEncoded);
            var transientStorage = program.ProgramContext.ExecutionStateService.TransientStorage;

            if (transientStorage.TryGetValue(address, out var addressStorage) && addressStorage.TryGetValue(key, out var value))
            {
                program.StackPush(value);
            }
            else
            {
                program.StackPush(EvmUInt256.Zero);
            }

            program.Step();
        }

        public void TStore(Program program)
        {
            var key = program.StackPopU256();
            var value = program.StackPopU256().ToBigEndian();
            var address = EvmAddress.From(program.ProgramContext.AddressContractEncoded);
            program.ProgramContext.ExecutionStateService.SetTransientStorage(address, key, value);

            program.Step();
        }
    }

}
