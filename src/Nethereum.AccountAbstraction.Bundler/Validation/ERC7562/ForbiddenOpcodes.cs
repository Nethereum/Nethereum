using System.Collections.Generic;
using Nethereum.EVM;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public static class ForbiddenOpcodes
    {
        // [OP-011] Always forbidden during validation phase
        public static readonly HashSet<Instruction> AlwaysForbidden = new()
        {
            Instruction.ORIGIN,
            Instruction.GASPRICE,
            Instruction.BLOCKHASH,
            Instruction.COINBASE,
            Instruction.TIMESTAMP,
            Instruction.NUMBER,
            Instruction.DIFFICULTY,
            Instruction.GASLIMIT,
            Instruction.BASEFEE,
            Instruction.BLOBHASH,
            Instruction.BLOBBASEFEE,
            Instruction.INVALID,
            Instruction.SELFDESTRUCT,
        };

        // [OP-080] Forbidden unless entity is staked
        public static readonly HashSet<Instruction> StakedOnlyOpcodes = new()
        {
            Instruction.BALANCE,
            Instruction.SELFBALANCE,
        };

        // Opcodes requiring special validation logic
        public static readonly HashSet<Instruction> ConditionalOpcodes = new()
        {
            Instruction.GAS,
            Instruction.CREATE,
            Instruction.CREATE2,
        };

        // Call-type opcodes (for GAS validation - OP-012)
        public static readonly HashSet<Instruction> CallOpcodes = new()
        {
            Instruction.CALL,
            Instruction.CALLCODE,
            Instruction.DELEGATECALL,
            Instruction.STATICCALL,
        };

        // Storage opcodes (for STO-xxx rules)
        public static readonly HashSet<Instruction> StorageOpcodes = new()
        {
            Instruction.SLOAD,
            Instruction.SSTORE,
        };

        // [OP-070] Transient storage follows same rules as SLOAD/SSTORE
        public static readonly HashSet<Instruction> TransientStorageOpcodes = new()
        {
            Instruction.TLOAD,
            Instruction.TSTORE,
        };

        // External code access opcodes (for OP-041)
        public static readonly HashSet<Instruction> ExtCodeOpcodes = new()
        {
            Instruction.EXTCODESIZE,
            Instruction.EXTCODECOPY,
            Instruction.EXTCODEHASH,
        };

        // All valid EVM opcodes - for OP-013 (unassigned opcode check)
        public static readonly HashSet<Instruction> ValidOpcodes = new()
        {
            Instruction.STOP, Instruction.ADD, Instruction.MUL, Instruction.SUB, Instruction.DIV,
            Instruction.SDIV, Instruction.MOD, Instruction.SMOD, Instruction.ADDMOD, Instruction.MULMOD,
            Instruction.EXP, Instruction.SIGNEXTEND, Instruction.LT, Instruction.GT, Instruction.SLT,
            Instruction.SGT, Instruction.EQ, Instruction.ISZERO, Instruction.AND, Instruction.OR,
            Instruction.XOR, Instruction.NOT, Instruction.BYTE, Instruction.SHL, Instruction.SHR,
            Instruction.SAR, Instruction.KECCAK256, Instruction.ADDRESS, Instruction.BALANCE, Instruction.ORIGIN,
            Instruction.CALLER, Instruction.CALLVALUE, Instruction.CALLDATALOAD, Instruction.CALLDATASIZE,
            Instruction.CALLDATACOPY, Instruction.CODESIZE, Instruction.CODECOPY, Instruction.GASPRICE,
            Instruction.EXTCODESIZE, Instruction.EXTCODECOPY, Instruction.RETURNDATASIZE, Instruction.RETURNDATACOPY,
            Instruction.EXTCODEHASH, Instruction.BLOCKHASH, Instruction.COINBASE, Instruction.TIMESTAMP,
            Instruction.NUMBER, Instruction.DIFFICULTY, Instruction.GASLIMIT, Instruction.CHAINID,
            Instruction.SELFBALANCE, Instruction.BASEFEE, Instruction.BLOBHASH, Instruction.BLOBBASEFEE,
            Instruction.POP, Instruction.MLOAD, Instruction.MSTORE, Instruction.MSTORE8, Instruction.SLOAD,
            Instruction.SSTORE, Instruction.JUMP, Instruction.JUMPI, Instruction.PC, Instruction.MSIZE,
            Instruction.GAS, Instruction.JUMPDEST, Instruction.TLOAD, Instruction.TSTORE, Instruction.MCOPY,
            Instruction.PUSH0, Instruction.PUSH1, Instruction.PUSH2, Instruction.PUSH3, Instruction.PUSH4,
            Instruction.PUSH5, Instruction.PUSH6, Instruction.PUSH7, Instruction.PUSH8, Instruction.PUSH9,
            Instruction.PUSH10, Instruction.PUSH11, Instruction.PUSH12, Instruction.PUSH13, Instruction.PUSH14,
            Instruction.PUSH15, Instruction.PUSH16, Instruction.PUSH17, Instruction.PUSH18, Instruction.PUSH19,
            Instruction.PUSH20, Instruction.PUSH21, Instruction.PUSH22, Instruction.PUSH23, Instruction.PUSH24,
            Instruction.PUSH25, Instruction.PUSH26, Instruction.PUSH27, Instruction.PUSH28, Instruction.PUSH29,
            Instruction.PUSH30, Instruction.PUSH31, Instruction.PUSH32, Instruction.DUP1, Instruction.DUP2,
            Instruction.DUP3, Instruction.DUP4, Instruction.DUP5, Instruction.DUP6, Instruction.DUP7,
            Instruction.DUP8, Instruction.DUP9, Instruction.DUP10, Instruction.DUP11, Instruction.DUP12,
            Instruction.DUP13, Instruction.DUP14, Instruction.DUP15, Instruction.DUP16, Instruction.SWAP1,
            Instruction.SWAP2, Instruction.SWAP3, Instruction.SWAP4, Instruction.SWAP5, Instruction.SWAP6,
            Instruction.SWAP7, Instruction.SWAP8, Instruction.SWAP9, Instruction.SWAP10, Instruction.SWAP11,
            Instruction.SWAP12, Instruction.SWAP13, Instruction.SWAP14, Instruction.SWAP15, Instruction.SWAP16,
            Instruction.LOG0, Instruction.LOG1, Instruction.LOG2, Instruction.LOG3, Instruction.LOG4,
            Instruction.CREATE, Instruction.CALL, Instruction.CALLCODE, Instruction.RETURN, Instruction.DELEGATECALL,
            Instruction.CREATE2, Instruction.STATICCALL, Instruction.REVERT, Instruction.INVALID, Instruction.SELFDESTRUCT,
        };

        // [OP-062] Allowed precompile addresses (0x01 - 0x0A)
        public static readonly HashSet<int> AllowedPrecompiles = new()
        {
            0x01,  // ecRecover
            0x02,  // SHA2-256
            0x03,  // RIPEMD-160
            0x04,  // identity (datacopy)
            0x05,  // modexp
            0x06,  // ecAdd
            0x07,  // ecMul
            0x08,  // ecPairing
            0x09,  // blake2f
            0x0A,  // point evaluation (KZG)
        };

        // RIP-7212 secp256r1 precompile address (network-dependent)
        public const int Secp256r1Precompile = 0x100;

        public static bool IsAlwaysForbidden(Instruction opcode)
        {
            return AlwaysForbidden.Contains(opcode);
        }

        public static bool RequiresStaking(Instruction opcode)
        {
            return StakedOnlyOpcodes.Contains(opcode);
        }

        public static bool IsCallOpcode(Instruction opcode)
        {
            return CallOpcodes.Contains(opcode);
        }

        public static bool IsAllowedPrecompile(int address, bool includeRip7212 = false)
        {
            if (AllowedPrecompiles.Contains(address))
                return true;

            if (includeRip7212 && address == Secp256r1Precompile)
                return true;

            return false;
        }

        public static bool IsCreateOpcode(Instruction opcode)
        {
            return opcode == Instruction.CREATE || opcode == Instruction.CREATE2;
        }

        public static bool IsStorageOpcode(Instruction opcode)
        {
            return StorageOpcodes.Contains(opcode);
        }

        public static bool IsTransientStorageOpcode(Instruction opcode)
        {
            return TransientStorageOpcodes.Contains(opcode);
        }

        public static bool IsExtCodeOpcode(Instruction opcode)
        {
            return ExtCodeOpcodes.Contains(opcode);
        }

        public static bool IsValidOpcode(Instruction opcode)
        {
            return ValidOpcodes.Contains(opcode);
        }

        public static bool IsWriteStorageOpcode(Instruction opcode)
        {
            return opcode == Instruction.SSTORE || opcode == Instruction.TSTORE;
        }
    }
}
