using Nethereum.Hex.HexTypes;
using System;
using System.Numerics;

namespace Nethereum.Contracts
{
    public struct BlockRange: 
        IEquatable<BlockRange>
    {
        private readonly int _hashCode;

        public BlockRange(ulong from, ulong to):
            this(new BigInteger(from), new BigInteger(to))
        {
        }

        public BlockRange(BigInteger from, BigInteger to):
            this(new HexBigInteger(from), new HexBigInteger(to))
        {
        }

        public BlockRange(HexBigInteger from, HexBigInteger to)
        {
            From = from;
            To = to;
            BlockCount = (To.Value - From.Value) + 1;
            _hashCode = new { From, To }.GetHashCode();
        }


        public HexBigInteger From { get;  }
        public HexBigInteger To { get; }
        public BigInteger BlockCount { get; }

        public bool Equals(BlockRange other)
        {
            return From.Equals(other.From.Value) && To.Equals(other.To.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is BlockRange other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}