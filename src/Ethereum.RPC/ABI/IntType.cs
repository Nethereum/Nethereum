using System;
using System.Linq;
using System.Numerics;
using Ethereum.RPC.Tests;

namespace Ethereum.ABI.Tests.DNX
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
            System.Numerics.BigInteger bigInt;

            if(value is string)
            {
                bigInt = (BigInteger)DecodeString((string)value);
            }
            //if (value is string)
            //{
            //    string s = ((string)value).ToLower().Trim();
            //    int radix = 10;
            //    if (s.StartsWith("0x", StringComparison.Ordinal))
            //    {
            //        s = s.Substring(2);
            //        radix = 16;
            //    }
            //    else if (s.Contains("a") || s.Contains("b") || s.Contains("c") || s.Contains("d") || s.Contains("e") || s.Contains("f"))
            //    {
            //        radix = 16;
            //    }
            //    bigInt = new BigInteger(s, radix);
            //}
            else if (value is BigInteger)
            {
                bigInt = (BigInteger)value;
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

        public virtual object DecodeString(string value)
        {
            string valueString = (string) value;
            if (valueString.StartsWith("0x"))
            {
                valueString = valueString.Substring(2);
            }

            //Force unsigned  when doing string conversion
            valueString = "0" + valueString;

            var bigInt = BigInteger.Parse(valueString, System.Globalization.NumberStyles.HexNumber);
            return bigInt;
        }

        public override object Decode(byte[] encoded)
        {
            return new System.Numerics.BigInteger(encoded);
        }

        public static byte[] EncodeInt(int i)
        {
            return EncodeInt(new System.Numerics.BigInteger(i));
        }
        public static byte[] EncodeInt(System.Numerics.BigInteger bigInt)
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