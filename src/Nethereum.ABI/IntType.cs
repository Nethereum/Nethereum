using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class IntType : ABIType
    {
        public static BigInteger MAX_INT256_VALUE =
            BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819967");

        public static BigInteger MIN_INT256_VALUE =
            BigInteger.Parse("-57896044618658097711785492504343953926634992332820282019728792003956564819968");

        public static BigInteger MAX_UINT256_VALUE =
            BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");

        public static BigInteger MIN_UINT_VALUE = 0;

        private static readonly Dictionary<uint, BigInteger> _maxUnsignedCache = new Dictionary<uint, BigInteger>();
        private static readonly Dictionary<uint, BigInteger> _maxSignedCache = new Dictionary<uint, BigInteger>();
        private static readonly Dictionary<uint, BigInteger> _minSignedCache = new Dictionary<uint, BigInteger>();
        private static readonly object _cacheLock = new object();

        public override int StaticSize { get; }

        public IntType(string name) : base(name)
        {
            var size = GetSize(CanonicalName);
            StaticSize = (int) size / 8;
            Decoder = new IntTypeDecoder(IsSigned(CanonicalName));
            Encoder = new IntTypeEncoder(IsSigned(CanonicalName), size);
        }

        public override string CanonicalName
        {
            get
            {
                if (Name.Equals("int"))
                    return "int256";
                if (Name.Equals("uint"))
                    return "uint256";
                return base.CanonicalName;
            }
        }

        private static bool IsSigned(string name)
        {
            return !name.ToLower().StartsWith("u");
        }

        public static BigInteger GetMaxSignedValue(uint size)
        {
            CheckIsValidAndThrow(size);
            lock (_cacheLock)
            {
                if (!_maxSignedCache.TryGetValue(size, out var result))
                {
                    result = BigInteger.Pow(2, (int)size - 1) - 1;
                    _maxSignedCache[size] = result;
                }
                return result;
            }
        }

        public static BigInteger GetMinSignedValue(uint size)
        {
            CheckIsValidAndThrow(size);
            lock (_cacheLock)
            {
                if (!_minSignedCache.TryGetValue(size, out var result))
                {
                    result = BigInteger.Pow(-2, (int)size - 1);
                    _minSignedCache[size] = result;
                }
                return result;
            }
        }

        public static BigInteger GetMaxUnSignedValue(uint size)
        {
            CheckIsValidAndThrow(size);
            lock (_cacheLock)
            {
                if (!_maxUnsignedCache.TryGetValue(size, out var result))
                {
                    result = BigInteger.Pow(2, (int)size) - 1;
                    _maxUnsignedCache[size] = result;
                }
                return result;
            }
        }

        private static void CheckIsValidAndThrow(uint size)
        {
            if (!IsValidSize(size)) throw new ArgumentException("Invalid size for type int :" + size);
        }

        public static bool IsValidSize(uint size)
        {
            var divisible = size % 8 == 0;
            return divisible && size <= 256 && size >= 8;
        }

        private static uint GetSize(string name)
        {
            if (IsSigned(name))
                return uint.Parse(name.Substring(3));
            return uint.Parse(name.Substring(4));
        }
    }
}