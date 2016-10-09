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

/* http://ethereum.stackexchange.com/questions/119/what-opcodes-are-available-for-the-ethereum-evm
     * 0s: Stop and Arithmetic Operations

0x00    STOP        Halts execution.
0x01    ADD         Addition operation.
0x02    MUL         Multiplication operation.
0x03    SUB         Subtraction operation.
0x04    DIV         Integer division operation.
0x05    SDIV        Signed integer
0x06    MOD         Modulo
0x07    SMOD        Signed modulo
0x08    ADDMOD      Modulo
0x09    MULMOD      Modulo 
0x0a    EXP         Exponential operation.
0x0b    SIGNEXTEND  Extend length of two’s ( complement signed integer.
10s: Comparison & Bitwise Logic Operations

0x10    LT      Lesser-than comparison
0x11    GT      Greater-than comparison
0x12    SLT     Signed less-than comparison
0x13    SGT     Signed greater-than comparison
0x14    EQ      Equality  comparison
0x15    ISZERO  Simple not operator
0x16    AND     Bitwise AND operation.
0x17    OR      Bitwise OR operation.
0x18    XOR     Bitwise XOR operation.
0x19    NOT     Bitwise NOT operation.
0x1a    BYTE    Retrieve single byte from word
20s: SHA3

0x20    SHA3    Compute Keccak-256 hash.
30s: Environmental Information

0x30    ADDRESS         Get address of currently executing account.
0x31    BALANCE         Get balance of the given account.
0x32    ORIGIN          Get execution origination address.
0x33    CALLER          Get caller address.This is the address of the account that is directly responsible for this execution.
0x34    CALLVALUE       Get deposited value by the instruction/transaction responsible for this execution.
0x35    CALLDATALOAD    Get input data of current environment.
0x36    CALLDATASIZE    Get size of input data in current environment.
0x37    CALLDATACOPY    Copy input data in current environment to memory. This pertains to the input data passed with the message call instruction or transaction.
0x38    CODESIZE        Get size of code running in current environment.
0x39    CODECOPY        Copy code running in current environment to memory.
0x3a    GASPRICE        Get price of gas in current environment.
0x3b    EXTCODESIZE     Get size of an account’s code.
0x3c    EXTCODECOPY     Copy an account’s code to memory.
40s: Block Information

0x40    BLOCKHASH   Get the hash of one of the 256 most recent complete blocks.
0x41    COINBASE    Get the block’s beneficiary address.
0x42    TIMESTAMP   Get the block’s timestamp.
0x43    NUMBER      Get the block’s number.
0x44    DIFFICULTY  Get the block’s difficulty.
0x45    GASLIMIT    Get the block’s gas limit.
50s Stack, Memory, Storage and Flow Operatiions

0x50    POP         Remove item from stack.
0x51    MLOAD       Load word from memory.
0x52    MSTORE      Save word to memory.
0x53    MSTORE8     Save byte to memory.
0x54    SLOAD       Load word from storage.
0x55    SSTORE      Save word to storage.
0x56    JUMP        Alter the program counter.
0x57    JUMPI       Conditionally alter the program counter.
0x58    PC          Get the value of the program counter prior to the increment
0x59    MSIZE       Get the size of active memory in bytes.
0x5a    GAS         Get the amount of available gas, including the corresponding reduction
0x5b    JUMPDEST    Mark a valid destination for jumps.
60s & 70s: Push Operations

0x60    PUSH1   Place 1 byte item on stack.
0x61    PUSH2   Place 2-byte item on stack.
…       
0x7f    PUSH32  Place 32-byte (full word) item on stack.
80s: Duplication Operations

0x80    DUP1    Duplicate 1st stack item.
0x81    DUP2    Duplicate 2nd stack item.
…       
0x8f    DUP16   Duplicate 16th stack item.
90s: Exchange Operations

0x90    SWAP1   Exchange 1st and 2nd stack items.
0x91    SWAP2   Exchange 1st and 3rd stack items.
…   …   
0x9f    SWAP16  Exchange 1st and 17th stack items.
a0s: Logging Operations

0xa0    LOG0    Append log record with no topics.
0xa1    LOG1    Append log record with one topic.
…   …   
0xa4    LOG4    Append log record with four topics.
f0s: System operations

0xf0    CREATE          Create a new account with associated code.
0xf1    CALL            Message-call into an account.
0xf2    CALLCODE        Message-call into this account with alternative account’s code.
0xf3    RETURN          Halt execution returning output data.
0xf4    DELEGATECALL    Message-call into this account with an alternative account’s code, but persisting the current values for `sender` and `value`.
Halt Execution, Mark for deletion.

0xff    SUICIDE     Halt execution and register account for later deletion.

    */
}
