using System;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using System.Threading.Tasks;
using System.Reflection.Emit;
using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;
using System.Diagnostics;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json.Linq;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.Signer;
using System.Collections;

namespace Nethereum.EVM.Execution
{

    public class EvmBlockchainCurrentContractContextExecution
    {
        public async Task BlockHashAsync(Program program)
        {
            var blockNumber = program.StackPopAndConvertToBigInteger();
            var blockHash = await program.ProgramContext.ExecutionStateService.NodeDataService.GetBlockHashAsync(blockNumber);
            program.StackPush(blockHash);
            program.Step();
        }

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
            BigInteger balance = await GetTotalBalanceAsync(program, address);
            program.StackPush(balance);
            program.Step();
        }

        public Task<BigInteger> GetTotalBalanceAsync(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(address.ConvertToEthereumChecksumAddress());
        }

        public void Address(Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            program.StackPush(address);
            program.Step();
        }

        public void SHA3(Program program)
        {
            var index = program.StackPopAndConvertToUBigInteger();
            var lenght = program.StackPopAndConvertToUBigInteger();
            var data = program.Memory.GetRange((int)index, (int)lenght);
            var encoded = Sha3Keccack.Current.CalculateHash(data.ToArray());
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
            var timestamp = program.ProgramContext.Timestamp;
            program.StackPush(timestamp);
            program.Step();
        }

        public void BlockNumber(Program program)
        {
            var blockNumber = program.ProgramContext.BlockNumber;
            program.StackPush(blockNumber);
            program.Step();
        }

        public void GasPrice(Program program)
        {
            var gasPrice = program.ProgramContext.GasPrice;
            program.StackPush(gasPrice);
            program.Step();
        }

        public void GasLimit(Program program)
        {
            var gaslimit = program.ProgramContext.GasLimit;
            program.StackPush(gaslimit);
            program.Step();
        }
        public void Gas(Program program)
        {
            var gas = program.ProgramContext.Gas;
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
    }

}