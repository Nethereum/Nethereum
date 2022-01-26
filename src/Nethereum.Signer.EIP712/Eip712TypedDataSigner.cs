using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Util;

namespace Nethereum.Signer.EIP712
{
    /// <summary>
    /// Implementation of EIP-712 signer
    /// https://github.com/ethereum/EIPs/blob/master/EIPS/eip-712.md
    /// </summary>
    public class Eip712TypedDataSigner
    {
        private readonly ABIEncode _abiEncode = new ABIEncode();
        private readonly EthereumMessageSigner _signer = new EthereumMessageSigner();
        private readonly ParametersEncoder _parametersEncoder = new ParametersEncoder();

        public static Eip712TypedDataSigner Current { get; } = new Eip712TypedDataSigner();

        /// <summary>
        /// Encodes data according to EIP-712, hashes it and signs with <paramref name="key"/>.
        /// Infers types of message fields from <see cref="Nethereum.ABI.FunctionEncoding.Attributes.ParameterAttribute"/>.
        /// For flat messages only, for complex messages with reference type fields use "SignTypedData(TypedData typedData, EthECKey key)" method.
        /// </summary>
        public string SignTypedData<T>(T data, Domain domain, string primaryTypeName, EthECKey key)
        {
            var typedData = GenerateTypedData(data, domain, primaryTypeName);

            return SignTypedData(typedData, key);
        }

        /// <summary>
        /// Encodes data according to EIP-712.
        /// Infers types of message fields from <see cref="Nethereum.ABI.FunctionEncoding.Attributes.ParameterAttribute"/>. 
        /// For flat messages only, for complex messages with reference type fields use "EncodeTypedData(TypedData typedData)" method.
        /// </summary>
        public byte[] EncodeTypedData<T>(T data, Domain domain, string primaryTypeName)
        {
            var typedData = GenerateTypedData(data, domain, primaryTypeName);

            return EncodeTypedData(typedData);
        }

        /// <summary>
        /// Encodes data according to EIP-712, hashes it and signs with <paramref name="key"/>.
        /// </summary>
        public string SignTypedData(TypedData typedData, EthECKey key)
        {
            var encodedData = EncodeTypedData(typedData);
            return _signer.HashAndSign(encodedData, key);
        }

        /// <summary>
        /// Encodes data according to EIP-712, hashes it and signs with <paramref name="key"/>.
        /// Matches the signature produced by eth_signTypedData_v4
        /// </summary>
        public string SignTypedDataV4(TypedData typedData, EthECKey key)
        {
            var encodedData = EncodeTypedData(typedData);
            var signature = key.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }

        public string RecoverFromSignatureV4(TypedData typedData, string signature)
        {
            var encodedData = EncodeTypedData(typedData);
            return  new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }

        public string RecoverFromSignatureV4(byte[] encodedData, string signature)
        {
            return new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }

        public string RecoverFromSignatureHashV4(byte[] hash, string signature)
        {
            return new MessageSigner().EcRecover(hash, signature);
        }

        /// <summary>
        /// Encodes data according to EIP-712.
        /// </summary>
        public byte[] EncodeTypedData(TypedData typedData)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write("1901".HexToByteArray());
                writer.Write(HashStruct(typedData.Types, "EIP712Domain", ParseDomain(typedData.Domain).Select(x => x.MemberValue)));
                writer.Write(HashStruct(typedData.Types, typedData.PrimaryType, typedData.Message));

                writer.Flush();
                var result = memoryStream.ToArray();
                return result;
            }
        }

        private byte[] HashStruct(IDictionary<string, MemberDescription[]> types, string primaryType, IEnumerable<MemberValue> message)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                var encodedType = EncodeType(types, primaryType);
                var typeHash = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(encodedType));
                writer.Write(typeHash);

                EncodeData(writer, types, message);

                writer.Flush();
                return Sha3Keccack.Current.CalculateHash(memoryStream.ToArray());
            }
        }

        private static string EncodeType(IDictionary<string, MemberDescription[]> types, string typeName)
        {
            var encodedTypes = EncodeTypes(types, typeName);
            var encodedPrimaryType = encodedTypes.Single(x => x.Key == typeName);
            var encodedReferenceTypes = encodedTypes.Where(x => x.Key != typeName).OrderBy(x => x.Key).Select(x => x.Value);
            var fullyEncodedType = encodedPrimaryType.Value + string.Join(string.Empty, encodedReferenceTypes);

            return fullyEncodedType;
        }

        // TODO: handle array types
        private static IList<KeyValuePair<string, string>> EncodeTypes(IDictionary<string, MemberDescription[]> types, string currentTypeName)
        {
            var currentTypeMembers = types[currentTypeName];
            var currentTypeMembersEncoded = currentTypeMembers.Select(x => x.Type + " " + x.Name);
            var result = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(currentTypeName, currentTypeName + "(" + string.Join(",", currentTypeMembersEncoded) + ")")
            };

            result.AddRange(currentTypeMembers.Select(x => x.Type).Distinct().Where(IsReferenceType).SelectMany(x => EncodeTypes(types, x)));

            return result;
        }

        private static bool IsReferenceType(string typeName)
        {
            switch (typeName)
            {
                // TODO: specify more precise conditions
                case var bytes when new Regex("bytes\\d+").IsMatch(bytes):
                case var @uint when new Regex("uint\\d+").IsMatch(@uint):
                case var @int when new Regex("int\\d+").IsMatch(@int):
                case "bytes":
                case "string":
                case "bool":
                case "address":
                    return false;
                case var array when array.Contains("["):
                    throw new NotImplementedException("Array types are not supported");
                default:
                    return true;
            }
        }

        private void EncodeData(BinaryWriter writer, IDictionary<string, MemberDescription[]> types, IEnumerable<MemberValue> memberValues)
        {
            foreach (var memberValue in memberValues)
            {
                switch (memberValue.TypeName)
                {
                    case var refType when IsReferenceType(refType):
                    {
                        writer.Write(HashStruct(types, memberValue.TypeName, (IEnumerable<MemberValue>) memberValue.Value));
                        break;
                    }
                    case "string":
                    {
                        var value = Encoding.UTF8.GetBytes((string) memberValue.Value);
                        var abiValueEncoded = Sha3Keccack.Current.CalculateHash(value);
                        writer.Write(abiValueEncoded);
                        break;
                    }
                    case "bytes":
                    {
                        var value = (byte[]) memberValue.Value;
                        var abiValueEncoded = Sha3Keccack.Current.CalculateHash(value);
                        writer.Write(abiValueEncoded);
                        break;
                    }
                    default:
                    {
                        var abiValue = new ABIValue(memberValue.TypeName, memberValue.Value);
                        var abiValueEncoded = _abiEncode.GetABIEncoded(abiValue);
                        writer.Write(abiValueEncoded);
                        break;
                    }
                }
            }
        }

        private static IEnumerable<Member> ParseDomain(Domain domain)
        {
            var result = new List<Member>();

            if (domain.Name != null)
            {
                result.Add(new Member( new MemberDescription {Type = "string", Name = "name"},
                    new MemberValue {TypeName = "string", Value = domain.Name}
                ));
            }

            if (domain.Version != null)
            {
                result.Add(new Member(
                    new MemberDescription {Type = "string", Name = "version"},
                    new MemberValue {TypeName = "string", Value = domain.Version}
                ));
            }

            if (domain.ChainId.HasValue)
            {
                result.Add(new Member(
                    new MemberDescription {Type = "uint256", Name = "chainId"},
                    new MemberValue {TypeName = "uint256", Value = domain.ChainId.Value}
                ));
            }

            if (domain.VerifyingContract != null)
            {
                result.Add(new Member(
                    new MemberDescription {Type = "address", Name = "verifyingContract"},
                    new MemberValue {TypeName = "address", Value = domain.VerifyingContract}
                ));
            }

            if (domain.Salt != null)
            {
                result.Add(new Member(
                    new MemberDescription {Type = "bytes32", Name = "salt"},
                    new MemberValue {TypeName = "bytes32", Value = domain.Salt}
                ));
            }

            return result;
        }

        private TypedData GenerateTypedData<T>(T data, Domain domain, string primaryTypeName)
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

            var result = new TypedData
            {
                PrimaryType = primaryTypeName,
                Types = new Dictionary<string, MemberDescription[]>
                {
                    [primaryTypeName] = typeMembers.ToArray(),
                    ["EIP712Domain"] = ParseDomain(domain).Select(x => x.MemberDescription).ToArray()
                },
                Message = typeValues.ToArray(),
                Domain = domain
            };

            return result;
        }
    }
}