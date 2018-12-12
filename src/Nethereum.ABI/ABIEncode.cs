using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.Util;
using Nethereum.Util;

namespace Nethereum.ABI
{
    public class ABIEncode
    {

        public byte[] GetSha3ABIEncodedPacked(params ABIValue[] abiValues)
        {
            return new Sha3Keccack().CalculateHash(GetABIEncodedPacked(abiValues));
        }

        public byte[] GetSha3ABIEncodedPacked(params object[] values)
        {
            return new Sha3Keccack().CalculateHash(GetABIEncodedPacked(values));
        }

        public byte[] GetABIEncodedPacked(params ABIValue[] abiValues)
        {
            var result = new List<byte>();
            foreach (var abiValue in abiValues)
            {
                result.AddRange(abiValue.ABIType.EncodePacked(abiValue.Value));
            }
            return result.ToArray();
        }

        public byte[] GetABIEncodedPacked(params object[] values)
        {
            var abiValues = new List<ABIValue>();
            foreach (var value in values)
            {
                if (value.IsNumber())
                {
                    var bigInt = BigInteger.Parse(value.ToString());
                    if (bigInt >= 0)
                    {
                        abiValues.Add(new ABIValue(new IntType("uint256"), value));
                    }
                    else
                    {
                        abiValues.Add(new ABIValue(new IntType("int256"), value));
                    }
                }

                if (value is string)
                {
                    abiValues.Add(new ABIValue(new StringType(), value));
                }

                if (value is bool)
                {
                    abiValues.Add(new ABIValue(new BoolType(), value));
                }

                if (value is byte[])
                {
                    abiValues.Add(new ABIValue(new BytesType(), value));
                }
            }
            return GetABIEncodedPacked(abiValues.ToArray());
        }

    }
}