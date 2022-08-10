using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Nethereum.ABI.ABIDeserialisation;

namespace Nethereum.Signer.EIP712
{
    public static class TypedDataRawJsonConversion
    {

        public static string ToJson(this TypedDataRaw typedDataRaw)
        {
            return SerialiseRawTypedDataToJson(typedDataRaw);
        }

        public static string ToJson<TDomain>(this TypedData<TDomain> typedData)
        {
            typedData.EnsureDomainRawValuesAreInitialised();
            return SerialiseRawTypedDataToJson(typedData);
        }

        public static string ToJson<TMessage, TDomain>(this TypedData<TDomain> typedData, TMessage message)
        {
            return SerialiseTypedDataToJson(typedData, message);
        }

        public static TypedDataRaw DeserialiseJsonToRawTypedData(string json)
        {
            var convertor = new ExpandoObjectConverter();
            var jsonDeserialised = JsonConvert.DeserializeObject<IDictionary<string, object>>(json, convertor);
            var types = jsonDeserialised["types"] as IDictionary<string, object>;
            var typeMemberDescriptions = GetMemberDescriptions(types);

            var domainValues = GetMemberValues((IDictionary<string, object>)jsonDeserialised["domain"], "EIP712Domain", typeMemberDescriptions);
            var primaryType = (string)jsonDeserialised["primaryType"];
            var message = jsonDeserialised["message"];
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

        public static string SerialiseTypedDataToJson<TMessage, TDomain>(TypedData<TDomain> typedData, TMessage message)
        {
            typedData.EnsureDomainRawValuesAreInitialised();
            typedData.Message = MemberValueFactory.CreateFromMessage(message);
            return SerialiseRawTypedDataToJson(typedData);
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

            if (Eip712TypedDataSigner.IsReferenceType(memberType))
            {
                return new MemberValue()
                {
                    TypeName = memberType,
                    Value = GetMemberValues((IDictionary<string, object>)memberValue, memberType, typeMemberDescriptions).ToArray()
                };
            }
            else
            {
                if (memberType.Contains("["))
                {
                    var items = (IList)memberValue;
                    var innerType = memberType.Substring(0, memberType.LastIndexOf("["));
                    if (Eip712TypedDataSigner.IsReferenceType(innerType))
                    {
                        var itemsMemberValues = new List<MemberValue[]>();
                        foreach (var item in items)
                        {
                            itemsMemberValues.Add(GetMemberValues((IDictionary<string, object>)item, innerType, typeMemberDescriptions).ToArray());
                        }

                        return new MemberValue() { TypeName = memberType, Value = itemsMemberValues };
                    }
                    else
                    {
                        var itemsMemberValues = new List<object>();

                        foreach (var item in items)
                        {
                            itemsMemberValues.Add(item);
                        }

                        return new MemberValue() { TypeName = memberType, Value = itemsMemberValues };
                    }

                }
                else
                {
                    return new MemberValue()
                    {
                        TypeName = memberType,
                        Value = memberValue
                    };
                }
            }
        }

        private static Dictionary<string, MemberDescription[]> GetMemberDescriptions(IDictionary<string, object> types)
        {
            var typeMemberDescriptions = new Dictionary<string, MemberDescription[]>();
            foreach (var type in types)
            {
                var memberDescriptions = new List<MemberDescription>();
                foreach (var typeMember in type.Value as List<object>)
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
            var memberValues = new List<MemberValue>();
            var typeMemberDescription = typeMemberDescriptions[typeName];
            foreach (var member in typeMemberDescription)
            {

                var memberType = member.Type;
                var memberValue = deserialisedObject[member.Name];

                memberValues.Add(GetMemberValue(memberType, memberValue, typeMemberDescriptions));
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
                if (Eip712TypedDataSigner.IsReferenceType(memberType))
                {
                    var memberProperty = new JProperty(memberName);
                    memberProperty.Value = new JObject(GetJProperties(memberType, (MemberValue[])values[i].Value, typedDataRaw).ToArray());
                    properties.Add(memberProperty);
                }
                else
                {
                    if (memberType.Contains("["))
                    {
                        var memberProperty = new JProperty(memberName);
                        var memberValueArray = new JArray();
                        var innerType = memberType.Substring(0, memberType.LastIndexOf("["));
                        if (Eip712TypedDataSigner.IsReferenceType(innerType))
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
                    else
                    {
                        var name = memberName;
                        var value = values[i].Value;
                        properties.Add(new JProperty(name, value));
                    }
                }
            }
            return properties;
        }

    }
}