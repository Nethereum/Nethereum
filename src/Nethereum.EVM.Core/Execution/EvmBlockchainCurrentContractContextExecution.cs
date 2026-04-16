using System;
using System.Numerics;
using Nethereum.Util;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using Nethereum.RLP;
using Nethereum.Util;
#if !EVM_SYNC
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
#endif
using System.Collections;
#if !EVM_SYNC
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.Blocks;
#endif

namespace Nethereum.EVM.Execution
{

    public class EvmBlockchainCurrentContractContextExecution
    {
        // EIP-2935 final values (active at Prague).
        public const string HISTORY_STORAGE_ADDRESS = "0x0000F90827F1C53a10cb7A02335B175320002935";
        public const int HISTORY_SERVE_WINDOW = 8191;
        public const int BLOCKHASH_SERVE_WINDOW = 256;

#if EVM_SYNC
        public void BlockHash(Program program)
#else
        public async Task BlockHashAsync(Program program)
#endif
        {
            var blockNumberU256 = program.StackPopU256();
            if (!blockNumberU256.FitsInULong)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return;
            }
            var blockNumber = blockNumberU256.ToLongSafe();
            var currentBlock = program.ProgramContext.BlockNumber;

            if (blockNumber >= currentBlock || blockNumber < 0)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return;
            }

            var blocksAgo = currentBlock - blockNumber;

            // EIP-2935 (Prague+): BLOCKHASH reads storage slot (blockNumber % 8191) of
            // the history contract. The 256-block window is the client-visible promise;
            // storage outside that range is zero by construction. Gas stays 20 and the
            // history contract is not warmed under EIP-2929.
            if (blocksAgo <= BLOCKHASH_SERVE_WINDOW)
            {
                var slot = new EvmUInt256(blockNumber % HISTORY_SERVE_WINDOW);
#if EVM_SYNC
                var blockHash = program.ProgramContext.ExecutionStateService.GetFromStorage(HISTORY_STORAGE_ADDRESS, slot);
#else
                var blockHash = await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(HISTORY_STORAGE_ADDRESS, slot);
#endif
                program.StackPush(blockHash ?? new byte[32]);
            }
            else
            {
                program.StackPush(new byte[32]);
            }

            program.Step();
        }

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
            var addressString = address.ConvertToEthereumChecksumAddress();
            program.ProgramContext.RecordAddressAccess(addressString);
            EvmUInt256 balance = program.ProgramContext.ExecutionStateService.GetTotalBalance(addressString);
            program.StackPush(balance);
            program.Step();
        }

        public EvmUInt256 GetTotalBalance(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalance(address.ConvertToEthereumChecksumAddress());
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
            var addressString = address.ConvertToEthereumChecksumAddress();
            program.ProgramContext.RecordAddressAccess(addressString);
            EvmUInt256 balance = await GetTotalBalanceAsync(program, address);
            program.StackPush(balance);
            program.Step();
        }

        public Task<EvmUInt256> GetTotalBalanceAsync(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(address.ConvertToEthereumChecksumAddress());
        }
#endif

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
                data = program.Memory.GetRange(index, length).ToArray();
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
            var address = program.ProgramContext.AddressContract;
            var transientStorage = program.ProgramContext.ExecutionStateService.TransientStorage;

            byte[] value = new byte[32];
            if (transientStorage.TryGetValue(address, out var addressStorage))
            {
                if (addressStorage.TryGetValue(key, out var val))
                {
                    value = val;
                }
            }

            program.StackPush(value);
            program.Step();
        }

        public void TStore(Program program)
        {
            var key = program.StackPopU256();
            var value = program.StackPop().PadTo32Bytes();
            var address = program.ProgramContext.AddressContract;
            var transientStorage = program.ProgramContext.ExecutionStateService.TransientStorage;

            if (program.ProgramContext.IsStatic)
            {
#if EVM_SYNC
                program.SetExecutionError(); return;
#else
                throw new Exception("TSTORE not allowed in static context");
#endif
            }

            if (!transientStorage.TryGetValue(address, out var addressStorage))
            {
                addressStorage = new Dictionary<EvmUInt256, byte[]>();
                transientStorage[address] = addressStorage;
            }
            addressStorage[key] = value;

            program.Step();
        }
    }

}
