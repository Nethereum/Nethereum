namespace Nethereum.EVM
{
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
        ///<summary>256-bit shift left</summary>///
        SHL,
        ///<summary>256-bit shift right</summary>///
        SHR,
        ///<summary>int256 shift right</summary>///
        SAR,

        ///<summary>compute SHA3-256 hash</summary>///
        KECCAK256 = 0x20,

        ///<summary>address of executing contract (account)</summary>///
        ADDRESS = 0x30,     
        ///<summary>get balance of the given contract</summary>///
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
        ///<summary>Byzantium hardfork, EIP-211: the size of the returned data from the last external call, in bytes</summary>///
        RETURNDATASIZE,
        ///<summary>Byzantium hardfork, EIP-211: copy returned data</summary>///
        RETURNDATACOPY,
        ///<summary>Constantinople hardfork, EIP-1052: hash of the contract bytecode at addr</summary>///
        EXTCODEHASH,

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
        ///<summary>Istanbul hardfork, EIP-1344: current network's chain id</summary>///
        CHAINID,
        ///<summary>Istanbul hardfork, EIP-1884: balance of the executing contract in wei</summary>///
        SELFBALANCE,
        ///<summary>London hardfork, EIP-3198: current block's base fee</summary>///
        BASEFEE,

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
        /// <summary>
        /// Constantinople harfork, EIP-1014: creates a child contract with a deterministic address
        /// </summary>
        CREATE2,
        /// <summary>
        /// Byzantium hardfork, EIP-214: calls a method in another contract with state changes such as contract creation, event emission, storage modification and contract destruction disallowed
        /// </summary>
        STATICCALL = 0xfa,
        /// <summary>
        /// Byzantium hardfork, EIP-140: reverts with return data
        /// </summary>
        REVERT = 0xfd,
        /// <summary>
        /// Designated invalid opcode
        /// </summary>
        INVALID = 0xfe,
        ///<summary>halt execution and register account for later deletion</summary>///
        SELFDESTRUCT = 0xff 
    }

}
