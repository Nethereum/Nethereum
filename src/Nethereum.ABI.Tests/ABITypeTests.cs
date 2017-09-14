using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.Tests
{
    public class ABITypeTests
    {
        [Fact]
        public virtual void CreateABIType_Bytes16()
        {
            var type = ABIType.CreateABIType("bytes16");
            Assert.Equal(typeof(Bytes16Type), type.GetType());
        }

        [Fact]
        public virtual void CreateABIType_Bytes32()
        {
            var type = ABIType.CreateABIType("bytes32");
            Assert.Equal(typeof(Bytes32Type), type.GetType());
        }

        [Fact]
        public virtual void CreateABIType_Bytes()
        {
            var type = ABIType.CreateABIType("bytes");
            Assert.Equal(typeof(BytesType), type.GetType());
        }
    }
}