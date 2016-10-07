using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.Tests
{
    public class VmTests
    {
        public void AssertSteps(string hexBytes, string expected, int numberOfSteps = 1)
        {
            var vm = new VM();
            var program = new Program(hexBytes.HexToByteArray());
            for (var i = 0; i < numberOfSteps; i++)
            {
                vm.Step(program);
            }
 
            Assert.Equal(expected, program.Stack.Peek().ToHex().ToUpper());
        }

        [Fact]
        public void ShouldPUSH1()
        {
            // PUSH1 OP
            AssertSteps("60A0", "A0");
        }


        [Fact]
        public void ShouldPUSH10()
        {
            // PUSH10 OP


            AssertSteps("69A0B0C0D0E0F0A1B1C1D1"
                , "A0B0C0D0E0F0A1B1C1D1");
        }

        [Fact]
        public void ShouldPUSH11()
        {
            // PUSH11 OP


            AssertSteps("6AA0B0C0D0E0F0A1B1C1D1E1"
                , "A0B0C0D0E0F0A1B1C1D1E1");
        }

        [Fact]
        public void ShouldPUSH12()
        {
            // PUSH12 OP


            AssertSteps("6BA0B0C0D0E0F0A1B1C1D1E1F1"
                , "A0B0C0D0E0F0A1B1C1D1E1F1");
        }

        [Fact]
        public void ShouldPUSH13()
        {
            // PUSH13 OP


            AssertSteps("6CA0B0C0D0E0F0A1B1C1D1E1F1A2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2");
        }

        [Fact]
        public void ShouldPUSH14()
        {
            // PUSH14 OP


            AssertSteps("6DA0B0C0D0E0F0A1B1C1D1E1F1A2B2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2");
        }

        [Fact]
        public void ShouldPUSH15()
        {
            // PUSH15 OP


            AssertSteps("6EA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2");
        }

        [Fact]
        public void ShouldPUSH16()
        {
            // PUSH16 OP


            AssertSteps("6FA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2");
        }

        [Fact]
        public void ShouldPUSH17()
        {
            // PUSH17 OP


            AssertSteps("70A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2");
        }

        [Fact]
        public void ShouldPUSH18()
        {
            // PUSH18 OP


            AssertSteps("71A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2");
        }

        [Fact]
        public void ShouldPUSH19()
        {
            // PUSH19 OP


            AssertSteps("72A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3");
        }

        [Fact]
        public void ShouldPUSH2()
        {
            // PUSH2 OP

            AssertSteps("61A0B0"
                , "A0B0");
        }

        [Fact]
        public void ShouldPUSH20()
        {
            // PUSH20 OP


            AssertSteps("73A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3");
        }

        [Fact]
        public void ShouldPUSH21()
        {
            // PUSH21 OP


            AssertSteps("74A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3");
        }

        [Fact]
        public void ShouldPUSH22()
        {
            // PUSH22 OP


            AssertSteps("75A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3");
        }

        [Fact]
        public void ShouldPUSH23()
        {
            // PUSH23 OP


            AssertSteps("76A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3");
        }

        [Fact]
        public void ShouldPUSH24()
        {
            // PUSH24 OP


            AssertSteps("77A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3");
        }

        [Fact]
        public void ShouldPUSH25()
        {
            // PUSH25 OP


            AssertSteps("78A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4");
        }

        [Fact]
        public void ShouldPUSH26()
        {
            // PUSH26 OP

            AssertSteps("79A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4");
        }

        [Fact]
        public void ShouldPUSH27()
        {
            // PUSH27 OP

            AssertSteps("7AA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4");
        }

        [Fact]
        public void ShouldPUSH28()
        {
            // PUSH28 OP

            AssertSteps("7BA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4");
        }

        [Fact]
        public void ShouldPUSH29()
        {
            // PUSH29 OP

            AssertSteps("7CA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4");
        }

        [Fact]
        public void ShouldPUSH3()
        {
            // PUSH3 OP

            AssertSteps("62A0B0C0"
                , "A0B0C0");
        }

        [Fact]
        public void ShouldPUSH30()
        {
            // PUSH30 OP

            AssertSteps("7DA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4");
        }

        [Fact]
        public void ShouldPUSH31()
        {
            // PUSH31 OP

            AssertSteps("7EA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1");
        }

        [Fact]
        public void ShouldPUSH32()
        {
            // PUSH32 OP

            AssertSteps("7FA0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1B1"
                , "A0B0C0D0E0F0A1B1C1D1E1F1A2B2C2D2E2F2A3B3C3D3E3F3A4B4C4D4E4F4A1B1");
        }

        [Fact]
        public void ShouldPUSH4()
        {
            // PUSH4 OP


            AssertSteps("63A0B0C0D0"
                , "A0B0C0D0");
        }

        [Fact]
        public void ShouldPUSH5()
        {
            // PUSH5 OP


            AssertSteps("64A0B0C0D0E0"
                , "A0B0C0D0E0");
        }

        [Fact]
        public void ShouldPUSH6()
        {
            // PUSH6 OP


            AssertSteps("65A0B0C0D0E0F0"
                , "A0B0C0D0E0F0");
        }

        [Fact]
        public void ShouldPUSH7()
        {
            // PUSH7 OP

            AssertSteps("66A0B0C0D0E0F0A1"
                , "A0B0C0D0E0F0A1");
        }

        [Fact]
        public void ShouldPUSH8()
        {
            // PUSH8 OP


            AssertSteps("67A0B0C0D0E0F0A1B1"
                , "A0B0C0D0E0F0A1B1");
        }

        [Fact]
        public void ShouldPUSH9()
        {
            // PUSH9 OP


            AssertSteps("68A0B0C0D0E0F0A1B1C1"
                , "A0B0C0D0E0F0A1B1C1");
        }

        [Fact]
        public void ShouldAND_1()
        { // AND OP
            //only 1 byte?
            AssertSteps("600A600A16", "0A", 3);
        }

        [Fact]
        public void ShouldAND_2()
        {
            AssertSteps("60C0600A16","00", 3);
        }

        [Fact]
        public void ShouldStopAND_3()
        { // AND OP mal data

            var vm = new VM();
            var program = new Program("60C016".HexToByteArray());
            try
            {
                vm.Step(program);
                vm.Step(program);
                vm.Step(program);
            }
            finally
            {
                Assert.True(program.Stopped);
            }
        }

        [Fact]
        public void ShouldOR_1()
        { // OR OP
            AssertSteps("60F0600F17", "FF", 3);
        }

        [Fact]
        public void ShouldOR_2()
        { // OR OP
            AssertSteps("60C3603C17", "FF", 3);
        }

        [Fact]
        public void ShouldXOR_1()
        { // XOR OP
            AssertSteps("60FF60FF18", "00", 3);
          
        }
        [Fact]
        public void ShouldXOR_2()
        { // XOR OP
            AssertSteps("600F60F018", "FF", 3);
           
        }

        [Fact]
        public void ShouldBYTE_1()
        { // BYTE OP
            AssertSteps("65AABBCCDDEEFF601E1A", "EE", 3);
          
        }
        [Fact]
        public void TestBYTE_2()
        { // BYTE OP
            AssertSteps("65AABBCCDDEEFF60201A", "", 3);
           
        }
        [Fact]
        public void TestBYTE_3()
        { // BYTE OP
            AssertSteps("65AABBCCDDEE3A601F1A", "3A", 3);
        }

        [Fact]
        public void ShouldCheck_ISZERO_1()
        { // ISZERO OP
            AssertSteps("600015", "01", 2);
            
        }
        [Fact]
        public void TestCheck_ISZERO_2()
        { // ISZERO OP
            AssertSteps("602A15", "00", 2);
        }

        [Fact]
        public void ShouldCheck_EQ_1()
        { // EQ OP

            AssertSteps("602A602A14", "01", 3);
        }

        [Fact]
        public void ShouldCheck_EQ_2()
        { // EQ OP
            AssertSteps("622A3B4C622A3B4C14", "01", 3);
           
        }
        [Fact]
        public void ShouldCheck_EQ_3()
        { // EQ OP
            AssertSteps("622A3B5C622A3B4C14", "00", 3);
        }

        [Fact]
        public void ShouldCheck_GT_1()
        { // GT OP
            AssertSteps("6001600211", "01", 3);
        }

        [Fact]
        public void ShouldCheck_GT_2()
        { // GT OP
            AssertSteps("6001610F0011", "01", 3);
        }

        [Fact]
        public void ShouldCheck_GT_3()
        { // GT OP
            AssertSteps("6301020304610F0011", "00", 3);
        }

       
    }
}