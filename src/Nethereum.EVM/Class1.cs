using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.EVM
{

    public class GasCost
    {

        /* backwards compatibility, remove eventually */
        public const int STEP = 1;
        public const int SSTORE = 300;
        /* backwards compatibility, remove eventually */

        public const int ZEROSTEP = 0;
        public const int QUICKSTEP = 2;
        public const int FASTESTSTEP = 3;
        public const int FASTSTEP = 5;
        public const int MIDSTEP = 8;
        public const int SLOWSTEP = 10;
        public const int EXTSTEP = 20;

        public const int GENESISGASLIMIT = 1000000;
        public const int MINGASLIMIT = 125000;

        public const int BALANCE = 20;
        public const int SHA3 = 30;
        public const int SHA3_WORD = 6;
        public const int SLOAD = 50;
        public const int STOP = 0;
        public const int SUICIDE = 0;
        public const int CLEAR_SSTORE = 5000;
        public const int SET_SSTORE = 20000;
        public const int RESET_SSTORE = 5000;
        public const int REFUND_SSTORE = 15000;
        public const int CREATE = 32000;

        public const int JUMPDEST = 1;
        public const int CREATE_DATA_BYTE = 5;
        public const int CALL = 40;
        public const int STIPEND_CALL = 2300;
        public const int VT_CALL = 9000; //value transfer call
        public const int NEW_ACCT_CALL = 25000; //new account call
        public const int MEMORY = 3;
        public const int SUICIDE_REFUND = 24000;
        public const int QUAD_COEFF_DIV = 512;
        public const int CREATE_DATA = 200;
        public const int TX_NO_ZERO_DATA = 68;
        public const int TX_ZERO_DATA = 4;
        public const int TRANSACTION = 21000;
        public const int TRANSACTION_CREATE_CONTRACT = 53000;
        public const int LOG_GAS = 375;
        public const int LOG_DATA_GAS = 8;
        public const int LOG_TOPIC_GAS = 375;
        public const int COPY_GAS = 3;
        public const int EXP_GAS = 10;
        public const int EXP_BYTE_GAS = 10;
        public const int IDENTITY = 15;
        public const int IDENTITY_WORD = 3;
        public const int RIPEMD160 = 600;
        public const int RIPEMD160_WORD = 120;
        public const int SHA256 = 60;
        public const int SHA256_WORD = 12;
        public const int EC_RECOVER = 3000;
    }



public class InstructionCollection
    {

     public static Dictionary<Instruction, InstructionInfo> Instructions = new Dictionary<Instruction, InstructionInfo>()
     { //                                                                  Add, Args, Ret, SideEffects, GasPriceTier
            { Instruction.STOP,        new InstructionInfo( "STOP",           0, 0, 0, true, GasPriceTier.ZeroTier ) } ,
            { Instruction.ADD,         new InstructionInfo( "ADD",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SUB,         new InstructionInfo( "SUB",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.MUL,         new InstructionInfo( "MUL",            0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.DIV,         new InstructionInfo( "DIV",            0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.SDIV,        new InstructionInfo( "SDIV",           0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.MOD,         new InstructionInfo( "MOD",            0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.SMOD,        new InstructionInfo( "SMOD",           0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.EXP,         new InstructionInfo( "EXP",            0, 2, 1, false, GasPriceTier.SpecialTier ) },
            { Instruction.NOT,         new InstructionInfo( "NOT",            0, 1, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.LT,          new InstructionInfo( "LT",             0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.GT,          new InstructionInfo( "GT",             0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SLT,         new InstructionInfo( "SLT",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SGT,         new InstructionInfo( "SGT",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.EQ,          new InstructionInfo( "EQ",             0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.ISZERO,      new InstructionInfo( "ISZERO",         0, 1, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.AND,         new InstructionInfo( "AND",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.OR,          new InstructionInfo( "OR",             0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.XOR,         new InstructionInfo( "XOR",            0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.BYTE,        new InstructionInfo( "BYTE",           0, 2, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.ADDMOD,      new InstructionInfo( "ADDMOD",         0, 3, 1, false, GasPriceTier.MidTier ) },
            { Instruction.MULMOD,      new InstructionInfo( "MULMOD",         0, 3, 1, false, GasPriceTier.MidTier ) },
            { Instruction.SIGNEXTEND,  new InstructionInfo( "SIGNEXTEND",     0, 2, 1, false, GasPriceTier.LowTier ) },
            { Instruction.SHA3,        new InstructionInfo( "SHA3",           0, 2, 1, false, GasPriceTier.SpecialTier ) },
            { Instruction.ADDRESS,     new InstructionInfo( "ADDRESS",        0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.BALANCE,     new InstructionInfo( "BALANCE",        0, 1, 1, false, GasPriceTier.ExtTier ) },
            { Instruction.ORIGIN,      new InstructionInfo( "ORIGIN",         0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.CALLER,      new InstructionInfo( "CALLER",         0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.CALLVALUE,   new InstructionInfo( "CALLVALUE",      0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.CALLDATALOAD,new InstructionInfo( "CALLDATALOAD",   0, 1, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.CALLDATASIZE,new InstructionInfo( "CALLDATASIZE",   0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.CALLDATACOPY,new InstructionInfo( "CALLDATACOPY",   0, 3, 0, true, GasPriceTier.VeryLowTier ) },
            { Instruction.CODESIZE,    new InstructionInfo( "CODESIZE",       0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.CODECOPY,    new InstructionInfo( "CODECOPY",       0, 3, 0, true, GasPriceTier.VeryLowTier ) },
            { Instruction.GASPRICE,    new InstructionInfo( "GASPRICE",       0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.EXTCODESIZE, new InstructionInfo( "EXTCODESIZE",    0, 1, 1, false, GasPriceTier.ExtTier ) },
            { Instruction.EXTCODECOPY, new InstructionInfo( "EXTCODECOPY",    0, 4, 0, true, GasPriceTier.ExtTier ) },
            { Instruction.BLOCKHASH,   new InstructionInfo( "BLOCKHASH",      0, 1, 1, false, GasPriceTier.ExtTier ) },
            { Instruction.COINBASE,    new InstructionInfo( "COINBASE",       0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.TIMESTAMP,   new InstructionInfo( "TIMESTAMP",      0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.NUMBER,      new InstructionInfo( "NUMBER",         0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.DIFFICULTY,  new InstructionInfo( "DIFFICULTY",     0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.GASLIMIT,    new InstructionInfo( "GASLIMIT",       0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.POP,         new InstructionInfo( "POP",            0, 1, 0, false, GasPriceTier.BaseTier ) },
            { Instruction.MLOAD,       new InstructionInfo( "MLOAD",          0, 1, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.MSTORE,      new InstructionInfo( "MSTORE",         0, 2, 0, true, GasPriceTier.VeryLowTier ) },
            { Instruction.MSTORE8,     new InstructionInfo( "MSTORE8",        0, 2, 0, true, GasPriceTier.VeryLowTier ) },
            { Instruction.SLOAD,       new InstructionInfo( "SLOAD",          0, 1, 1, false, GasPriceTier.SpecialTier ) },
            { Instruction.SSTORE,      new InstructionInfo( "SSTORE",         0, 2, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.JUMP,        new InstructionInfo( "JUMP",           0, 1, 0, true, GasPriceTier.MidTier ) },
            { Instruction.JUMPI,       new InstructionInfo( "JUMPI",          0, 2, 0, true, GasPriceTier.HighTier ) },
            { Instruction.PC,          new InstructionInfo( "PC",             0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.MSIZE,       new InstructionInfo( "MSIZE",          0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.GAS,         new InstructionInfo( "GAS",            0, 0, 1, false, GasPriceTier.BaseTier ) },
            { Instruction.JUMPDEST,    new InstructionInfo( "JUMPDEST",       0, 0, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.PUSH1,       new InstructionInfo( "PUSH1",          1, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH2,       new InstructionInfo( "PUSH2",          2, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH3,       new InstructionInfo( "PUSH3",          3, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH4,       new InstructionInfo( "PUSH4",          4, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH5,       new InstructionInfo( "PUSH5",          5, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH6,       new InstructionInfo( "PUSH6",          6, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH7,       new InstructionInfo( "PUSH7",          7, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH8,       new InstructionInfo( "PUSH8",          8, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH9,       new InstructionInfo( "PUSH9",          9, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH10,      new InstructionInfo( "PUSH10",         10, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH11,      new InstructionInfo( "PUSH11",         11, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH12,      new InstructionInfo( "PUSH12",         12, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH13,      new InstructionInfo( "PUSH13",         13, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH14,      new InstructionInfo( "PUSH14",         14, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH15,      new InstructionInfo( "PUSH15",         15, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH16,      new InstructionInfo( "PUSH16",         16, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH17,      new InstructionInfo( "PUSH17",         17, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH18,      new InstructionInfo( "PUSH18",         18, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH19,      new InstructionInfo( "PUSH19",         19, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH20,      new InstructionInfo( "PUSH20",         20, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH21,      new InstructionInfo( "PUSH21",         21, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH22,      new InstructionInfo( "PUSH22",         22, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH23,      new InstructionInfo( "PUSH23",         23, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH24,      new InstructionInfo( "PUSH24",         24, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH25,      new InstructionInfo( "PUSH25",         25, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH26,      new InstructionInfo( "PUSH26",         26, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH27,      new InstructionInfo( "PUSH27",         27, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH28,      new InstructionInfo( "PUSH28",         28, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH29,      new InstructionInfo( "PUSH29",         29, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH30,      new InstructionInfo( "PUSH30",         30, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH31,      new InstructionInfo( "PUSH31",         31, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.PUSH32,      new InstructionInfo( "PUSH32",         32, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP1,        new InstructionInfo( "DUP1",           0, 1, 2, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP2,        new InstructionInfo( "DUP2",           0, 2, 3, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP3,        new InstructionInfo( "DUP3",           0, 3, 4, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP4,        new InstructionInfo( "DUP4",           0, 4, 5, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP5,        new InstructionInfo( "DUP5",           0, 5, 6, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP6,        new InstructionInfo( "DUP6",           0, 6, 7, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP7,        new InstructionInfo( "DUP7",           0, 7, 8, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP8,        new InstructionInfo( "DUP8",           0, 8, 9, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP9,        new InstructionInfo( "DUP9",           0, 9, 10, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP10,       new InstructionInfo( "DUP10",          0, 10, 11, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP11,       new InstructionInfo( "DUP11",          0, 11, 12, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP12,       new InstructionInfo( "DUP12",          0, 12, 13, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP13,       new InstructionInfo( "DUP13",          0, 13, 14, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP14,       new InstructionInfo( "DUP14",          0, 14, 15, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP15,       new InstructionInfo( "DUP15",          0, 15, 16, false, GasPriceTier.VeryLowTier ) },
            { Instruction.DUP16,       new InstructionInfo( "DUP16",          0, 16, 17, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP1,       new InstructionInfo( "SWAP1",          0, 2, 2, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP2,       new InstructionInfo( "SWAP2",          0, 3, 3, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP3,       new InstructionInfo( "SWAP3",          0, 4, 4, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP4,       new InstructionInfo( "SWAP4",          0, 5, 5, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP5,       new InstructionInfo( "SWAP5",          0, 6, 6, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP6,       new InstructionInfo( "SWAP6",          0, 7, 7, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP7,       new InstructionInfo( "SWAP7",          0, 8, 8, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP8,       new InstructionInfo( "SWAP8",          0, 9, 9, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP9,       new InstructionInfo( "SWAP9",          0, 10, 10, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP10,      new InstructionInfo( "SWAP10",         0, 11, 11, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP11,      new InstructionInfo( "SWAP11",         0, 12, 12, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP12,      new InstructionInfo( "SWAP12",         0, 13, 13, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP13,      new InstructionInfo( "SWAP13",         0, 14, 14, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP14,      new InstructionInfo( "SWAP14",         0, 15, 15, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP15,      new InstructionInfo( "SWAP15",         0, 16, 16, false, GasPriceTier.VeryLowTier ) },
            { Instruction.SWAP16,      new InstructionInfo( "SWAP16",         0, 17, 17, false, GasPriceTier.VeryLowTier ) },
            { Instruction.LOG0,        new InstructionInfo( "LOG0",           0, 2, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.LOG1,        new InstructionInfo( "LOG1",           0, 3, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.LOG2,        new InstructionInfo( "LOG2",           0, 4, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.LOG3,        new InstructionInfo( "LOG3",           0, 5, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.LOG4,        new InstructionInfo( "LOG4",           0, 6, 0, true, GasPriceTier.SpecialTier ) },
            { Instruction.CREATE,      new InstructionInfo( "CREATE",         0, 3, 1, true, GasPriceTier.SpecialTier ) },
            { Instruction.CALL,        new InstructionInfo( "CALL",           0, 7, 1, true, GasPriceTier.SpecialTier ) },
            { Instruction.CALLCODE,    new InstructionInfo( "CALLCODE",       0, 7, 1, true, GasPriceTier.SpecialTier ) },
            { Instruction.RETURN,      new InstructionInfo( "RETURN",         0, 2, 0, true, GasPriceTier.ZeroTier ) },
            { Instruction.DELEGATECALL,new InstructionInfo( "DELEGATECALL",   0, 6, 1, true, GasPriceTier.SpecialTier ) },
            { Instruction.SUICIDE,     new InstructionInfo( "SUICIDE",        0, 1, 0, true, GasPriceTier.ZeroTier ) },

	        // these are generated by the interpreter - should never be in user code
	        { Instruction.PUSHC,       new InstructionInfo( "PUSHC",          2, 0, 1, false, GasPriceTier.VeryLowTier ) },
            { Instruction.JUMPV,       new InstructionInfo( "JUMPV",          0, 1, 0, true, GasPriceTier.MidTier ) },
            { Instruction.JUMPVI,      new InstructionInfo( "JUMPVI",         0, 1, 0, true, GasPriceTier.HighTier ) },
            { Instruction.STOP,        new InstructionInfo( "BAD",            0, 0, 0, true, GasPriceTier.ZeroTier ) },
        };
    }

    public enum Instruction
    {
       ///<summary>halts execution</summary>///
        STOP = 0x00,        
        ///<summary>addition operation</summary>///
        ADD,                
        ///<summary>mulitplication operation</summary>///
        MUL,                
        ///<summary>subtraction operation</summary>///
        SUB,                
        ///<summary>integer division operation</summary>///
        DIV,                
    ///<summary>signed integer division operation</summary>///
        SDIV,               
		///<summary>modulo remainder operation</summary>///
        MOD,                
		///<summary>signed modulo remainder operation</summary>///
        SMOD,               
		///<summary>unsigned modular addition</summary>///
        ADDMOD,             
		///<summary>unsigned modular multiplication</summary>///
        MULMOD,             
		///<summary>exponential operation</summary>///
        EXP,                
		///<summary>extend length of signed integer</summary>///
        SIGNEXTEND,         

		///<summary>less-than comparision</summary>///
        LT = 0x10,          
		///<summary>greater-than comparision</summary>///
        GT,                 
		///<summary>signed less-than comparision</summary>///
        SLT,                
		///<summary>signed greater-than comparision</summary>///
        SGT,                
		///<summary>equality comparision</summary>///
        EQ,                 
		///<summary>simple not operator</summary>///
        ISZERO,             
		///<summary>bitwise AND operation</summary>///
        AND,                
		///<summary>bitwise OR operation</summary>///
        OR,                 
		///<summary>bitwise XOR operation</summary>///
        XOR,                
		///<summary>bitwise NOT opertation</summary>///
        NOT,                
		///<summary>retrieve single byte from word</summary>///
        BYTE,               

		///<summary>compute SHA3-256 hash</summary>///
        SHA3 = 0x20,        

		///<summary>get address of currently executing account</summary>///
        ADDRESS = 0x30,     
		///<summary>get balance of the given account</summary>///
        BALANCE,            
		///<summary>get execution origination address</summary>///
        ORIGIN,             
		///<summary>get caller address</summary>///
        CALLER,             
		///<summary>get deposited value by the instruction/transaction responsible</summary>///
        CALLVALUE, 
		///<summary>get input data of current environment</summary>///
        CALLDATALOAD,       
		///<summary>get size of input data in current environment</summary>///
        CALLDATASIZE,       
		///<summary>copy input data in current environment to memory</summary>///
        CALLDATACOPY,       
		///<summary>get size of code running in current environment</summary>///
        CODESIZE,           
		///<summary>copy code running in current environment to memory</summary>///
        CODECOPY,           
		///<summary>get price of gas in current environment</summary>///
        GASPRICE,           
		///<summary>get external code size (from another contract)</summary>///
        EXTCODESIZE,        
		///<summary>copy external code (from another contract)</summary>///
        EXTCODECOPY,        

		///<summary>get hash of most recent complete block</summary>///
        BLOCKHASH = 0x40,   
		///<summary>get the block's coinbase address</summary>///
        COINBASE,           
		///<summary>get the block's timestamp</summary>///
        TIMESTAMP,          
		///<summary>get the block's number</summary>///
        NUMBER,             
		///<summary>get the block's difficulty</summary>///
        DIFFICULTY,         
		///<summary>get the block's gas limit</summary>///
        GASLIMIT,           

		///<summary>remove item from stack</summary>///
        POP = 0x50,         
		///<summary>load word from memory</summary>///
        MLOAD,              
		///<summary>save word to memory</summary>///
        MSTORE,             
		///<summary>save byte to memory</summary>///
        MSTORE8,            
		///<summary>load word from storage</summary>///
        SLOAD,              
		///<summary>save word to storage</summary>///
        SSTORE,             
		///<summary>alter the program counter</summary>///
        JUMP,               
		///<summary>conditionally alter the program counter</summary>///
        JUMPI,              
		///<summary>get the program counter</summary>///
        PC,                 
		///<summary>get the size of active memory</summary>///
        MSIZE,              
		///<summary>get the amount of available gas</summary>///
        GAS,                
		///<summary>set a potential jump destination</summary>///
        JUMPDEST,           

		///<summary>place 1 byte item on stack</summary>///
        PUSH1 = 0x60,       
		///<summary>place 2 byte item on stack</summary>///
        PUSH2,              
		///<summary>place 3 byte item on stack</summary>///
        PUSH3,              
		///<summary>place 4 byte item on stack</summary>///
        PUSH4,              
		///<summary>place 5 byte item on stack</summary>///
        PUSH5,              
		///<summary>place 6 byte item on stack</summary>///
        PUSH6,              
		///<summary>place 7 byte item on stack</summary>///
        PUSH7,              
		///<summary>place 8 byte item on stack</summary>///
        PUSH8,              
		///<summary>place 9 byte item on stack</summary>///
        PUSH9,              
		///<summary>place 10 byte item on stack</summary>///
        PUSH10,             
		///<summary>place 11 byte item on stack</summary>///
        PUSH11,             
		///<summary>place 12 byte item on stack</summary>///
        PUSH12,             
		///<summary>place 13 byte item on stack</summary>///
        PUSH13,             
		///<summary>place 14 byte item on stack</summary>///
        PUSH14,             
		///<summary>place 15 byte item on stack</summary>///
        PUSH15,             
		///<summary>place 16 byte item on stack</summary>///
        PUSH16,             
		///<summary>place 17 byte item on stack</summary>///
        PUSH17,             
		///<summary>place 18 byte item on stack</summary>///
        PUSH18,             
		///<summary>place 19 byte item on stack</summary>///
        PUSH19,             
		///<summary>place 20 byte item on stack</summary>///
        PUSH20,             
		///<summary>place 21 byte item on stack</summary>///
        PUSH21,             
		///<summary>place 22 byte item on stack</summary>///
        PUSH22,             
		///<summary>place 23 byte item on stack</summary>///
        PUSH23,             
		///<summary>place 24 byte item on stack</summary>///
        PUSH24,             
		///<summary>place 25 byte item on stack</summary>///
        PUSH25,             
		///<summary>place 26 byte item on stack</summary>///
        PUSH26,             
		///<summary>place 27 byte item on stack</summary>///
        PUSH27,             
		///<summary>place 28 byte item on stack</summary>///
        PUSH28,             
		///<summary>place 29 byte item on stack</summary>///
        PUSH29,             
		///<summary>place 30 byte item on stack</summary>///
        PUSH30,             
		///<summary>place 31 byte item on stack</summary>///
        PUSH31,             
		///<summary>place 32 byte item on stack</summary>///
        PUSH32,             

		///<summary>copies the highest item in the stack to the top of the stack</summary>///
        DUP1 = 0x80,        
		///<summary>copies the second highest item in the stack to the top of the stack</summary>///
        DUP2,               
		///<summary>copies the third highest item in the stack to the top of the stack</summary>///
        DUP3,               
		///<summary>copies the 4th highest item in the stack to the top of the stack</summary>///
        DUP4,               
		///<summary>copies the 5th highest item in the stack to the top of the stack</summary>///
        DUP5,               
		///<summary>copies the 6th highest item in the stack to the top of the stack</summary>///
        DUP6,               
		///<summary>copies the 7th highest item in the stack to the top of the stack</summary>///
        DUP7,               
		///<summary>copies the 8th highest item in the stack to the top of the stack</summary>///
        DUP8,               
		///<summary>copies the 9th highest item in the stack to the top of the stack</summary>///
        DUP9,               
		///<summary>copies the 10th highest item in the stack to the top of the stack</summary>///
        DUP10,              
		///<summary>copies the 11th highest item in the stack to the top of the stack</summary>///
        DUP11,              
		///<summary>copies the 12th highest item in the stack to the top of the stack</summary>///
        DUP12,              
		///<summary>copies the 13th highest item in the stack to the top of the stack</summary>///
        DUP13,              
		///<summary>copies the 14th highest item in the stack to the top of the stack</summary>///
        DUP14,              
		///<summary>copies the 15th highest item in the stack to the top of the stack</summary>///
        DUP15,              
		///<summary>copies the 16th highest item in the stack to the top of the stack</summary>///
        DUP16,              

		///<summary>swaps the highest and second highest value on the stack</summary>///
        SWAP1 = 0x90,       
		///<summary>swaps the highest and third highest value on the stack</summary>///
        SWAP2,              
		///<summary>swaps the highest and 4th highest value on the stack</summary>///
        SWAP3,              
		///<summary>swaps the highest and 5th highest value on the stack</summary>///
        SWAP4,              
		///<summary>swaps the highest and 6th highest value on the stack</summary>///
        SWAP5,              
		///<summary>swaps the highest and 7th highest value on the stack</summary>///
        SWAP6,              
		///<summary>swaps the highest and 8th highest value on the stack</summary>///
        SWAP7,              
		///<summary>swaps the highest and 9th highest value on the stack</summary>///
        SWAP8,              
		///<summary>swaps the highest and 10th highest value on the stack</summary>///
        SWAP9,              
		///<summary>swaps the highest and 11th highest value on the stack</summary>///
        SWAP10,             
		///<summary>swaps the highest and 12th highest value on the stack</summary>///
        SWAP11,             
		///<summary>swaps the highest and 13th highest value on the stack</summary>///
        SWAP12,             
		///<summary>swaps the highest and 14th highest value on the stack</summary>///
        SWAP13,             
		///<summary>swaps the highest and 15th highest value on the stack</summary>///
        SWAP14,             
		///<summary>swaps the highest and 16th highest value on the stack</summary>///
        SWAP15,             
		///<summary>swaps the highest and 17th highest value on the stack</summary>///
        SWAP16,             

		///<summary>Makes a log entry; no topics.</summary>///
        LOG0 = 0xa0,        
		///<summary>Makes a log entry; 1 topic.</summary>///
        LOG1,               
		///<summary>Makes a log entry; 2 topics.</summary>///
        LOG2,               
		///<summary>Makes a log entry; 3 topics.</summary>///
        LOG3,               
		///<summary>Makes a log entry; 4 topics.</summary>///
        LOG4,               


		///<summary>push value from constant pool</summary>///
        PUSHC = 0xac,       
		///<summary>alter the program counter - pre-verified</summary>///
        JUMPV,              
		///<summary>conditionally alter the program counter - pre-verified</summary>///
        JUMPVI,             
		///<summary>placed to force invalid instruction exception</summary>///
        BAD,                

		///<summary>create a new account with associated code</summary>///
        CREATE = 0xf0,      
		///<summary>message-call into an account</summary>///
        CALL,               
		///<summary>message-call with another account's code only</summary>///
        CALLCODE,           
		///<summary>halt execution returning output data</summary>///
        RETURN,             
		///<summary>like CALLCODE but keeps caller's value and sender</summary>///
        DELEGATECALL,       
		///<summary>halt execution and register account for later deletion</summary>///
        SUICIDE = 0xff 

    }

    public class InstructionInfo
    {
        public InstructionInfo(string name, int additional, int args, int ret, bool sideEffects, GasPriceTier gasPriceTier)
        {
            this.Name = name;
            this.Additional = additional;
            this.Args = args;
            this.Ret = ret;
             this.SideEffects = sideEffects;
            this.GasPriceTier = gasPriceTier;
        }
        /// <summary>
        /// The name of the instruction.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Additional items required in memory for this instructions (only for PUSH).
        /// </summary>
        public int Additional { get; set; }
        /// <summary>
        /// Number of items required on the stack for this instruction (and, for the purposes of ret, the number taken from the stack).
        /// </summary>
        public int Args { get; set; }
        
        /// <summary>
        /// Number of items placed (back) on the stack by this instruction, assuming args items were removed.
        /// </summary>
        public int Ret { get; set; }
        
        /// <summary>
        /// false if the only effect on the execution environment (apart from gas usage) is a change to a topmost segment of the stack
        /// </summary>
        public bool SideEffects { get; set; }
        
        /// <summary>
        /// Tier for gas pricing.
        /// </summary>
        public GasPriceTier GasPriceTier { get; set; }
    }
    //0, 2, 3, 5, 8, 10, 20, 0}
    public enum GasPriceTier
    {
	    ZeroTier = 0,   // 0, Zero
        BaseTier,       // 2, Quick
        VeryLowTier,    // 3, Fastest
        LowTier,        // 5, Fast
        MidTier,        // 8, Mid
        HighTier,       // 10, Slow
        ExtTier,        // 20, Ext
        SpecialTier,    // multiparam or otherwise special
        InvalidTier     // Invalid.
    }
}
