using System;
using System.Numerics;

namespace Ethereum.RPC
{
    public class HexRPCTypeFactory
    {
        public static object GetHexRPCType<T>(string hex)
        {
            if (typeof (BigInteger) == typeof(T))
            {
                return new HexBigInteger(hex);
            }

            throw new NotImplementedException();
        }
    }
}