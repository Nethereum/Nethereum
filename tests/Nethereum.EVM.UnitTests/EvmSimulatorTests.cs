using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EVM.UnitTests
{

    public class EvmSimulatorTests
    {
       

        [Fact]
        public async Task ShouldPUSH()
        {
     
            await AssertSteps("60A0", "00000000000000000000000000000000000000000000000000000000000000A0");
            await AssertSteps("69A0B0C0D0E0F0A1B1C1D1"
                , "00000000000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1");
            await AssertSteps("6AA0B0C0D0E0F0A1B1C1D1E1"
                , "000000000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1");
           await AssertSteps("6BA0B0C0D0E0F0A1B1C1D1E1F1"
                , "0000000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1");
            await AssertSteps("6CA0B0C0D0E0F0A1B1C1D1E1F1A2"
                , "00000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2");
       
            await AssertSteps("6DA0B0C0D0E0F0A1B1C1D1E1F1A2B2"
                , "000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2");
       
            await AssertSteps("6EA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2"
                , "0000000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2");
        
            await AssertSteps("6FA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2"
                , "00000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2");
       
            await AssertSteps("70A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2"
                , "000000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2");
       
            await AssertSteps("71A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2"
                , "0000000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2");
       
            await AssertSteps("72A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3"
                , "00000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3");
       
            await AssertSteps("61A0B0"
                , "000000000000000000000000000000000000000000000000000000000000A0B0");
      
            await AssertSteps("73A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3"
                , "000000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3");
        
            await AssertSteps("74A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3"
                , "0000000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3");
      
            await AssertSteps("75A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3"
                , "00000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3");
            await AssertSteps("76A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3"
                , "000000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3");
      
            await AssertSteps("77A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3"
                , "0000000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3");
            await AssertSteps("78A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4"
                , "00000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4");
            await AssertSteps("79A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4"
                , "000000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4");
            await AssertSteps("7AA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4"
                , "0000000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4");
      
            await AssertSteps("7BA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4"
                , "00000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4");
       
            await AssertSteps("7CA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4"
                , "000000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4");
            await AssertSteps("62A0B0C0"
                , "0000000000000000000000000000000000000000000000000000000000A0B0C0");
        
            await AssertSteps("7DA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4"
                , "0000A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4");
            await AssertSteps("7EA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1"
                , "00A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1");
        
            await AssertSteps("7FA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1B1"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1B1");

            await AssertSteps("63A0B0C0D0"
                , "00000000000000000000000000000000000000000000000000000000A0B0C0D0");
    

            await AssertSteps("64A0B0C0D0E0"
                , "000000000000000000000000000000000000000000000000000000A0B0C0D0E0");
 
            await AssertSteps("65A0B0C0D0E0F0"
                , "0000000000000000000000000000000000000000000000000000A0B0C0D0E0F0");
  
            await AssertSteps("66A0B0C0D0E0F0A1"
                , "00000000000000000000000000000000000000000000000000A0B0C0D0E0F0A1");

            await AssertSteps("67A0B0C0D0E0F0A1B1"
                , "000000000000000000000000000000000000000000000000A0B0C0D0E0F0A1B1");

            await AssertSteps("68A0B0C0D0E0F0A1B1C1"
                , "0000000000000000000000000000000000000000000000A0B0C0D0E0F0A1B1C1");
        }

        [Fact]
        public async Task ShouldAND()
        {
            await AssertSteps("600A600A16", "000000000000000000000000000000000000000000000000000000000000000A", 3);
            await AssertSteps("60C0600A16", "0000000000000000000000000000000000000000000000000000000000000000", 3);
        }


        [Fact]
        public async Task ShouldOR()
        { 
            await AssertSteps("60F0600F17", "00000000000000000000000000000000000000000000000000000000000000FF", 3);
            await AssertSteps("60C3603C17", "00000000000000000000000000000000000000000000000000000000000000FF", 3);
        }

        [Fact]
        public async Task ShouldXOR()
        { 
            await AssertSteps("60FF60FF18", "0000000000000000000000000000000000000000000000000000000000000000", 3);
            await AssertSteps("600F60F018", "00000000000000000000000000000000000000000000000000000000000000FF", 3);
           
        }

        [Fact]
        public async Task ShouldBYTE()
        { 
            await AssertSteps("65AABBCCDDEEFF601E1A", "EE", 3);
            await AssertSteps("65AABBCCDDEEFF60201A", "", 3);
            await AssertSteps("65AABBCCDDEE3A601F1A", "3A", 3);
        }

        [Fact]
        public async Task ShouldCheck_ISZERO()
        { 
            await AssertSteps("600015", "0000000000000000000000000000000000000000000000000000000000000001", 2);
            await AssertSteps("602A15", "0000000000000000000000000000000000000000000000000000000000000000", 2);
        }

        [Fact]
        public async Task ShouldCheck_EQ()
        { 
            await AssertSteps("602A602A14", "0000000000000000000000000000000000000000000000000000000000000001", 3);
            await AssertSteps("622A3B4C622A3B4C14", "0000000000000000000000000000000000000000000000000000000000000001", 3);
            await AssertSteps("622A3B5C622A3B4C14", "0000000000000000000000000000000000000000000000000000000000000000", 3);
        }

        [Fact]
        public async Task ShouldCheck_GT()
        { 
            await AssertSteps("6001600211", "0000000000000000000000000000000000000000000000000000000000000001", 3);
            await AssertSteps("6001610F0011", "0000000000000000000000000000000000000000000000000000000000000001", 3);
            await AssertSteps("6301020304610F0011", "0000000000000000000000000000000000000000000000000000000000000000", 3);
        }

     
        [Fact]
        public async Task ShouldCheck_SGT()
        { 
            await AssertSteps("6001600213", "0000000000000000000000000000000000000000000000000000000000000001", 3);
            await AssertSteps("7F000000000000000000000000000000000000000000000000000000000000001E" + "7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF56" + "13", "0000000000000000000000000000000000000000000000000000000000000000", 3); // -170 -    30
            await AssertSteps("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF56" + "7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF57" + "13", "0000000000000000000000000000000000000000000000000000000000000001", 3); // // -169 -  -170

        }

        [Fact]
        public  async Task ShouldLT()
        {
            await AssertSteps("6001600210", "0000000000000000000000000000000000000000000000000000000000000000", 3);
            await AssertSteps("6001610F0010", "0000000000000000000000000000000000000000000000000000000000000000", 3);
            await AssertSteps("6301020304610F0010", "0000000000000000000000000000000000000000000000000000000000000001", 3);
        }

        [Fact]
        public async Task ShouldSLT_1()
        { // SLT OP
            await AssertSteps("6001600212", "0000000000000000000000000000000000000000000000000000000000000000", 3);
        }

        [Fact]
        public async Task ShouldSLT_2()
        { // SLT OP
            await AssertSteps("7F000000000000000000000000000000000000000000000000000000000000001E" + "7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF56" + "12", "0000000000000000000000000000000000000000000000000000000000000001", 3); // -170 -    30
        }

        [Fact]
        public async Task ShouldSLT_3()
        { // SLT OP
            await AssertSteps("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF56" + "7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF57" + "12", "0000000000000000000000000000000000000000000000000000000000000000", 3); // // -169 -  -170
        }

        [Fact]
        public async Task Should_NOT_1()
        { // NOT OP
            await AssertSteps("600119", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", 2);
        }

        [Fact]
        public async Task Should_NOT_2()
        {
            // NOT OP
            await AssertSteps("61A00319", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF5FFC", 2);
        }

        [Fact]
        public async Task Should_NOT_3()
        {
            // NOT OP
            await AssertSteps("600019", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", 2);
        }

        [Fact]
        public async Task ShouldPOP_1()
        { // POP OP
            await AssertSteps("61000060016200000250", "0000000000000000000000000000000000000000000000000000000000000001", 4);
          
        }

        [Fact]
        public async Task ShouldPOP_2()
        { // POP OP
            await AssertSteps("6100006001620000025050", "0000000000000000000000000000000000000000000000000000000000000000", 5);
        }

        [Fact]
        public async Task ShouldAdd4_4()
        {
            var x = 4 + 4;
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH1.ToHex()}04{Instruction.PUSH1.ToHex()}04{Instruction.ADD.ToHex()}",encoded,3);
        }


        [Fact]
        public async Task ShouldAdd1234567_1234567()
        {
            var x = new BigInteger(1234567) + 1234567;
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH32.ToHex()}000000000000000000000000000000000000000000000000000000000012d687{Instruction.PUSH32.ToHex()}000000000000000000000000000000000000000000000000000000000012d687{Instruction.ADD.ToHex()}", encoded, 3);
        }

        //000000000000000000000000000000000000000000000000000000000012d687
        //1234567


        [Fact]
        public async Task ShouldAddMod2_2_3()
        {
            var x = (2 + 2) % 3;
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH1.ToHex()}03{Instruction.PUSH1.ToHex()}02{Instruction.PUSH1.ToHex()}02{Instruction.ADDMOD.ToHex()}", encoded, 4);
        }

        [Fact]
        public async Task ShouldAddMod2_276_274()
        {
            var x = (2 + 276) % 274;
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH2.ToHex()}0112{Instruction.PUSH1.ToHex()}02{Instruction.PUSH2.ToHex()}0114{Instruction.ADDMOD.ToHex()}", encoded, 4);
        }

        [Fact]
        public async Task ShouldAddMod1234567_1234567_3()
        {
            var x = (new BigInteger(1234567) + 1234567) % 3;
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH1.ToHex()}03{Instruction.PUSH32.ToHex()}000000000000000000000000000000000000000000000000000000000012d687{Instruction.PUSH32.ToHex()}000000000000000000000000000000000000000000000000000000000012d687{Instruction.ADDMOD.ToHex()}", encoded, 4);
        }

        [Fact]
        public async Task ShouldExp()
        {
            var x = BigInteger.ModPow(2, 3, BigInteger.Pow(2, 256));
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH1.ToHex()}03{Instruction.PUSH1.ToHex()}02{Instruction.EXP.ToHex()}", encoded, 3);
        }

        [Fact]
        public async Task ShouldExp1()
        {
            var x = BigInteger.ModPow(123, 0, BigInteger.Pow(2, 256));
            var encoded = x.EncodeAbiHex();
            await AssertSteps($"{Instruction.PUSH1.ToHex()}00{Instruction.PUSH1.ToHex()}7B{Instruction.EXP.ToHex()}", encoded, 3);
        }


        [Fact]
        public async Task ShouldSha3()
        {
            await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}00{Instruction.MSTORE8.ToHex()}{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}00{Instruction.KECCAK256.ToHex()}", "5fe7f977e71dba2ea1a68e21057beebb9be2ac30c6410aa38d4f3fbe41dcffd2", 6);
        }

        [Fact]
        public async Task ShouldSha3_1()
        {
            await AssertSteps($"{Instruction.PUSH2.ToHex()}0201{Instruction.PUSH1.ToHex()}00{Instruction.MSTORE.ToHex()}{Instruction.PUSH1.ToHex()}05{Instruction.PUSH1.ToHex()}1b{Instruction.KECCAK256.ToHex()}", "20720BFCC1D580BFEEDB4385B267D7B641DD58801B5986A19B66AA76DC5D8969", 6);
        }

        [Fact]
        public async Task ShouldJump()
        {
            await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}05{Instruction.JUMPI.ToHex()}{Instruction.JUMPDEST.ToHex()}{Instruction.PUSH1.ToHex()}CC", "00000000000000000000000000000000000000000000000000000000000000CC", 5);
        }

        [Fact]
        public async Task ShouldShl()
        {
            await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}00{Instruction.SHL.ToHex()}", "0000000000000000000000000000000000000000000000000000000000000001", 4);
            await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}01{Instruction.SHL.ToHex()}", "0000000000000000000000000000000000000000000000000000000000000002", 4);
            await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH1.ToHex()}ff{Instruction.SHL.ToHex()}", "8000000000000000000000000000000000000000000000000000000000000000", 4);
            
            ////overflow
            ////await AssertSteps($"{Instruction.PUSH2.ToHex()}0100{Instruction.PUSH1.ToHex()}01{Instruction.SHL.ToHex()}", "0x0000000000000000000000000000000000000000000000000000000000000000", 4);

            //await AssertSteps($"{Instruction.PUSH32.ToHex()}ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff{Instruction.PUSH1.ToHex()}01{Instruction.SHL.ToHex()}", "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe", 4);
            
            //await AssertSteps($"{Instruction.PUSH1.ToHex()}ff{Instruction.PUSH32.ToHex()}ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff{Instruction.SHL.ToHex()}", "8000000000000000000000000000000000000000000000000000000000000000", 4);
            
            ////overflow
            ////await AssertSteps($"{Instruction.PUSH2.ToHex()}0100{Instruction.PUSH32.ToHex()}ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff{Instruction.SHL.ToHex()}", "8000000000000000000000000000000000000000000000000000000000000000", 4);
            
            ////overflow
            ////await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH32.ToHex()}0000000000000000000000000000000000000000000000000000000000000000{Instruction.SHL.ToHex()}", "x0000000000000000000000000000000000000000000000000000000000000000", 4);

            //await AssertSteps($"{Instruction.PUSH1.ToHex()}01{Instruction.PUSH32.ToHex()}7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff{Instruction.SHL.ToHex()}", "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe", 4);
        }

        private async Task AssertSteps(string hexBytes, string expected, int numberOfSteps = 1)
        {
            var vm = new EVMSimulator();
            var program = new Program(hexBytes.HexToByteArray());
            for (var i = 0; i < numberOfSteps; i++)
            {
                await vm.StepAsync(program);
            }

            Assert.Equal(expected.ToUpper(), program.StackPeek().ToHex().ToUpper());
        }
    }

    public static class Extensions
    {
        public static string ToHex(this Instruction instruction)
        {
            return ((byte)instruction).ToString("x2");
        }

        public static string EncodeAbiHex(this BigInteger value)
        {
            return new IntType("int256").Encode(value).ToHex();
        }

        public static string EncodeAbiHex(this int value)
        {
            return new IntType("int256").Encode(value).ToHex();
        }
    }
}