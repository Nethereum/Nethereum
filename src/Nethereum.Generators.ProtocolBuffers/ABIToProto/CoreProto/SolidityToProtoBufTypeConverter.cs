using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto
{
    public class SolidityToProtoBufTypeConverter : ISolidityToProtoBufTypeConverter
    {
        private static readonly string[] _solidityToPbNumericTypes =
        {
            "int", "uint", "sint", "fixed", "sfixed"
        };

        private static readonly Dictionary<string, string> _oneToOneMappings = new Dictionary<string, string>
        {
            {"string", "string"},
            {"bytes", "bytes"},
            {"bool", "bool"},
            {"address", "string"}
        };

        private readonly Dictionary<string, string> _typeMappingCache = new Dictionary<string, string>();
        private static readonly object _lockObject = new object();

        public string Convert(string abiType)
        {
            //TryGetValue is not supported in Javascript
            if (_typeMappingCache.ContainsKey(abiType))
            {
                return _typeMappingCache[abiType];
            }

            lock (_lockObject)
            {
                if (_typeMappingCache.ContainsKey(abiType))
                {
                    return _typeMappingCache[abiType];
                }

                var mappedType = ExecuteConvert(abiType);
                _typeMappingCache.Add(abiType, mappedType);

                return mappedType;
            }
        }

        private string ExecuteConvert(string abiType)
        {
            //TryGetValue is not supported in Javascript
            if (_oneToOneMappings.ContainsKey(abiType))
            {
                return _oneToOneMappings[abiType];
            }
            
            if (abiType.StartsWith("byte"))
                return "bytes";

            var isArray = abiType.EndsWith("[]");

            var abiItemtype = isArray ? abiType.TrimEnd('[', ']') : abiType;

            var matchingPbPrefix = _solidityToPbNumericTypes.FirstOrDefault(abiItemtype.StartsWith);

            if (matchingPbPrefix == null)
                return CreateProtoMessageFieldDeclaration(abiItemtype, isArray);

            var size = ParseAbiSize(abiItemtype);

            if (size > 64)
            {
                return "bytes";
            }

            return CreateProtoMessageFieldDeclaration(matchingPbPrefix + size, isArray);
        }

        string CreateProtoMessageFieldDeclaration(string protoType, bool isArray)
        {
            return isArray ? "repeated " + protoType : protoType;
        }

        int ParseAbiSize(string abiType)
        {
             if (!Char.IsDigit(abiType.Last()))
                return 32;

            var indexOfFirstNumber = 0;
            foreach (var character in abiType)
            {
                if (Char.IsDigit(character)) break;
                indexOfFirstNumber = indexOfFirstNumber + 1;
            }

            if (indexOfFirstNumber < 1)
                return 32;

            var abiLength = int.Parse(abiType.Substring(indexOfFirstNumber));

            if (abiLength < 33)
                return 32;
            if(abiLength >= 32 && abiLength <= 64)
                return 64;

            return abiLength;
        }

        /*
SOLIDITY (ABI) TYPES
uint<M>: unsigned integer type of M bits, 0 < M <= 256, M % 8 == 0. e.g. uint32, uint8, uint256.
int<M>: two’s complement signed integer type of M bits, 0 < M <= 256, M % 8 == 0.
address: equivalent to uint160, except for the assumed interpretation and language typing. For computing the function selector, address is used.
uint, int: synonyms for uint256, int256 respectively. For computing the function selector, uint256 and int256 have to be used.
bool: equivalent to uint8 restricted to the values 0 and 1. For computing the function selector, bool is used.
fixed<M>x<N>: signed fixed-point decimal number of M bits, 8 <= M <= 256, M % 8 ==0, and 0 < N <= 80, which denotes the value v as v / (10 ** N).
ufixed<M>x<N>: unsigned variant of fixed<M>x<N>.
fixed, ufixed: synonyms for fixed128x19, ufixed128x19 respectively. For computing the function selector, fixed128x19 and ufixed128x19 have to be used.
bytes<M>: binary type of M bytes, 0 < M <= 32.
function: an address (20 bytes) followed by a function selector (4 bytes). Encoded identical to bytes24.
The following (fixed-size) array type exists:

<type>[M]: a fixed-length array of M elements, M > 0, of the given type.
The following non-fixed-size types exist:

bytes: dynamic sized byte sequence.
string: dynamic sized unicode string assumed to be UTF-8 encoded.
<type>[]: a variable-length array of elements of the given type.
Types can be combined to a tuple by enclosing a finite non-negative number of them inside parentheses, separated by commas:

(T1,T2,...,Tn): tuple consisting of the types T1, …, Tn, n >= 0
         */

        /*
PROTOBUF TYPES
"double" | "float" | "int32" | "int64" | "uint32" | "uint64"
      | "sint32" | "sint64" | "fixed32" | "fixed64" | "sfixed32" | "sfixed64"
      | "bool" | "string" | "bytes" | messageType | enumType
         */

        public static string[] ProtobufTypes = {
            "double",
            "float",
            "int32", "int64",
            "uint32", "uint64",
            "sint32", "sint64",
            "fixed32", "fixed64",
            "sfixed32", "sfixed64",
            "bool",
            "string",
            "bytes" };
    }
}