using System.Text;
using Ethereum.RPC.Util;
using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class ByteUtilTest
    {

        [Fact]
        public virtual void TestAppendByte()
        {
            //sbyte[] bytes = "tes"
            byte[] bytes = Encoding.UTF8.GetBytes("tes");
            byte tByte = 0x74;

            byte[] expectedbytes = Encoding.UTF8.GetBytes("test");

            Assert.Equal(expectedbytes, ByteUtil.AppendByte(bytes, tByte));
                       
        }

        //[Fact]
        //public virtual void TestBigIntegerToBytes()
        //{
        //    byte[] expected = new byte[] { unchecked((byte)0xff), unchecked((byte)0xec), 0x78 };
        //    BigInteger b = BigInteger.Parse("16772216");
        //    byte[] actual = b.ToByteArray();
        //    //ByteUtil.BigIntegerToBytes(b);
        //  //  Assert.Equal(expected, actual);

        //    //different order of bytes expected (little indian / big indian ??) plus extra 0 to indicate positive.
        //    //255,236,120
        //    //120,236,255,0

        //}

    }
}