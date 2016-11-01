using System;
using System.Numerics;
using System.Collections;
using System.Linq;

namespace Nethereum.EVM
{

 //   public class Memory
 //   {
 //       /// Retrieve current size of the memory
 //       public int Size()
 //       {
            
 //       }

 //       /// Resize (shrink or expand) the memory to specified size (fills 0)
 //       public void Resize(int size)
 //       {
            
 //       }

 //       /// Resize the memory only if its smaller
 //       public void Expand(int newSize)
 //       {
 //       }

 //       /// Write single byte to memory
 //       public void WriteByte(int offset, int value)
 //       {
 //       }

 ////       /// Write a word to memory. Does not resize memory!
 ////       fn write(&mut self, offset: U256, value: U256);
 ////       /// Read a word from memory
 ////       fn read(&self, offset: U256) -> U256;
	/////// Write slice of bytes to memory. Does not resize memory!
	////fn write_slice(&mut self, offset: U256, &[u8]);
 ////       /// Retrieve part of the memory between offset and offset + size
 ////       fn read_slice(&self, offset: U256, size: U256) -> &[u8];
	/////// Retrieve writeable part of memory
	////fn writeable_slice(&mut self, offset: U256, size: U256) -> &mut[u8];
	////fn dump(&self);
 //   }

    public class VM
    {
        //TODO: Endianism checks can be done on pop and push // or not

        public void Step(Program program)
        {
            var instruction = program.GetCurrentInstruction();
            var instructionInfo = GetInstructionInfo(instruction);
            switch ((Instruction)instruction)
            {
                case Instruction.STOP:
                    break;
                case Instruction.ADD:
                    break;
                case Instruction.MUL:
                    break;
                case Instruction.SUB:
                    break;
                case Instruction.DIV:
                    break;
                case Instruction.SDIV:
                    break;
                case Instruction.MOD:
                    break;
                case Instruction.SMOD:
                    break;
                case Instruction.ADDMOD:
                    break;
                case Instruction.MULMOD:
                    break;
                case Instruction.EXP:
                    break;
                case Instruction.SIGNEXTEND:
                    break;
                case Instruction.LT:
                    var ltBytes1 = program.StackPop();
                    var ltBytes2 = program.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
                        ltBytes1 = ltBytes1.Reverse().ToArray();
                        ltBytes2 = ltBytes2.Reverse().ToArray();
                    }
                    program.StackPush(new[] { new BigInteger(ltBytes1) < new BigInteger(ltBytes2) ? (byte)1 : (byte)0 });
                    program.Step();
                    break;
                case Instruction.GT:
                    var gtBytes1 = program.StackPop();
                    var gtBytes2 = program.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
                        gtBytes1 = gtBytes1.Reverse().ToArray();
                        gtBytes2 = gtBytes2.Reverse().ToArray();
                    }
                    program.StackPush(new[] { new BigInteger(gtBytes1) > new BigInteger(gtBytes2) ? (byte)1 : (byte)0 });
                    program.Step();
                    break;
                case Instruction.SLT:
                    var sltBytes1 = program.StackPop();
                    var sltBytes2 = program.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
                        sltBytes1 = sltBytes1.Reverse().ToArray();
                        sltBytes2 = sltBytes2.Reverse().ToArray();
                    }
                    program.StackPush(new[] { new BigInteger(sltBytes1) < new BigInteger(sltBytes2) ? (byte)1 : (byte)0 });
                    program.Step();
                    break;
                case Instruction.SGT:
                    var sgtBytes1 = program.StackPop();
                    var sgtBytes2 = program.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
                        sgtBytes1 = sgtBytes1.Reverse().ToArray();
                        sgtBytes2 = sgtBytes2.Reverse().ToArray();
                    }
                    program.StackPush(new[] { new BigInteger(sgtBytes1) > new BigInteger(sgtBytes2) ? (byte)1 : (byte)0 });

                    program.Step();
                    break;
                case Instruction.EQ:
                    var eqBytes1 = program.StackPop();
                    var eqBytes2 = program.StackPop();
                    //check endianism
                    if (BitConverter.IsLittleEndian)
                    {
                        eqBytes1 = eqBytes1.Reverse().ToArray();
                        eqBytes2 = eqBytes2.Reverse().ToArray();
                    }
                    program.StackPush(new[] { new BigInteger(eqBytes1) == new BigInteger(eqBytes2) ? (byte)1: (byte)0});
                    program.Step();

                    break;
                case Instruction.ISZERO:
                    var isZeroBytes = program.StackPop();
                    //check endianism
                    program.StackPush(new BigInteger(isZeroBytes) == 0 ? new[] {(byte) 1} : new[] {(byte) 0});
                    program.Step();
                    break;
                case Instruction.AND:
                                   
                    var andBytes1 = program.StackPop();
                    var andBytes2 = program.StackPop();
                    //check endianism
                    var andB1 = new BigInteger(andBytes1);
                    andB1 = andB1 & new BigInteger(andBytes2);
     
                    program.StackPush(andB1.ToByteArray());
                    program.Step();
                    
                    break;
                case Instruction.OR:

                    var orBytes1 = program.StackPop();
                    var orBytes2 = program.StackPop();
                    //check endianism
                    var orB1 = new BigInteger(orBytes1);
                    orB1 = orB1 | new BigInteger(orBytes2);

                    program.StackPush(orB1.ToByteArray());
                    program.Step();
                    break;
                case Instruction.XOR:
                    var xorBytes1 = program.StackPop();
                    var xorBytes2 = program.StackPop();
                    //check endianism
                    var xorB1 = new BigInteger(xorBytes1);
                    xorB1 = xorB1 ^ new BigInteger(xorBytes2);

                    program.StackPush(xorB1.ToByteArray());
                    program.Step();
                    break;
                case Instruction.NOT:
                    var notBytes1 = program.StackPop();
                    //check endianism
                    var notB1 = new BigInteger(notBytes1);
                    program.StackPush((~notB1).ToByteArray());
                    break;
                case Instruction.BYTE:
                    var byteBytes1 = program.StackPop();
                    var byteBytes2 = program.StackPop();

                    var pos = new BigInteger(byteBytes1);
                    var word = PadTo32Bytes(byteBytes2);

                    var result = pos < 32 ? new [] { word[(int)pos]} : new byte[0];

                    program.StackPush(result);
                    program.Step();
                    break;
                case Instruction.SHA3:
                    break;
                case Instruction.ADDRESS:
                    break;
                case Instruction.BALANCE:
                    break;
                case Instruction.ORIGIN:
                    break;
                case Instruction.CALLER:
                    break;
                case Instruction.CALLVALUE:
                    break;
                case Instruction.CALLDATALOAD:
                    break;
                case Instruction.CALLDATASIZE:
                    break;
                case Instruction.CALLDATACOPY:
                    break;
                case Instruction.CODESIZE:
                    break;
                case Instruction.CODECOPY:
                    break;
                case Instruction.GASPRICE:
                    break;
                case Instruction.EXTCODESIZE:
                    break;
                case Instruction.EXTCODECOPY:
                    break;
                case Instruction.BLOCKHASH:
                    break;
                case Instruction.COINBASE:
                    break;
                case Instruction.TIMESTAMP:
                    break;
                case Instruction.NUMBER:
                    break;
                case Instruction.DIFFICULTY:
                    break;
                case Instruction.GASLIMIT:
                    break;
                case Instruction.POP:
                    program.StackPop();
                    program.Step();
                    break;
                case Instruction.MLOAD:
                    break;
                case Instruction.MSTORE:
                    break;
                case Instruction.MSTORE8:
                    break;
                case Instruction.SLOAD:
                    break;
                case Instruction.SSTORE:
                    break;
                case Instruction.JUMP:
                    break;
                case Instruction.JUMPI:
                    break;
                case Instruction.PC:
                    break;
                case Instruction.MSIZE:
                    break;
                case Instruction.GAS:
                    break;
                case Instruction.JUMPDEST:
                    break;
                case Instruction.PUSH1:
                case Instruction.PUSH2:
                case Instruction.PUSH3:
                case Instruction.PUSH4:
                case Instruction.PUSH5:
                case Instruction.PUSH6:
                case Instruction.PUSH7:
                case Instruction.PUSH8:
                case Instruction.PUSH9:
                case Instruction.PUSH10:
                case Instruction.PUSH11:
                case Instruction.PUSH12:
                case Instruction.PUSH13:
                case Instruction.PUSH14:
                case Instruction.PUSH15:
                case Instruction.PUSH16:
                case Instruction.PUSH17:
                case Instruction.PUSH18:
                case Instruction.PUSH19:
                case Instruction.PUSH20:
                case Instruction.PUSH21:
                case Instruction.PUSH22:
                case Instruction.PUSH23:
                case Instruction.PUSH24:
                case Instruction.PUSH25:
                case Instruction.PUSH26:
                case Instruction.PUSH27:
                case Instruction.PUSH28:
                case Instruction.PUSH29:
                case Instruction.PUSH30:
                case Instruction.PUSH31:
                case Instruction.PUSH32:
                    program.Step();
                    var pushNumber = instruction - (int)Instruction.PUSH1 + 1;
                    var data = program.Sweep(pushNumber);

                    program.StackPush(data);
                    break;
                case Instruction.DUP1:
                case Instruction.DUP2:
                case Instruction.DUP3:
                case Instruction.DUP4:
                case Instruction.DUP5:
                case Instruction.DUP6:
                case Instruction.DUP7:
                case Instruction.DUP8:
                case Instruction.DUP9:
                case Instruction.DUP10:
                case Instruction.DUP11:
                case Instruction.DUP12:
                case Instruction.DUP13:
                case Instruction.DUP14:
                case Instruction.DUP15:
                case Instruction.DUP16:
                    break;
                case Instruction.SWAP1:
                case Instruction.SWAP2:
                case Instruction.SWAP3:
                case Instruction.SWAP4:
                case Instruction.SWAP5:
                case Instruction.SWAP6:
                case Instruction.SWAP7:
                case Instruction.SWAP8:
                case Instruction.SWAP9:
                case Instruction.SWAP10:
                case Instruction.SWAP11:
                case Instruction.SWAP12:
                case Instruction.SWAP13:
                case Instruction.SWAP14:  
                case Instruction.SWAP15:
                case Instruction.SWAP16:
                    break;
                case Instruction.LOG0:
                    break;
                case Instruction.LOG1:
                    break;
                case Instruction.LOG2:
                    break;
                case Instruction.LOG3:
                    break;
                case Instruction.LOG4:
                    break;
                case Instruction.PUSHC:
                    break;
                case Instruction.JUMPV:
                    break;
                case Instruction.JUMPVI:
                    break;
                case Instruction.BAD:
                    break;
                case Instruction.CREATE:
                    break;
                case Instruction.CALL:
                    break;
                case Instruction.CALLCODE:
                    break;
                case Instruction.RETURN:
                    break;
                case Instruction.DELEGATECALL:
                    break;
                case Instruction.SUICIDE:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


        }

        public byte[] PadTo32Bytes(byte[] bytesToPad)
        {
     
            var ret = new byte[32];

            for (var i = 0; i < ret.Length; i++)
                    ret[i] = 0;          
            Array.Copy(bytesToPad, 0, ret, 32 - bytesToPad.Length, bytesToPad.Length);

            return ret;
        }
    

        public InstructionInfo GetInstructionInfo(byte instruction)
        {
            return InstructionInfoCollection.Instructions[(Instruction) instruction];
        }
    }
}