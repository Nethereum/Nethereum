namespace Nethereum.EVM.Gas
{
    using Nethereum.Hex.HexConvertors.Extensions;
    using Nethereum.Util;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;

    public static class OpcodeGasTable
    {
        public static readonly Dictionary<Instruction, BigInteger> GasCosts = new()
    {
        // Arithmetic & Bitwise
        { Instruction.STOP, 0 },
        { Instruction.ADD, 3 },
        { Instruction.SUB, 3 },
        { Instruction.MUL, 5 },
        { Instruction.DIV, 5 },
        { Instruction.SDIV, 5 },
        { Instruction.MOD, 5 },
        { Instruction.SMOD, 5 },
        { Instruction.ADDMOD, 8 },
        { Instruction.MULMOD, 8 },
        { Instruction.SIGNEXTEND, 5 },
        { Instruction.LT, 3 },
        { Instruction.GT, 3 },
        { Instruction.SLT, 3 },
        { Instruction.SGT, 3 },
        { Instruction.EQ, 3 },
        { Instruction.ISZERO, 3 },
        { Instruction.AND, 3 },
        { Instruction.OR, 3 },
        { Instruction.XOR, 3 },
        { Instruction.NOT, 3 },
        { Instruction.BYTE, 3 },
        { Instruction.SHL, 3 },
        { Instruction.SHR, 3 },
        { Instruction.SAR, 3 },

        // Dynamic Gas
        { Instruction.EXP, -1 },
        { Instruction.KECCAK256, -1 },
        { Instruction.CALLDATACOPY, -1 },
        { Instruction.CODECOPY, -1 },
        { Instruction.EXTCODESIZE, -1 },
        { Instruction.EXTCODECOPY, -1 },
        { Instruction.EXTCODEHASH, -1 },
        { Instruction.RETURNDATACOPY, -1 },
        { Instruction.SLOAD, -1 },
        { Instruction.SSTORE, -1 },
        { Instruction.LOG0, -1 },
        { Instruction.LOG1, -1 },
        { Instruction.LOG2, -1 },
        { Instruction.LOG3, -1 },
        { Instruction.LOG4, -1 },
        { Instruction.CREATE, -1 },
        { Instruction.CREATE2, -1 },
        { Instruction.CALL, -1 },
        { Instruction.CALLCODE, -1 },
        { Instruction.DELEGATECALL, -1 },
        { Instruction.STATICCALL, -1 },
        { Instruction.MCOPY, -1 },

        // Context
        { Instruction.ADDRESS, 2 },
        { Instruction.BALANCE, -1 },
        { Instruction.ORIGIN, 2 },
        { Instruction.CALLER, 2 },
        { Instruction.CALLVALUE, 2 },
        { Instruction.CALLDATALOAD, 3 },
        { Instruction.CALLDATASIZE, 2 },
        { Instruction.CODESIZE, 2 },
        { Instruction.GASPRICE, 2 },
        { Instruction.BLOCKHASH, 20 },
        { Instruction.COINBASE, 2 },
        { Instruction.TIMESTAMP, 2 },
        { Instruction.NUMBER, 2 },
        { Instruction.DIFFICULTY, 2 },
        { Instruction.GASLIMIT, 2 },
        { Instruction.CHAINID, 2 },
        { Instruction.SELFBALANCE, 5 },
        { Instruction.BASEFEE, 2 },
        { Instruction.BLOBHASH, 3 },
        { Instruction.BLOBBASEFEE, 2 },

        // Flow / Memory
        { Instruction.POP, 2 },
        { Instruction.MLOAD, -1 },
        { Instruction.MSTORE, -1 },
        { Instruction.MSTORE8, -1 },
        { Instruction.JUMP, 8 },
        { Instruction.JUMPI, 10 },
        { Instruction.PC, 2 },
        { Instruction.MSIZE, 2 },
        { Instruction.GAS, 2 },
        { Instruction.JUMPDEST, 1 },
        { Instruction.TLOAD, 100 },
        { Instruction.TSTORE, 100 },

        // Return control
        { Instruction.RETURN, -1 },
        { Instruction.REVERT, -1 },
        { Instruction.INVALID, 0 },
        { Instruction.SELFDESTRUCT, 5000 },

        // PUSH0
        { Instruction.PUSH0, 2 },
    };

        static OpcodeGasTable()
        {
            // PUSH1–PUSH32
            for (int i = 1; i <= 32; i++)
                GasCosts[(Instruction)Enum.Parse(typeof(Instruction), $"PUSH{i}")] = 3;

            // DUP1–DUP16
            for (int i = 1; i <= 16; i++)
                GasCosts[(Instruction)Enum.Parse(typeof(Instruction), $"DUP{i}")] = 3;

            // SWAP1–SWAP16
            for (int i = 1; i <= 16; i++)
                GasCosts[(Instruction)Enum.Parse(typeof(Instruction), $"SWAP{i}")] = 3;
        }

        public static bool IsDynamic(Instruction instruction) =>
            GasCosts.TryGetValue(instruction, out var cost) && cost == -1;

        public static BigInteger GetStaticGas(Instruction instruction) =>
            GasCosts.TryGetValue(instruction, out var cost) && cost >= 0 ? cost : 0;

        public static async Task<BigInteger> GetGasCostAsync(Instruction instruction, Program program)
        {
            if (GasCosts.TryGetValue(instruction, out var cost))
            {
                if (cost >= 0)
                    return cost;

                return await GetDynamicGasCost(instruction, program);
            }
            return 0;
        }

        private static async Task<BigInteger> GetDynamicGasCost(Instruction instruction, Program program)
        {
            return instruction switch
            {
                Instruction.EXP => CalculateExpGas(program),
                Instruction.KECCAK256 => CalculateSha3Gas(program),
                Instruction.CALLDATACOPY => CalculateCallDataCopyGas(program),
                Instruction.CODECOPY => CalculateCopyGas(program),
                Instruction.EXTCODESIZE => CalculateExtCodeSizeGas(program),
                Instruction.EXTCODECOPY => CalculateExtCodeCopyGas(program),
                Instruction.EXTCODEHASH => CalculateExtCodeHashGas(program),
                Instruction.RETURNDATACOPY => CalculateReturnDataCopyGas(program),
                Instruction.SLOAD => CalculateSloadGas(program),
                Instruction.SSTORE => CalculateSStoreGas(program),
                Instruction.LOG0 => CalculateLogGas(program),
                Instruction.LOG1 => CalculateLogGas(program),
                Instruction.LOG2 => CalculateLogGas(program),
                Instruction.LOG3 => CalculateLogGas(program),
                Instruction.LOG4 => CalculateLogGas(program),
                Instruction.CREATE => CalculateCreateGas(program),
                Instruction.CREATE2 => CalculateCreate2Gas(program),
                Instruction.CALL => await CalculateCallGas(program),
                Instruction.CALLCODE => CalculateCallCodeGas(program),
                Instruction.DELEGATECALL => CalculateDelegateCallGas(program),
                Instruction.STATICCALL => CalculateStaticCallGas(program),
                Instruction.MCOPY => CalculateMCopyGas(program),
                Instruction.BALANCE => CalculateBalanceGas(program),
                Instruction.MLOAD => CalculateMLoadGas(program),
                Instruction.MSTORE => CalculateMStoreGas(program),
                Instruction.MSTORE8 => CalculateMStore8Gas(program),
                Instruction.RETURN => CalculateReturnGas(program),
                Instruction.REVERT => CalculateRevertGas(program),
                _ => throw new NotImplementedException($"Dynamic gas not implemented for {instruction}")
            };
        }

        // Dynamic gas handlers (stubs)
        private static BigInteger CalculateExpGas(Program program)
        {
            var baseVal = program.StackPeekAt(1); // Exponent is second in stack (topmost is base)
            var bytesInExponent = baseVal.Length;
            return 10 + 50 * bytesInExponent;
        }
        private static BigInteger CalculateSha3Gas(Program program)
        {
            var index = program.StackPeekAtAndConvertToUBigInteger(0); // memoryIndex is second from top
            var length = program.StackPeekAtAndConvertToUBigInteger(1); // indexInMemory is top of stack

            var lengthWords = (int)((length + 31) / 32); // ceil(len / 32)

            var memoryCost = program.CalculateMemoryExpansionGas(index, length);
            return 30 + (6 * lengthWords) + memoryCost;
        }

        private static BigInteger CalculateCallDataCopyGas(Program program)
        {
            return CalculateMemoryCopyGas(program, indexInMemoryPeekIndex: 0, lengthDataToCopyPeekIndex: 2);
        }

        private static BigInteger CalculateCopyGas(Program program)
        {
            return CalculateMemoryCopyGas(program, indexInMemoryPeekIndex: 0, lengthDataToCopyPeekIndex: 2);
        }

        private static BigInteger CalculateReturnDataCopyGas(Program program)
        {
            return CalculateMemoryCopyGas(program, indexInMemoryPeekIndex: 0, lengthDataToCopyPeekIndex: 2);
        }

        private static BigInteger CalculateMemoryCopyGas(Program program, int indexInMemoryPeekIndex, int lengthDataToCopyPeekIndex)
        {
            var indexInMemory = program.StackPeekAtAndConvertToUBigInteger(indexInMemoryPeekIndex);
            var lengthDataToCopy = program.StackPeekAtAndConvertToUBigInteger(lengthDataToCopyPeekIndex);

            var words = (int)((indexInMemory + 31) / 32);
            var memoryCost = program.CalculateMemoryExpansionGas(lengthDataToCopy, indexInMemory);

            return 3 + (3 * words) + memoryCost;
        }

        private static BigInteger CalculateExtCodeSizeGas(Program program)
        {
            var addressBytes = program.StackPeekAt(0);
            var isWarm = program.IsAddressWarm(addressBytes);

            if (!isWarm)
            {
                program.MarkAddressAsWarm(addressBytes); // mark for future ops
                return 2600;
            }

            return 100;
        }


        private static BigInteger CalculateExtCodeCopyGas(Program program)
        {
            var length = program.StackPeekAtAndConvertToUBigInteger(0);        
            var indexOfByteCode = program.StackPeekAtAndConvertToUBigInteger(1); 
            var indexInMemory = program.StackPeekAtAndConvertToUBigInteger(2); 
            var addressBytes = program.StackPeekAt(3);                         

            var words = (length + 31) / 32;
            var memoryCost = program.CalculateMemoryExpansionGas(indexInMemory, length);

            var isWarm = program.IsAddressWarm(addressBytes);
            if (!isWarm) program.MarkAddressAsWarm(addressBytes);

            var accessCost = isWarm ? 100 : 2600;

            return accessCost + (3 * words) + memoryCost;
        }


        private static BigInteger CalculateExtCodeHashGas(Program program)
        {
            var addressBytes = program.StackPeekAt(0); 

            var isWarm = program.IsAddressWarm(addressBytes);
            if (!isWarm)
                program.MarkAddressAsWarm(addressBytes);

            return isWarm ? 100 : 2600;
        }

        private static BigInteger CalculateSloadGas(Program program)
        {
            var key = program.StackPeekAtAndConvertToUBigInteger(0);
            var isWarm = program.IsStorageSlotWarm(key);

            if (!isWarm)
                program.MarkStorageSlotAsWarm(key);

            return isWarm ? 100 : 2100;
        }

        private static BigInteger CalculateSStoreGas(Program program)
        {
            var key = program.StackPeekAtAndConvertToUBigInteger(0);
            var newValue = program.StackPeekAt(1).PadTo32Bytes();

            var contextAddress = program.ProgramContext.AddressContract;
            var state = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(contextAddress);

            var isWarm = state.IsStorageKeyWarm(key);
            if (!isWarm)
            {
                state.MarkStorageKeyAsWarm(key);
            }

            BigInteger gasCost = isWarm ? 0 : 2100;

            var currentVal = state.GetStorageValue(key)?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
            var origVal = state.OriginalStorageValues.ContainsKey(key)
                ? state.OriginalStorageValues[key]?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32)
                : currentVal;

            if (ByteUtil.AreEqual(newValue, currentVal))
            {
                return gasCost + 100;
            }

            var isClean = ByteUtil.AreEqual(currentVal, origVal);

            if (isClean)
            {
                gasCost += ByteUtil.IsZero(origVal) ? 20000 : 2900;
            }
            else
            {
                gasCost += 100;
            }

            return gasCost;
        }
       
        private static BigInteger CalculateLogGas(Program program)
        {
            var opcode = program.GetCurrentInstruction().Instruction.Value;
            var numTopics = opcode - Instruction.LOG0; // LOG0 = A0, LOG4 = A4

            var memStart = program.StackPeekAtAndConvertToUBigInteger(0);
            var memLength = program.StackPeekAtAndConvertToUBigInteger(1); 

            var memoryCost = program.CalculateMemoryExpansionGas(memStart, memLength);
            var dataGas = 8 * memLength;
            var topicGas = 375 * numTopics;

            return 375 + topicGas + dataGas + memoryCost;
        }

        private static BigInteger CalculateCreateGas(Program program)
        {
            var memoryIndex = program.StackPeekAtAndConvertToUBigInteger(1); 
            var memoryLength = program.StackPeekAtAndConvertToUBigInteger(2);

            var memoryCost = program.CalculateMemoryExpansionGas(memoryIndex, memoryLength);

            return 32000 + memoryCost;
        }

        private static BigInteger CalculateCreate2Gas(Program program)
        {
            var memoryIndex = program.StackPeekAtAndConvertToUBigInteger(1);   
            var memoryLength = program.StackPeekAtAndConvertToUBigInteger(2);  

            var words = (memoryLength + 31) / 32;
            var memoryCost = program.CalculateMemoryExpansionGas(memoryIndex, memoryLength);

            return 32000 + (6 * words) + memoryCost;
        }


        private static async Task<BigInteger> CalculateCallGas(Program program)
        {
            var gas = program.StackPeekAtAndConvertToBigInteger(0);
            var toBytes = program.StackPeekAt(1);
            var value = program.StackPeekAtAndConvertToBigInteger(2);
            var inOffset = program.StackPeekAtAndConvertToBigInteger(3);
            var inSize = program.StackPeekAtAndConvertToBigInteger(4);
            var outOffset = program.StackPeekAtAndConvertToBigInteger(5);
            var outSize = program.StackPeekAtAndConvertToBigInteger(6);

            var to = toBytes.ConvertToEthereumChecksumAddress();
            // Cold access cost
            var isWarm = program.IsAddressWarm(toBytes);
            if (!isWarm) program.MarkAddressAsWarm(toBytes);
            var accountState = await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(to);

            // Memory expansion cost - take MAX of input and output regions, not sum
            // Per Yellow Paper: μ'_i ≡ M(M(μ_i, μ_s[3], μ_s[4]), μ_s[5], μ_s[6])
            var inEnd = inSize > 0 ? inOffset + inSize : BigInteger.Zero;
            var outEnd = outSize > 0 ? outOffset + outSize : BigInteger.Zero;
            var maxEnd = BigInteger.Max(inEnd, outEnd);
            var memCost = maxEnd > 0 ? program.CalculateMemoryExpansionGas(0, maxEnd) : BigInteger.Zero;

            var accessCost = isWarm ? 100 : 2600;

            var baseGas = accessCost + memCost;

            if (value > 0)
            {
                baseGas += 9000;

                // If account was empty before, extra cost for account creation
                if (accountState.Balance.IsZero() && accountState.Nonce == 0 && (accountState.Code == null || accountState.Code.Length == 0))
                {
                    baseGas += 25000;
                }
            }

            return baseGas;
        }

        private static BigInteger CalculateCallCodeGas(Program program)
        {
            var gas = program.StackPeekAtAndConvertToBigInteger(0);
            var toBytes = program.StackPeekAt(1);
            var value = program.StackPeekAtAndConvertToBigInteger(2);
            var inOffset = program.StackPeekAtAndConvertToBigInteger(3);
            var inSize = program.StackPeekAtAndConvertToBigInteger(4);
            var outOffset = program.StackPeekAtAndConvertToBigInteger(5);
            var outSize = program.StackPeekAtAndConvertToBigInteger(6);

            var isWarm = program.IsAddressWarm(toBytes);
            if (!isWarm) program.MarkAddressAsWarm(toBytes);

            // Memory expansion cost - take MAX of input and output regions
            var inEnd = inSize > 0 ? inOffset + inSize : BigInteger.Zero;
            var outEnd = outSize > 0 ? outOffset + outSize : BigInteger.Zero;
            var maxEnd = BigInteger.Max(inEnd, outEnd);
            var memCost = maxEnd > 0 ? program.CalculateMemoryExpansionGas(0, maxEnd) : BigInteger.Zero;

            var accessCost = isWarm ? 100 : 2600;
            var baseGas = accessCost + memCost;

            if (value > 0)
            {
                baseGas += 9000;
                // No +25000 for account creation in CALLCODE
            }

            return baseGas;
        }

        private static BigInteger CalculateDelegateCallGas(Program program)
        {
            var gas = program.StackPeekAtAndConvertToBigInteger(0);
            var toBytes = program.StackPeekAt(1);
            var inOffset = program.StackPeekAtAndConvertToBigInteger(2);
            var inSize = program.StackPeekAtAndConvertToBigInteger(3);
            var outOffset = program.StackPeekAtAndConvertToBigInteger(4);
            var outSize = program.StackPeekAtAndConvertToBigInteger(5);

            var isWarm = program.IsAddressWarm(toBytes);
            if (!isWarm) program.MarkAddressAsWarm(toBytes);

            // Memory expansion cost - take MAX of input and output regions
            var inEnd = inSize > 0 ? inOffset + inSize : BigInteger.Zero;
            var outEnd = outSize > 0 ? outOffset + outSize : BigInteger.Zero;
            var maxEnd = BigInteger.Max(inEnd, outEnd);
            var memCost = maxEnd > 0 ? program.CalculateMemoryExpansionGas(0, maxEnd) : BigInteger.Zero;

            var accessCost = isWarm ? 100 : 2600;
            return accessCost + memCost;
        }

        private static BigInteger CalculateStaticCallGas(Program program)
        {
            var gas = program.StackPeekAtAndConvertToBigInteger(0);
            var toBytes = program.StackPeekAt(1);
            var inOffset = program.StackPeekAtAndConvertToBigInteger(2);
            var inSize = program.StackPeekAtAndConvertToBigInteger(3);
            var outOffset = program.StackPeekAtAndConvertToBigInteger(4);
            var outSize = program.StackPeekAtAndConvertToBigInteger(5);

            var isWarm = program.IsAddressWarm(toBytes);
            if (!isWarm) program.MarkAddressAsWarm(toBytes);

            // Memory expansion cost - take MAX of input and output regions
            var inEnd = inSize > 0 ? inOffset + inSize : BigInteger.Zero;
            var outEnd = outSize > 0 ? outOffset + outSize : BigInteger.Zero;
            var maxEnd = BigInteger.Max(inEnd, outEnd);
            var memCost = maxEnd > 0 ? program.CalculateMemoryExpansionGas(0, maxEnd) : BigInteger.Zero;

            var accessCost = isWarm ? 100 : 2600;
            return accessCost + memCost;
        }

        private static BigInteger CalculateMCopyGas(Program program)
        {
            var destOffset = program.StackPeekAtAndConvertToUBigInteger(0); // top
            var srcOffset = program.StackPeekAtAndConvertToUBigInteger(1);  // second
            var length = program.StackPeekAtAndConvertToUBigInteger(2);     // third

            var words = (length + 31) / 32;
            var memoryCost =
                program.CalculateMemoryExpansionGas(destOffset, length) +
                program.CalculateMemoryExpansionGas(srcOffset, length);

            return 3 + (3 * words) + memoryCost;
        }
        
        private static BigInteger CalculateBalanceGas(Program program)
        {
            var addressBytes = program.StackPeekAt(0); // top of stack

            var isWarm = program.IsAddressWarm(addressBytes);
            if (!isWarm) program.MarkAddressAsWarm(addressBytes);

            return isWarm ? 100 : 2600;
        }

        private static BigInteger CalculateMLoadGas(Program program)
        {
            var offset = program.StackPeekAtAndConvertToUBigInteger(0);
            return 3 + program.CalculateMemoryExpansionGas(offset, 32);
        }

        private static BigInteger CalculateMStoreGas(Program program)
        {
            var offset = program.StackPeekAtAndConvertToUBigInteger(0);
            return 3 + program.CalculateMemoryExpansionGas(offset, 32);
        }

        private static BigInteger CalculateMStore8Gas(Program program)
        {
            var offset = program.StackPeekAtAndConvertToUBigInteger(0);
            return 3 + program.CalculateMemoryExpansionGas(offset, 1);
        }

        private static BigInteger CalculateReturnGas(Program program)
        {
            var offset = program.StackPeekAtAndConvertToUBigInteger(0);
            var length = program.StackPeekAtAndConvertToUBigInteger(1);
            return program.CalculateMemoryExpansionGas(offset, length);
        }

        private static BigInteger CalculateRevertGas(Program program)
        {
            var offset = program.StackPeekAtAndConvertToUBigInteger(0);
            var length = program.StackPeekAtAndConvertToUBigInteger(1);
            return program.CalculateMemoryExpansionGas(offset, length);
        }

    }
    

}