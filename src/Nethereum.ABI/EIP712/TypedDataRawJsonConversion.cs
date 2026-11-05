using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Nethereum.ABI.ABIDeserialisation;
using System.Linq;
using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.EIP712
{
    public static class TypedDataRawJsonConversion
    {

        public static string ToJson(this TypedDataRaw typedDataRaw)
        {
            return SerialiseRawTypedDataToJson(typedDataRaw);
        }

        public static string ToJson<TDomain>(this TypedData<TDomain> typedData)
        {
            return SerialiseRawTypedDataToJson(typedData.ToRaw(typedData.Message));
        }

        public static string ToJson<TMessage, TDomain>(this TypedData<TDomain> typedData, TMessage message)
        {
            return SerialiseTypedDataToJson(typedData, message);
        }        

        public static TypedDataRaw DeserialiseJsonToRawTypedData<DomainType>(string json, string messageKeySelector = "message")
        {
            var convertor = new ExpandoObjectConverter();
            var jsonDeserialised = JsonConvert.DeserializeObject<IDictionary<string, object>>(json, convertor);
            var types = jsonDeserialised["types"] as IDictionary<string, object>;
            var typeMemberDescriptions = GetMemberDescriptions(types);
            if (!typeMemberDescriptions.ContainsKey("EIP712Domain"))
            {
                var domainMemberDescription = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainType)).FirstOrDefault();
                typeMemberDescriptions.Add(domainMemberDescription.Key, domainMemberDescription.Value);
            }

            var domainValues = GetMemberValues((IDictionary<string, object>)jsonDeserialised["domain"], "EIP712Domain", typeMemberDescriptions);
            var primaryType = string.Empty;

            if (jsonDeserialised.ContainsKey("primaryType"))
            {
                primaryType = jsonDeserialised["primaryType"].ToString();
            }
            else
            {
                if (types.Count == 1)
                {
                    primaryType = types.First().Key;
                }
                else
                {
                    throw new Exception("Primary type not set");
                }
            }

            var message = jsonDeserialised[messageKeySelector];
            var messageValues = GetMemberValues((IDictionary<string, object>)message, primaryType, typeMemberDescriptions);

            var rawTypedData = new TypedDataRaw()
            {
                DomainRawValues = domainValues.ToArray(),
                PrimaryType = primaryType,
                Message = messageValues.ToArray(),
                Types = typeMemberDescriptions
            };

            return rawTypedData;
        }


        public static TypedDataRaw DeserialiseJsonToRawTypedData(string json)
        {
            return DeserialiseJsonToRawTypedData<Domain>(json);
        }

        public static string SerialiseTypedDataToJson<TMessage, TDomain>(TypedData<TDomain> typedData, TMessage message)
        {
            return SerialiseRawTypedDataToJson(typedData.ToRaw(MemberValueFactory.CreateFromMessage(message)));
        }

        public static string SerialiseRawTypedDataToJson(TypedDataRaw typedDataRaw)
        {
            var jobject = (JObject)JToken.FromObject(typedDataRaw);
            var domainProperty = new JProperty("domain");
            var domainProperties = GetJProperties("EIP712Domain", typedDataRaw.DomainRawValues, typedDataRaw);
            domainProperty.Value = new JObject(domainProperties.ToArray());
            jobject.Add(domainProperty);
            var messageProperty = new JProperty("message");
            var messageProperties = GetJProperties(typedDataRaw.PrimaryType, typedDataRaw.Message, typedDataRaw);
            messageProperty.Value = new JObject(messageProperties.ToArray());
            jobject.Add(messageProperty);
            return jobject.ToString();
        }
        private static MemberValue GetMemberValue(string memberType, object memberValue, Dictionary<string, MemberDescription[]> typeMemberDescriptions)
        {
            return new MemberValue()
            {
                TypeName = memberType,
                Value = GetValue(memberType, memberValue, typeMemberDescriptions)
            };
        }

        private static object GetValue(string type, object value, Dictionary<string, MemberDescription[]> typeMemberDescriptions)
        {
            if (type.EndsWith("]", StringComparison.Ordinal))
            {
                var items = value as IList;
                if (items == null || value is string)
                {
                    throw new ArgumentException("The provided data does not match type '" + type + "'.");
                }

                var innerType = type.Substring(0, type.LastIndexOf('['));
                if (typeMemberDescriptions.ContainsKey(innerType))
                {
                    var structItems = new List<MemberValue[]>();
                    foreach (var item in items)
                    {
                        structItems.Add((MemberValue[])GetValue(innerType, item, typeMemberDescriptions));
                    }
                    return structItems;
                }

                var convertedItems = new List<object>();
                foreach (var item in items)
                {
                    convertedItems.Add(GetValue(innerType, item, typeMemberDescriptions));
                }
                return convertedItems;
            }

            if (typeMemberDescriptions.ContainsKey(type))
            {
                var structValue = value as IDictionary<string, object>;
                if (structValue == null)
                {
                    throw new ArgumentException("The provided data does not match type '" + type + "'.");
                }
                return GetMemberValues(structValue, type, typeMemberDescriptions).ToArray();
            }

            return value;
        }

        private static Dictionary<string, MemberDescription[]> GetMemberDescriptions(IDictionary<string, object> types)
        {
            var typeMemberDescriptions = new Dictionary<string, MemberDescription[]>();
            foreach (var type in types)
            {
                var members = type.Value as IList;
                if (members == null)
                {
                    throw new ArgumentException("Type '" + type.Key + "' does not declare a list of members.");
                }

                var memberDescriptions = new List<MemberDescription>();
                foreach (var typeMember in members)
                {
                    var typeMemberDictionary = (IDictionary<string, object>)typeMember;
                    memberDescriptions.Add(
                          new MemberDescription()
                          {
                              Name = (string)typeMemberDictionary["name"],
                              Type = (string)typeMemberDictionary["type"]
                          });
                }
                typeMemberDescriptions.Add(type.Key, memberDescriptions.ToArray());
            }

            return typeMemberDescriptions;
        }

        private static List<MemberValue> GetMemberValues(IDictionary<string, object> deserialisedObject, string typeName, Dictionary<string, MemberDescription[]> typeMemberDescriptions)
        {
            if (deserialisedObject == null)
            {
                throw new ArgumentException("The value of '" + typeName + "' is missing.");
            }

            if (!typeMemberDescriptions.TryGetValue(typeName, out var typeMemberDescription))
            {
                throw new ArgumentException("Type '" + typeName + "' is not defined.");
            }

            var memberValues = new List<MemberValue>();
            foreach (var member in typeMemberDescription)
            {
                if (!deserialisedObject.TryGetValue(member.Name, out var memberValue))
                {
                    throw new ArgumentException("The value of member '" + member.Name + "' of '" + typeName + "' is missing.");
                }

                memberValues.Add(GetMemberValue(member.Type, memberValue, typeMemberDescriptions));
            }

            return memberValues;
        }

        private static List<JProperty> GetJProperties(string mainTypeName, MemberValue[] values, TypedDataRaw typedDataRaw)
        {
            var properties = new List<JProperty>();
            var mainType = typedDataRaw.Types[mainTypeName];
            for (int i = 0; i < mainType.Length; i++)
            {
                var memberType = mainType[i].Type;
                var memberName = mainType[i].Name;
                if (typedDataRaw.Types.ContainsKey(memberType))
                {
                    var memberProperty = new JProperty(memberName);
                    if (values[i].Value != null)
                    {
                        memberProperty.Value = new JObject(GetJProperties(memberType, (MemberValue[])values[i].Value, typedDataRaw).ToArray());
                    }
                    else
                    {
                        memberProperty.Value = null;
                    }
                    properties.Add(memberProperty);
                }
                else
                {
                    if (memberType.StartsWith("bytes"))
                    {
                        var name = memberName;
                        if (values[i].Value is byte[])
                        {
                            var value = ((byte[])values[i].Value).ToHex(true);
                            properties.Add(new JProperty(name, value));
                        }
                        else
                        {
                            var value = values[i].Value;
                            properties.Add(new JProperty(name, value));
                        }
                    }
                    else
                    {
                        if (memberType.Contains("["))
                        {
                            var memberProperty = new JProperty(memberName);
                            var memberValueArray = new JArray();
                            var innerType = memberType.Substring(0, memberType.LastIndexOf("["));
                            if (values[i].Value == null)
                            {
                                memberProperty.Value = null;
                                properties.Add(memberProperty);
                            }
                            else
                            {
                                if (typedDataRaw.Types.ContainsKey(innerType))
                                {
                                    var items = (List<MemberValue[]>)values[i].Value;

                                    foreach (var item in items)
                                    {
                                        memberValueArray.Add(new JObject(GetJProperties(innerType, item, typedDataRaw).ToArray()));
                                    }
                                    memberProperty.Value = memberValueArray;
                                    properties.Add(memberProperty);
                                }
                                else
                                {
                                    var items = (IList)values[i].Value;

                                    foreach (var item in items)
                                    {
                                        memberValueArray.Add(item);
                                    }

                                    memberProperty.Value = memberValueArray;
                                    properties.Add(memberProperty);
                                }
                            }

                        }
                        else
                        {

                            var name = memberName;
                            var value = values[i].Value;
                            properties.Add(new JProperty(name, value));
                        }
                    }
                }
            }
            return properties;
        }

    }
}
