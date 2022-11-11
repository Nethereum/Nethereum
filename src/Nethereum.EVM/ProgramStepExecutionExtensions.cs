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

namespace Nethereum.EVM
{
    public static class ProgramStepExecutionExtensions
    {
                
        public static byte[] PadTo32Bytes(this byte[] bytesToPad)
        {
            var ret = new byte[32];

            for (var i = 0; i < ret.Length; i++)
                ret[i] = 0;
            Array.Copy(bytesToPad, 0, ret, 32 - bytesToPad.Length, bytesToPad.Length);

            return ret;
        }

        public static void WriteToMemory(this Program program, int index, byte[] data)
        {
            WriteToMemory(program, index, data.Length, data);
        }

        public static void WriteToMemory(this Program program, int index, int totalSize, byte[] data)
        {
            if (totalSize == 0) return;
            //totalSize might be bigger than data length so memory will be extended to match

            if(data == null) data = new byte[0];
            //if (data.Length > totalSize) totalSize = data.Length;
            int newSize = index + totalSize;

            if (newSize > program.Memory.Count)
            {
                program.Memory.AddRange(new byte[newSize - program.Memory.Count]);
            }

            for (int i = 0; i < totalSize; i++)
            {
                program.Memory[index + i] = data[i];
            }
        }

        public static void CallDataLoad(this Program program)
        {
            var index = (int)program.StackPopAndConvertToBigInteger();
            var dataInput = program.ProgramContext.DataInput;
            if (index > dataInput.Length)
            {
                program.StackPush(0);
            }
            else
            {
                //ensure only 32 bytes
                int size = Math.Min(dataInput.Length - index, 32);
                byte[] dataLoaded = new byte[32];
                Array.Copy(dataInput, index, dataLoaded, 0, size);
                program.StackPush(dataLoaded);
            }
            program.Step();
        }

        public static async Task BlockHashAsync(this Program program)
        {
            var blockNumber = program.StackPopAndConvertToBigInteger();
            var blockHash = await program.ProgramContext.ExecutionStateService.NodeDataService.GetBlockHashAsync(blockNumber);
            program.StackPush(blockHash);
            program.Step();
        }

        public static async Task ExtCodeHashAsync(this Program program)
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

        public static async Task<List<ProgramTrace>> CreateAsync(this Program program,int vmExecutionStep, bool traceEnabled)
        {
            var value = program.StackPopAndConvertToBigInteger();
            var memoryIndex = (int)program.StackPopAndConvertToBigInteger();
            var memoryLength = (int)program.StackPopAndConvertToBigInteger();
            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);
            var newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, nonce);
            var byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();

            return await CreateContractAsync(program, vmExecutionStep, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        public static async Task<List<ProgramTrace>> Create2Async(this Program program, int vmExecutionStep, bool traceEnabled)
        {
            var value = program.StackPopAndConvertToBigInteger();
            var memoryIndex = (int)program.StackPopAndConvertToBigInteger();
            var memoryLength = (int)program.StackPopAndConvertToBigInteger();
            var salt = program.StackPop();
            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);
            
            var byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();
            var newContractAddress = ContractUtils.CalculateCreate2Address(contractAddress, salt.ToHex(), byteCode.ToHex());
            return await CreateContractAsync(program, vmExecutionStep, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        private static async Task<List<ProgramTrace>> CreateContractAsync(Program program, int vmExecutionStep, bool traceEnabled, BigInteger value, string contractAddress, BigInteger nonce, string newContractAddress, byte[] byteCode)
        {
            program.ProgramContext.ExecutionStateService.SetNonce(contractAddress, nonce + 1);


            var callInput = new CallInput()
            {
                From = contractAddress,
                Value = new HexBigInteger(value),
                To = newContractAddress
            };

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);


            var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressCaller,
                (long)program.ProgramContext.BlockNumber, (long)program.ProgramContext.Timestamp, program.ProgramContext.Coinbase, (long)program.ProgramContext.BaseFee);
            var callProgram = new Program(byteCode, programContext);
            var vm = new EVMSimulator();
            var trace = await vm.ExecuteAsync(callProgram, vmExecutionStep, traceEnabled);

            if (callProgram.ProgramResult.IsRevert == false)
            {
                program.StackPush(new AddressType().Encode(newContractAddress));
                var code = callProgram.ProgramResult.Result;
                program.ProgramResult.Result = callProgram.ProgramResult.Result;
                program.ProgramContext.ExecutionStateService.SaveCode(newContractAddress, code);
                program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                program.ProgramResult.InnerCalls.Add(callInput);
                program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);
                program.ProgramResult.CreatedContractAccounts.Add(newContractAddress);
                program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, 1);
                program.Step();
            }
            else
            {
                program.Stop();
            }

            return trace;
        }

        public static async Task SelfDestructAsync(this Program program)
        {
            var addressReceiverFunds = program.StackPop();
            var addressReceiverFundsHex = addressReceiverFunds.ConvertToEthereumChecksumAddress();
  
            var balanceContract = await GetTotalBalanceAsync(program, program.ProgramContext.AddressContractEncoded);
            if (addressReceiverFundsHex.IsTheSameAddress(program.ProgramContext.AddressContract))
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -balanceContract);
            }
            else
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -balanceContract);
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(addressReceiverFundsHex, balanceContract);
            }

            program.ProgramResult.DeletedContractAccounts.Add(program.ProgramContext.AddressContract);
            program.Stop();
        }


        public static async Task<List<ProgramTrace>> StaticCallAsync(this Program program, int vmExecutionStep, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = 0;
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();
            return await GenericCallAsync(program, vmExecutionStep, traceEnabled, gas, codeAddress, value, from, to);
        }

        public static async Task<List<ProgramTrace>> DelegateCallAsync(this Program program, int vmExecutionStep, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.ProgramContext.Value; // value is the same
            var from = program.ProgramContext.AddressCaller; //sender is the original caller
            var to = program.ProgramContext.AddressContract; // keeping the same storage
            return await GenericCallAsync(program, vmExecutionStep, traceEnabled, gas, codeAddress, value, from, to);
        }

        public static async Task<List<ProgramTrace>> CallCodeAsync(this Program program, int vmExecutionStep, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToBigInteger(); 
            var from = program.ProgramContext.AddressContract; //sender is the current contract
            var to = program.ProgramContext.AddressContract; // keeping the same storage
            return await GenericCallAsync(program, vmExecutionStep, traceEnabled, gas, codeAddress, value, from, to);
        }

        public static async Task<List<ProgramTrace>> CallAsync(this Program program, int vmExecutionStep, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToBigInteger();
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();

            return await GenericCallAsync(program, vmExecutionStep, traceEnabled, gas, codeAddress, value, from, to);

        }

        private static async Task<List<ProgramTrace>> GenericCallAsync(Program program, int vmExecutionStep, bool traceEnabled, BigInteger gas, byte[] codeAddress, BigInteger value, string from, string to)
        {
            var dataInputIndex = (int)program.StackPopAndConvertToBigInteger();
            var dataInputLength = (int)program.StackPopAndConvertToBigInteger();

            var resultMemoryDataIndex = (int)program.StackPopAndConvertToBigInteger();
            var resultMemoryDataLength = (int)program.StackPopAndConvertToBigInteger();

            var dataInput = new byte[] { };
            
            if (dataInputLength != 0)
            {
                dataInput = new byte[dataInputLength];
                if (dataInputIndex + dataInputLength > program.Memory.Count)
                {
                    var dataToCopy = program.Memory.Skip(dataInputIndex).ToArray();
                    Array.Copy(dataToCopy, dataInput, dataToCopy.Length);
                }
                else
                {
                    dataInput = program.Memory.GetRange(dataInputIndex, dataInputLength).ToArray();
                }
            }
           
               
            var callInput = new CallInput()
            {
                From = from,
                Value = new HexBigInteger(value),
                To = to,
                Data = dataInput.ToHex(),
                Gas = new HexBigInteger(gas)
            };

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);

            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(codeAddress.ConvertToEthereumChecksumAddress());
            if (byteCode.Length == 0) // calling / transfering a non contract account
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);
                program.StackPush(1);
                program.Step();
                return new List<ProgramTrace>();
            }
            else
            {
                var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressCaller,
                    (long)program.ProgramContext.BlockNumber, (long)program.ProgramContext.Timestamp, program.ProgramContext.Coinbase, (long)program.ProgramContext.BaseFee);
                var callProgram = new Program(byteCode, programContext);
                var vm = new EVMSimulator();
                var trace = await vm.ExecuteAsync(callProgram, vmExecutionStep, traceEnabled);

                if (callProgram.ProgramResult.IsRevert == false)
                {
                    program.StackPush(1);
                    var result = callProgram.ProgramResult.Result;
                    program.ProgramResult.Result = callProgram.ProgramResult.Result;
                    WriteToMemory(program, resultMemoryDataIndex, resultMemoryDataLength, result);
                    program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                    program.ProgramResult.InnerCalls.Add(callInput);
                    program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);
                    program.Step();
                }
                else
                {
                    program.Stop();
                }
                return trace;
            }
            
        }

        public static async Task BalanceAsync(this Program program)
        {
            var address = program.StackPop();
            await BalanceStepAsync(program, address);
        }

        public static async Task SelfBalanceAsync(this Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            await BalanceStepAsync(program, address);
        }

        private static async Task BalanceStepAsync(Program program, byte[] address)
        {
            BigInteger balance = await GetTotalBalanceAsync(program, address);
            program.StackPush(balance);
            program.Step();
        }

        public static  Task<BigInteger> GetTotalBalanceAsync(Program program, byte[] address)
        {
            return program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(address.ConvertToEthereumChecksumAddress());
        }

        public static void Address(this Program program)
        {
            var address = program.ProgramContext.AddressContractEncoded;
            program.StackPush(address);
            program.Step();
        }

        public static void CallValue(this Program program)
        {
            var value = program.ProgramContext.Value;
            program.StackPush(value);
            program.Step();
        }

        public static void Origin(this Program program)
        {
            var address = program.ProgramContext.AddressOriginEncoded;
            program.StackPush(address);
            program.Step();
        }

        public static void Log(this Program program, int numberTopics)
        {
            var address = program.ProgramContext.AddressContract;

            var memStart = (int)program.StackPopAndConvertToBigInteger();
            var memOffset = (int)program.StackPopAndConvertToBigInteger();

            var topics = new List<string>();
            for (int i = 0; i < numberTopics; ++i)
            {
                var topic = program.StackPop();
                topics.Add(topic.ToHex());
            }

            byte[] data = program.Memory.GetRange(memStart, memOffset).ToArray();

            var filterLog = new FilterLog
            {
                Address = address,
                Topics = topics.ToArray(),
                Data = data.ToHex()
            };
            program.ProgramResult.Logs.Add(filterLog);
            program.Step();
        }

        public static void Caller(this Program program)
        {
            var address = program.ProgramContext.AddressCallerEncoded;
            program.StackPush(address);
            program.Step();
        }

        public static void CallDataSize(this Program program)
        {
            program.StackPush(program.ProgramContext.DataInput.Length);
            program.Step();
        }

        public static async Task<BigInteger> SLoad(this Program program)
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

        public static void SStore(this Program program)
        {
            var key = program.StackPopAndConvertToUBigInteger();
            var storageValue = program.StackPop();
            program.ProgramContext.SaveToStorage(key, storageValue);
            program.Step();
        }

        public static void ReturnDataCopy(this Program program)
        {
            var memoryIndex = (int)program.StackPopAndConvertToBigInteger();
            var resultIndex = (int)program.StackPopAndConvertToBigInteger();
            var lengthResult = (int)program.StackPopAndConvertToBigInteger();
            var result = program.ProgramResult.Result;
            if (result == null)
            {
                program.WriteToMemory(memoryIndex, lengthResult, new byte[] { });
            }
            else
            {

                var size = result.Length - resultIndex;

                if (lengthResult < size)
                {
                    size = lengthResult;
                }

                var dataToCopy = new byte[size];
                Array.Copy(result, resultIndex, dataToCopy, 0, size);

                program.WriteToMemory(memoryIndex, lengthResult, dataToCopy);
            }
            program.Step();
        }

        public static void ReturnDataSize(this Program program)
        {
            var result = program.ProgramResult.Result;
            var length = 0;
            if (result != null)
            {
                length = result.Length;
            }
            program.StackPush(length);
            program.Step();
        }

        public static void CallDataCopy(this Program program)
        {
            var indexInMemory = (int)program.StackPopAndConvertToBigInteger();
            var indexOfData = (int)program.StackPopAndConvertToBigInteger();
            var lengthDataToCopy = (int)program.StackPopAndConvertToBigInteger();
            var dataInput = program.ProgramContext.DataInput;
            if(indexOfData > dataInput.Length)
            {
                WriteToMemory(program, indexInMemory, lengthDataToCopy, new byte[0]);
            }
            else
            {
                var size = dataInput.Length - indexOfData;

                if (lengthDataToCopy < size)
                {
                    size = lengthDataToCopy;
                }

                var dataToCopy = new byte[size];
                Array.Copy(dataInput, indexOfData, dataToCopy, 0, size);
                WriteToMemory(program, indexInMemory, lengthDataToCopy, dataToCopy);
            }
            
           program.Step();
        }

        public static void CodeCopy(this Program program)
        {
            var byteCode = program.ByteCode;
            var byteCodeLength = byteCode.Length;

            int indexInMemory = (int)program.StackPopAndConvertToBigInteger();
            int indexOfByteCode = (int)program.StackPopAndConvertToBigInteger();
            int lengthOfByteCodeToCopy = (int)program.StackPopAndConvertToBigInteger();
            CodeCopy(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
        }

        private static void CodeCopy(Program program, byte[] byteCode, int byteCodeLength, int indexInMemory, int indexOfByteCode, int lengthOfByteCodeToCopy)
        {
            byte[] byteCodeCopy = new byte[lengthOfByteCodeToCopy];

            if (indexOfByteCode < byteCodeLength)
            {
                var totalSizeToBeCopied = lengthOfByteCodeToCopy;
                if ((indexOfByteCode + lengthOfByteCodeToCopy) > byteCodeLength)
                {
                    totalSizeToBeCopied = byteCodeLength - indexOfByteCode;
                }

                Array.Copy(byteCode, indexOfByteCode, byteCodeCopy, 0, totalSizeToBeCopied);
            }

            WriteToMemory(program, indexInMemory, lengthOfByteCodeToCopy, byteCodeCopy);
            program.Step();
        }

        public static void Revert(this Program program)
        {
            program.ProgramResult.IsRevert = true;
            program.Step();
            program.Stop();
        }

        public static void Return(this Program program)
        {
            var index = (int)program.StackPopAndConvertToBigInteger();
            var size = (int)program.StackPopAndConvertToBigInteger();

            byte[] result = program.Memory.GetRange(index, size).ToArray();
            program.ProgramResult.Result = result;

            program.Step();
            program.Stop();
        }

        public static async Task ExtCodeCopyAsync(this Program program)
        {
            var address = program.StackPop();
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(address.ConvertToEthereumChecksumAddress());

            var byteCodeLength = byteCode.Length;
            int indexInMemory = (int)program.StackPopAndConvertToBigInteger();
            int indexOfByteCode = (int)program.StackPopAndConvertToBigInteger();
            int lengthOfByteCodeToCopy = (int)program.StackPopAndConvertToBigInteger();

            CodeCopy(program, byteCode, byteCodeLength, indexInMemory, indexOfByteCode, lengthOfByteCodeToCopy);
        }

        public static void CodeSize(this Program program)
        {
            var size = program.ByteCode.Length;
            program.StackPush(size);
            program.Step();
        }

        public static async Task ExtCodeSizeAsync(this Program program)
        {
            var address = program.StackPop();
            var code = await program.ProgramContext.ExecutionStateService.GetCodeAsync(address.ConvertToEthereumChecksumAddress());
            program.StackPush(code.Length);
            program.Step();
        }

        public static void MLoad(this Program program)
        {
            var index = (int)program.StackPopAndConvertToBigInteger();
           
            var data = new byte[32];
            if (index + 32 > program.Memory.Count)
            {
                var dataToCopy = program.Memory.Skip(index).ToArray();
                Array.Copy(dataToCopy, data, dataToCopy.Length);
            }
            else
            {
               data = program.Memory.GetRange(index, 32).ToArray();
            }
           
            program.StackPush(data);
            program.Step();
        }

        public static void MStore8(this Program program)
        {
            var index = program.StackPopAndConvertToBigInteger();
            var value = program.StackPop();
            WriteToMemory(program, (int)index, new byte[] { value[31] });
            program.Step();
        }

        public static void MStore(this Program program)
        {
            var index = program.StackPopAndConvertToBigInteger();
            var value = program.StackPop();
            WriteToMemory(program, (int)index, value);
            program.Step();
        }

        public static void SHA3(this Program program)
        {
            var index = program.StackPopAndConvertToBigInteger();
            var lenght = program.StackPopAndConvertToBigInteger();
            var data = program.Memory.GetRange((int)index, (int)lenght);
            var encoded = Util.Sha3Keccack.Current.CalculateHash(data.ToArray());
            program.StackPush(encoded);
            program.Step();
        }

        public static void Byte(this Program program)
        {
            var pos = program.StackPopAndConvertToBigInteger();
            var byteBytes = program.StackPop();
            var word = PadTo32Bytes(byteBytes);

            var result = pos < 32 ? new[] { word[(int)pos] } : new byte[0];
            program.StackPush(result);
            program.Step();
        }

        public static void And(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first & second);
            program.Step();
        }

        public static void ShiftLeft(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(second << (int)first);
            program.Step();
        }

        public static void ShiftRight(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            Debug.WriteLine((second >> (int)first).ToBytesForRLPEncoding().ToHex());
            program.StackPush(second >> (int)first);
            program.Step();
        }

        public static void ShiftSignedRight(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(second >> (int)first);
            program.Step();
        }

        public static void OrSimple(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first | second);
            program.Step();
        }

        public static void Or(this Program program)
        {
            var first = program.StackPop();
            var second = program.StackPop();
            var convertedValue = new byte[second.Length];
            for (int i = 0; i < second.Length; i++)
                convertedValue[i] = (byte)(second[i] | first[i]);
            program.StackPush(convertedValue.PadTo32Bytes());
            program.Step();
        }

        public static void XorSimple(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first^ second);
            program.Step();
        }

        public static void Xor(this Program program)
        {
            var first = program.StackPop();
            var second = program.StackPop();
            var convertedValue = new byte[second.Length];
            for (int i = 0; i < second.Length; i++)
                convertedValue[i] = (byte)(second[i] ^ first[i]);
            program.StackPush(convertedValue.PadTo32Bytes());
            program.Step();
        }

        public static void Not(this Program program)
        {
            var value = program.StackPopAndConvertToBigInteger();
            program.StackPush(~value);
            program.Step();
        }

        public static void LT(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first < second ? 1 : 0);
            program.Step();
        }

        public static void EQ(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(first == second ? 1 : 0);
            program.Step();
        }

        public static void IsZero(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            program.StackPush(first == 0 ? 1 : 0);
            program.Step();
        }

        public static void SLT(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(first < second ? 1 : 0);
            program.Step();
        }

        public static void GT(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first > second ? 1 : 0);
            program.Step();
        }

        public static void SGT(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(first > second ? 1 : 0);
            program.Step();
        }

        public static void AddMod(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var third = program.StackPopAndConvertToBigInteger();
            var result = (first + second) % third;
            program.StackPush(result);
            program.Step();
        }

        public static void MulMod(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var third = program.StackPopAndConvertToBigInteger();
            var result = (first * second) % third;
            program.StackPush(result);
            program.Step();
        }

        public static void Add(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = first + second;
            
            program.StackPush(result);
            program.Step();
        }

        public static void Exp(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = BigInteger.ModPow(first, second, BigInteger.Pow(2, 256));
            program.StackPush(result);
            program.Step();
        }

        public static void Mul(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = first * second;
            program.StackPush(result);
            program.Step();
        }

        public static void Sub(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = first - second;
            program.StackPush(result);
            program.Step();
        }

        public static void Div(this Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            if (second == 0)
            {
                program.StackPush(0);
                return;
            }
            var result = first / second;
            program.StackPush(result);
            program.Step();
        }

        public static void SDiv(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            if (second == 0)
            {
                program.StackPush(0);
                return;
            }
            var result = first / second;
            program.StackPush(result);
            program.Step();
        }

        public static void Mod(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = first % second;
            program.StackPush(result);
            program.Step();
        }

        public static void SMod(this Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            var result = first % second;
            program.StackPush(result);
            program.Step();
        }

        public static BigInteger StackPopAndConvertToBigInteger(this Program program)
        {
            var bytes = program.StackPop();
            return new IntType("int256").Decode<BigInteger>(bytes);
        }

        public static BigInteger StackPopAndConvertToUBigInteger(this Program program)
        {
            var bytes = program.StackPop();
            return new IntType("uint256").Decode<BigInteger>(bytes);
        }

        public static void StackPush(this Program program, BigInteger value)
        {
            program.StackPush(new IntTypeEncoder(false, 256).EncodeInt(value, 32, false, true));
        }

    }
}