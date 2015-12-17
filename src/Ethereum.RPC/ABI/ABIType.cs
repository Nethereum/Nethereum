using System;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    /// <summary>
    /// Generic ABI type
    /// </summary>
    public abstract class ABIType
    {


        public ABIType(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// The type name as it was specified in the interface description
        /// </summary>
        public virtual string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// The canonical type name (used for the method signature creation)
        /// E.g. 'int' - canonical 'int256'
        /// </summary>
        public virtual string CanonicalName => Name;

        public static ABIType CreateABIType(string typeName)
        {
            if (typeName.Contains("["))
            {
                return ArrayType.CreateABIType(typeName);
            }
            if ("bool".Equals(typeName))
            {
                return new BoolType();
            }
            if (typeName.StartsWith("int", StringComparison.Ordinal) || typeName.StartsWith("uint", StringComparison.Ordinal))
            {
                return new IntType(typeName);
            }
            if ("address".Equals(typeName))
            {
                return new AddressType();
            }
            if ("string".Equals(typeName))
            {
                return new StringType();
            }
            if ("bytes".Equals(typeName))
            {
                return new BytesType();
            }
            if (typeName.StartsWith("bytes", StringComparison.Ordinal))
            {
                return new Bytes32Type(typeName);
            }
            throw new ArgumentException("Unknown type: " + typeName);
        }

        public virtual object DecodeString(string value)
        {
            if (!value.StartsWith("0x"))
            {
                value = "0x" + value;
            }

            return Decode(value.HexStringToByteArray());
        }

        /// <summary>
        /// Encodes the value according to specific type rules </summary>
        /// <param name="value"> </param>
        public abstract byte[] Encode(object value);

        public abstract object Decode(byte[] encoded);

        /// <returns> fixed size in bytes or negative value if the type is dynamic </returns>
        public virtual int FixedSize => 32;

        public bool IsDynamic()
        {
            return FixedSize < 0;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}