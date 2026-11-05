using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Util;
using System.Collections;
using System.Numerics;
using System;

namespace Nethereum.ABI.EIP712
{
    public class Eip712TypedDataEncoder
    {
        public static Eip712TypedDataEncoder Current { get; } = new Eip712TypedDataEncoder();
        private readonly ParametersEncoder _parametersEncoder = new ParametersEncoder();

        public byte[] EncodeTypedData<T, TDomain>(T message, TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            return EncodeTypedDataRaw(typedData.ToRaw(MemberValueFactory.CreateFromMessage(message)));
        }

        public byte[] EncodeTypedData<T, TDomain>(T data, TDomain domain, string primaryTypeName)
        {
            var typedData = GenerateTypedData(data, domain, primaryTypeName);

            return EncodeTypedData(typedData);
        }

        public byte[] EncodeTypedData(string json)
        {
            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json);
            return EncodeTypedDataRaw(typedDataRaw);
        }

        public byte[] EncodeAndHashTypedData(string json)
        {
            var encodedData = EncodeTypedData(json);
            return Sha3Keccack.Current.CalculateHash(encodedData);
        }

        public byte[] EncodeTypedData<DomainType>(string json, string messageKeySelector = "message")
        {
            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData<DomainType>(json, messageKeySelector);
            return EncodeTypedDataRaw(typedDataRaw);
        }

        public byte[] EncodeAndHashTypedData<TDomain>(string json, string messageKeySelector = "message")
        {
            var encodedData = EncodeTypedData<TDomain>(json, messageKeySelector);
            return Sha3Keccack.Current.CalculateHash(encodedData);
        }

        public byte[] EncodeAndHashTypedData<T, TDomain>(T message, TypedData<TDomain> typedData)
        {
            var encodedData = EncodeTypedData(message, typedData);
            return Sha3Keccack.Current.CalculateHash(encodedData);
        }

        public byte[] EncodeAndHashTypedData<TDomain>(TypedData<TDomain> typedData)
        {
            var encodedData = EncodeTypedData(typedData);
            return Sha3Keccack.Current.CalculateHash(encodedData);
        }

        public byte[] EncodeTypedData<TDomain>(TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            return EncodeTypedDataRaw(typedData.ToRaw(typedData.Message));
        }

        public byte[] EncodeTypedDataRaw(TypedDataRaw typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            var context = new EncodingContext(typedData.Types);
            var domainHash = HashStruct(context, "EIP712Domain", typedData.DomainRawValues);
            var messageHash = HashStruct(context, typedData.PrimaryType, typedData.Message);
            return EncodeTypedDataBytes(domainHash, messageHash);
        }

        private static byte[] EncodeTypedDataBytes(byte[] domainHash, byte[] messageHash)
        {
            var result = new byte[2 + domainHash.Length + messageHash.Length];
            result[0] = 0x19;
            result[1] = 0x01;
            Buffer.BlockCopy(domainHash, 0, result, 2, domainHash.Length);
            Buffer.BlockCopy(messageHash, 0, result, 2 + domainHash.Length, messageHash.Length);
            return result;
        }

        public byte[] HashDomainSeparator<TDomain>(TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            return HashDomainSeparator(typedData.ToRaw(typedData.Message));
        }

        public byte[] HashDomainSeparator(TypedDataRaw typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            if (typedData.DomainRawValues == null)
            {
                throw new ArgumentException("Typed data domain values have not been initialised.", nameof(typedData));
            }

            return HashStruct(new EncodingContext(typedData.Types), "EIP712Domain", typedData.DomainRawValues);
        }

        public byte[] HashStruct<T>(T message, string primaryType, params Type[] types)
        {
            var memberDescriptions = MemberDescriptionFactory.GetTypesMemberDescription(types);
            var memberValue = MemberValueFactory.CreateFromMessage(message);
            return HashStruct(new EncodingContext(memberDescriptions), primaryType, memberValue);
        }

        public byte[] EncodeStruct<T>(T message, string primaryType, params Type[] types)
        {
            var memberDescriptions = MemberDescriptionFactory.GetTypesMemberDescription(types);
            var memberValue = MemberValueFactory.CreateFromMessage(message);
            return EncodeStruct(new EncodingContext(memberDescriptions), primaryType, memberValue);
        }

        public string GetEncodedType(string primaryType, params Type[] types)
        {
            var memberDescriptions = MemberDescriptionFactory.GetTypesMemberDescription(types);
            return new EncodingContext(memberDescriptions).EncodeType(primaryType);
        }

        public string GetEncodedTypeDomainSeparator<TDomain>(TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            return new EncodingContext(typedData.Types).EncodeType("EIP712Domain");
        }

        public TypedDataHashResult CalculateTypedDataHashes<TDomain>(TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            if (typedData.Message == null)
            {
                throw new ArgumentException("Typed data message has not been initialised. Call SetMessage before calculating hashes.", nameof(typedData));
            }

            return CalculateTypedDataHashes(typedData.ToRaw(typedData.Message));
        }

        public TypedDataHashResult CalculateTypedDataHashes<TDomain, TMessage>(TMessage message, TypedData<TDomain> typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            return CalculateTypedDataHashes(typedData.ToRaw(MemberValueFactory.CreateFromMessage(message)));
        }

        public TypedDataHashResult CalculateTypedDataHashes(string json)
        {
            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json);
            return CalculateTypedDataHashes(typedDataRaw);
        }

        public TypedDataHashResult CalculateTypedDataHashes<TDomain>(string json, string messageKeySelector = "message")
        {
            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData<TDomain>(json, messageKeySelector);
            return CalculateTypedDataHashes(typedDataRaw);
        }

        public TypedDataHashResult CalculateTypedDataHashes(TypedDataRaw typedData)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            if (typedData.DomainRawValues == null)
            {
                throw new ArgumentException("Typed data domain values have not been initialised.", nameof(typedData));
            }

            if (typedData.Message == null)
            {
                throw new ArgumentException("Typed data message has not been initialised.", nameof(typedData));
            }

            var context = new EncodingContext(typedData.Types);
            var domainHash = HashStruct(context, "EIP712Domain", typedData.DomainRawValues);
            var messageHash = HashStruct(context, typedData.PrimaryType, typedData.Message);
            var typedDataHash = Sha3Keccack.Current.CalculateHash(EncodeTypedDataBytes(domainHash, messageHash));

            return new TypedDataHashResult(domainHash, messageHash, typedDataHash);
        }

        private static byte[] HashStruct(EncodingContext context, string typeName, IEnumerable<MemberValue> message)
        {
            return Sha3Keccack.Current.CalculateHash(EncodeStruct(context, typeName, message));
        }

        private static byte[] EncodeStruct(EncodingContext context, string typeName, IEnumerable<MemberValue> message)
        {
            if (message == null) throw new ArgumentException("The value of struct '" + typeName + "' is missing.");
            var members = context.GetMembers(typeName);
            var values = message as IList<MemberValue> ?? message.ToList();
            if (values.Count != members.Length)
            {
                throw new ArgumentException("The value of struct '" + typeName + "' has " + values.Count + " members but its type declares " + members.Length + ".");
            }

            using (var memoryStream = new MemoryStream())
            {
                var typeHash = context.GetTypeHash(typeName);
                memoryStream.Write(typeHash, 0, typeHash.Length);
                for (var i = 0; i < members.Length; i++)
                {
                    var encoded = EncodeValue(context, members[i].Type, values[i]?.Value);
                    memoryStream.Write(encoded, 0, encoded.Length);
                }
                return memoryStream.ToArray();
            }
        }

        private static byte[] EncodeValue(EncodingContext context, string type, object value)
        {
            if (type.EndsWith("]", StringComparison.Ordinal))
            {
                return EncodeArray(context, type, value);
            }

            if (context.IsStruct(type))
            {
                var structValue = value as IEnumerable<MemberValue>;
                if (structValue == null) throw DataMismatch(type, value);
                return HashStruct(context, type, structValue);
            }

            return EncodePrimitive(type, value);
        }

        private static byte[] EncodeArray(EncodingContext context, string type, object value)
        {
            var items = value as IList;
            if (items == null || value is string) throw DataMismatch(type, value);
            var elementType = type.Substring(0, type.LastIndexOf('['));
            using (var memoryStream = new MemoryStream())
            {
                foreach (var item in items)
                {
                    var encoded = EncodeValue(context, elementType, item);
                    memoryStream.Write(encoded, 0, encoded.Length);
                }
                return Sha3Keccack.Current.CalculateHash(memoryStream.ToArray());
            }
        }

        private static byte[] EncodePrimitive(string type, object value)
        {
            switch (type)
            {
                case "address":
                    return EncodeAddress(value);
                case "bool":
                    if (!(value is bool flag)) throw DataMismatch(type, value);
                    var boolWord = new byte[32];
                    if (flag) boolWord[31] = 1;
                    return boolWord;
                case "string":
                    if (!(value is string text)) throw DataMismatch(type, value);
                    return Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(text));
                case "bytes":
                    if (!TryGetBytes(value, out var dynamicBytes)) throw DataMismatch(type, value);
                    return Sha3Keccack.Current.CalculateHash(dynamicBytes);
            }

            if (Eip712Values.TryGetBytesWidth(type, out var bytesWidth))
            {
                if (!TryGetBytes(value, out var fixedBytes) || fixedBytes.Length != bytesWidth) throw DataMismatch(type, value);
                var word = new byte[32];
                Buffer.BlockCopy(fixedBytes, 0, word, 0, fixedBytes.Length);
                return word;
            }

            if (Eip712Values.TryGetIntegerWidth(type, out var width, out var signed))
            {
                return EncodeInteger(type, value, width, signed);
            }

            throw new ArgumentException("Unrecognized type '" + type + "'.");
        }

        private static byte[] EncodeAddress(object value)
        {
            var word = new byte[32];
            if (value is string emptyAddress && emptyAddress == AddressUtil.AddressEmptyAsHex)
            {
                return word;
            }

            if (value is string text && Eip712Values.IsHexAddress(text))
            {
                Eip712Values.TryParseHexBytes(text.Length == 42 ? text : "0x" + text, out var parsed);
                Buffer.BlockCopy(parsed, 0, word, 12, 20);
                return word;
            }

            if (value is byte[] bytes && bytes.Length == 20)
            {
                Buffer.BlockCopy(bytes, 0, word, 12, 20);
                return word;
            }

            throw DataMismatch("address", value);
        }

        private static byte[] EncodeInteger(string type, object value, int width, bool signed)
        {
            if (!Eip712Values.TryParseInteger(value, out var number)) throw DataMismatch(type, value);
            var limit = BigInteger.One << (signed ? width - 1 : width);
            var inRange = signed ? number >= -limit && number < limit : number.Sign >= 0 && number < limit;
            if (!inRange) throw new ArgumentException("The value '" + number + "' is out of range for type '" + type + "'.");

            if (number.Sign < 0) number += BigInteger.One << 256;
            var littleEndian = number.ToByteArray();
            var word = new byte[32];
            var length = Math.Min(littleEndian.Length, 32);
            for (var i = 0; i < length; i++)
            {
                word[31 - i] = littleEndian[i];
            }
            return word;
        }

        private static bool TryGetBytes(object value, out byte[] bytes)
        {
            bytes = value as byte[];
            if (bytes != null) return true;
            return Eip712Values.TryParseHexBytes(value as string, out bytes);
        }

        private static ArgumentException DataMismatch(string type, object value)
        {
            return new ArgumentException("The provided data '" + value + "' does not match type '" + type + "'.");
        }

        private sealed class EncodingContext
        {
            private readonly IDictionary<string, MemberDescription[]> _types;
            private readonly Dictionary<string, byte[]> _typeHashes = new Dictionary<string, byte[]>();

            public EncodingContext(IDictionary<string, MemberDescription[]> types)
            {
                _types = types ?? throw new ArgumentException("Typed data types are missing.");
                Validate();
            }

            public bool IsStruct(string type)
            {
                return _types.ContainsKey(type);
            }

            public MemberDescription[] GetMembers(string typeName)
            {
                if (typeName == null || !_types.TryGetValue(typeName, out var members))
                {
                    throw new ArgumentException("Type '" + typeName + "' is not defined.");
                }
                return members;
            }

            public byte[] GetTypeHash(string typeName)
            {
                if (!_typeHashes.TryGetValue(typeName, out var hash))
                {
                    hash = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(EncodeType(typeName)));
                    _typeHashes[typeName] = hash;
                }
                return hash;
            }

            public string EncodeType(string primaryType)
            {
                GetMembers(primaryType);
                var found = new List<string>();
                var visited = new HashSet<string>();
                CollectDependencies(primaryType, found, visited);

                var dependencies = found.Where(x => x != primaryType).ToList();
                dependencies.Sort(StringComparer.Ordinal);
                dependencies.Insert(0, primaryType);

                var builder = new StringBuilder();
                foreach (var dependency in dependencies)
                {
                    builder.Append(dependency).Append('(');
                    builder.Append(string.Join(",", _types[dependency].Select(x => x.Type + " " + x.Name)));
                    builder.Append(')');
                }
                return builder.ToString();
            }

            private void CollectDependencies(string type, List<string> found, HashSet<string> visited)
            {
                var bracket = type.IndexOf('[');
                var name = bracket >= 0 ? type.Substring(0, bracket) : type;
                if (visited.Contains(name)) return;
                if (!_types.TryGetValue(name, out var members)) return;
                visited.Add(name);
                found.Add(name);
                foreach (var member in members)
                {
                    CollectDependencies(member.Type, found, visited);
                }
            }

            private void Validate()
            {
                foreach (var entry in _types)
                {
                    if (string.IsNullOrEmpty(entry.Key)) throw new ArgumentException("A type has an empty name.");
                    if (entry.Value == null) throw new ArgumentException("Type '" + entry.Key + "' has no members.");
                    for (var i = 0; i < entry.Value.Length; i++)
                    {
                        var member = entry.Value[i];
                        if (member == null || string.IsNullOrEmpty(member.Type))
                            throw new ArgumentException("Type '" + entry.Key + "' member " + i + " has an empty type.");
                        if (string.IsNullOrEmpty(member.Name))
                            throw new ArgumentException("Type '" + entry.Key + "' member " + i + " has an empty name.");
                        if (member.Type == entry.Key)
                            throw new ArgumentException("Type '" + entry.Key + "' cannot reference itself.");

                        var bracket = member.Type.IndexOf('[');
                        var baseType = bracket >= 0 ? member.Type.Substring(0, bracket) : member.Type;
                        if (!_types.ContainsKey(baseType) && !Eip712Values.IsPrimitiveType(baseType))
                            throw new ArgumentException("Type '" + entry.Key + "' member '" + member.Name + "' has the unrecognized type '" + member.Type + "'.");
                    }
                }
            }
        }

        public TypedData<TDomain> GenerateTypedData<T, TDomain>(T data, TDomain domain, string primaryTypeName)
        {
            var parameters = _parametersEncoder.GetParameterAttributeValues(typeof(T), data).OrderBy(x => x.ParameterAttribute.Order);

            var typeMembers = new List<MemberDescription>();
            var typeValues = new List<MemberValue>();
            foreach (var parameterAttributeValue in parameters)
            {
                typeMembers.Add(new MemberDescription
                {
                    Type = parameterAttributeValue.ParameterAttribute.Type,
                    Name = parameterAttributeValue.ParameterAttribute.Name
                });

                typeValues.Add(new MemberValue
                {
                    TypeName = parameterAttributeValue.ParameterAttribute.Type,
                    Value = parameterAttributeValue.Value
                });
            }

            var result = new TypedData<TDomain>
            {
                PrimaryType = primaryTypeName,
                Types = new Dictionary<string, MemberDescription[]>
                {
                    [primaryTypeName] = typeMembers.ToArray(),
                    ["EIP712Domain"] = MemberDescriptionFactory.GetTypesMemberDescription(typeof(TDomain))["EIP712Domain"]
                },
                Message = typeValues.ToArray(),
                Domain = domain
            };

            return result;
        }
    }

    public sealed class TypedDataHashResult
    {
        public TypedDataHashResult(byte[] domainHash, byte[] messageHash, byte[] typedDataHash)
        {
            DomainHash = domainHash ?? throw new ArgumentNullException(nameof(domainHash));
            MessageHash = messageHash ?? throw new ArgumentNullException(nameof(messageHash));
            TypedDataHash = typedDataHash ?? throw new ArgumentNullException(nameof(typedDataHash));
        }

        public byte[] DomainHash { get; }
        public byte[] MessageHash { get; }
        public byte[] TypedDataHash { get; }
    }
}
