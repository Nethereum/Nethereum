using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class IntType : ABIType
    {
        public IntType(string name) : base(name)
        {
        }

        public override string CanonicalName
        {
            get
            {
                if (Name.Equals("int"))
                {
                    return "int256";
                }
                if (Name.Equals("uint"))
                {
                    return "uint256";
                }
                return base.CanonicalName;
            }
        }

        public override byte[] Encode(object value)
        {
            BigInteger bigInt;

            var stringValue = value as string;

            if (stringValue != null)
            {
                bigInt = (BigInteger) DecodeString(stringValue);
            }
            else if (value is BigInteger)
            {
                bigInt = (BigInteger) value;
            }
            else if (value.IsNumber())
            {
                bigInt = BigInteger.Parse(value.ToString());
            }
            else
            {
                throw new Exception("Invalid value for type '" + this + "': " + value + " (" + value.GetType() + ")");
            }
            return EncodeInt(bigInt);
        }

       

        public override object Decode(byte[] encoded)
        {
            bool paddedPrefix = true;
            var unpaddedBytes = new List<byte>();
             
            foreach (byte item in encoded)
            {
                if (!(item == 0 || item == 0xFF) && paddedPrefix)
                {
                    paddedPrefix = false;
                }

                if (!paddedPrefix)
                {
                    unpaddedBytes.Add(item);
                }
            }

            if (!unpaddedBytes.Any()) unpaddedBytes.Add(encoded.Last());

            if (BitConverter.IsLittleEndian)
            {
                encoded = unpaddedBytes.ToArray().Reverse().ToArray();
            }

            return new BigInteger(encoded);
        }

        public static byte[] EncodeInt(int i)
        {
            return EncodeInt(new BigInteger(i));
        }
        public static byte[] EncodeInt(BigInteger bigInt)
        {
            byte[] ret = new byte[32];

            for (int i=0; i<ret.Length; i++)
            {
                if (bigInt.Sign < 0)
                {
                    ret[i] = 0xFF;
                }
                else
                {
                    ret[i] = 0;
                }
            }

            byte[] bytes;

            //It should always be Big Endian.
            if (BitConverter.IsLittleEndian)
            {
                bytes = bigInt.ToByteArray().Reverse().ToArray();
            }
            else
            {
                bytes = bigInt.ToByteArray().ToArray();
            }

            Array.Copy(bytes, 0, ret, 32 - bytes.Length, bytes.Length);

            return ret;
        }
    }
}