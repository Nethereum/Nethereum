using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.UnitTests
{
    public class OpenRpcTests
    {
        public JObject GetOpenRpc()
        {
            return JObject.Parse(File.ReadAllText("openrpc.json"));
        }

        public JObject GetOpenRpcFromWebsite()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString("https://raw.githubusercontent.com/ethereum/execution-apis/assembled-spec/refs-openrpc.json");
                return JObject.Parse(json);
            }
       
        }

        [Fact]
        public void RpcSpecShouldBeTheSame()
        {
            var currentLocalRpc = GetOpenRpc();
            var repoRpc = GetOpenRpcFromWebsite();

            Assert.True(JToken.DeepEquals(currentLocalRpc, repoRpc));
        }


        [Fact]
        public void ShouldHaveAllMethodsListedInApiMethodsWhenNotUnsupported()
        {
            var openRpc = GetOpenRpc();
            JArray methods = (JArray) openRpc["methods"];
            foreach (var method in methods)
            {
                var methodName = method["name"].ToString();
                if (!Enum.TryParse(methodName, out UnsupportedApiMethods unsupportedApiMethod))
                {
                    Assert.True(Enum.TryParse(methodName, out ApiMethods apiMethod));
                }
            }
        }

        [Fact]
        public void ShouldHaveAllComponentsMapped()
        {
            var openRpc = GetOpenRpc();
            var components = (JObject) openRpc["components"];
            var schemas = (JObject) components["schemas"];
            var complexMappings = ComplexComponentTypeMappings();
            var simpleMappings = SimpleComponentTypeMapping();

            foreach (var schema in schemas.Properties())
            {
                var schemaName = schema.Name;
                if (!simpleMappings.ContainsKey(schemaName))
                {
                    var complexValidators = complexMappings.Where(x => x.Name == schemaName);
                    if(!complexValidators.Any()) throw new Exception("Complex Object Not Mapped");
                    foreach (var complexTypeValidation in complexValidators)
                    {
                        if (!complexTypeValidation.Ignored)
                        {
                            var dataMembers = GetPropertiesWithJsonPropertyAttribute(complexTypeValidation.Type);

                            if (schemas[schemaName]["properties"] is JObject properties)
                            {
                                ValidateProperties(properties, complexTypeValidation, dataMembers);
                            }

                            if (schemas[schemaName]["allOf"] is JArray allOfArray)
                            {
                                foreach (var item in allOfArray)
                                {
                                    if (item["properties"] is JObject allOfProperties)
                                    {
                                        ValidateProperties(allOfProperties, complexTypeValidation, dataMembers);
                                    }
                                }
                            }

                        }
                    }
                    
                }

            }
        }

        private static void ValidateProperties(JObject properties, ComplexTypeValidation complexTypeValidation,
            IEnumerable<PropertyInfo> dataMembers)
        {
            foreach (var property in properties.Properties())
            {
                if (complexTypeValidation.IgnoredProperties == null ||
                    !complexTypeValidation.IgnoredProperties.Contains(property.Name))
                {
                    var propertyObject = properties[property.Name].Value<JObject>();
                    var dataMember = dataMembers.FirstOrDefault(x =>
                        x.GetCustomAttribute<JsonPropertyAttribute>().PropertyName == property.Name);

                    if (dataMember == null)
                    {
                        throw new Exception("Property not found: " + property.Name);
                    }
                }
            }
        }


        public static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
#if DOTNET35
            var hidingProperties = type.GetProperties().Where(x => PropertyInfoExtensions.IsHidingMember(x));
            var nonHidingProperties = type.GetProperties().Where(x => hidingProperties.All(y => y.Name != x.Name));
            return nonHidingProperties.Concat(hidingProperties);
#else
            var hidingProperties = type.GetRuntimeProperties().Where(x => IsHidingMember(x));
            var nonHidingProperties =
                type.GetRuntimeProperties().Where(x => hidingProperties.All(y => y.Name != x.Name));
            return nonHidingProperties.Concat(hidingProperties);
#endif
        }

        public static bool IsHidingMember(PropertyInfo self)
        {
            try
            {
                Type baseType = self.DeclaringType.GetTypeInfo().BaseType;
                PropertyInfo baseProperty = baseType.GetRuntimeProperty(self.Name);

                if (baseProperty == null)
                {
                    return false;
                }

                if (baseProperty.DeclaringType == self.DeclaringType)
                {
                    return false;
                }

                var baseMethodDefinition = baseProperty.GetMethod.GetRuntimeBaseDefinition();
                var thisMethodDefinition = self.GetMethod.GetRuntimeBaseDefinition();

                return baseMethodDefinition.DeclaringType != thisMethodDefinition.DeclaringType;
            }
            catch (System.Reflection.AmbiguousMatchException)
            {
                return true;
            }
        }

        public static IEnumerable<PropertyInfo> GetPropertiesWithJsonPropertyAttribute(Type type)
        {
            return GetProperties(type).Where(x => x.IsDefined(typeof(JsonPropertyAttribute), true));
        }


        public class ComplexTypeValidation
        {
            public string Name { get; set; }
            public Type Type { get; set; }
            public string[] IgnoredProperties { get; set; }
            public bool Ignored { get; set; }
        }

        public List<ComplexTypeValidation> ComplexComponentTypeMappings()
        {
            var list = new List<ComplexTypeValidation>();
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(Block), IgnoredProperties = new []{"transactions"}});
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(BlockWithTransactionHashes)}); // transactions included here as hashes
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(BlockWithTransactions) }); // transactions full object included here
            list.Add(new ComplexTypeValidation { Name = "SyncingStatus", Type = typeof(SyncingOutput), Ignored = true}); // Custom object
            list.Add(new ComplexTypeValidation { Name = "BlockTag", Type = typeof(BlockParameter.BlockParameterType), Ignored = true}); // Custom object these are the enum values
            list.Add(new ComplexTypeValidation { Name = "BlockNumberOrTag", Type = typeof(BlockParameter), Ignored = true}); // Custom object
            list.Add(new ComplexTypeValidation { Name = "FilterResults", Ignored = true});
            list.Add(new ComplexTypeValidation { Name = "Filter", Type = typeof(NewFilterInput)});
            list.Add(new ComplexTypeValidation { Name = "FilterTopics", Ignored = true});
            list.Add(new ComplexTypeValidation { Name = "FilterTopic", Ignored = true});
            list.Add(new ComplexTypeValidation { Name = "Log", Type = typeof(FilterLog) });
            list.Add(new ComplexTypeValidation { Name = "ReceiptInfo", Type = typeof(TransactionReceipt)});
            list.Add(new ComplexTypeValidation { Name = "AccessListEntry", Type = typeof(AccessList) });
            list.Add(new ComplexTypeValidation { Name = "AccessList", Type = typeof(List<AccessList>), Ignored = true});
            list.Add(new ComplexTypeValidation { Name = "TransactionWithSender", Type = typeof(TransactionInput), Ignored = true});
            list.Add(new ComplexTypeValidation { Name = "Transaction1559Unsigned", Type = typeof(TransactionInput), IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "Transaction2930Unsigned", Type = typeof(TransactionInput) , IgnoredProperties = new[] { "input" }});
            list.Add(new ComplexTypeValidation { Name = "TransactionLegacyUnsigned", Type = typeof(TransactionInput), IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "TransactionUnsigned", Type = typeof(TransactionInput), Ignored = true, IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "GenericTransaction", Type = typeof(TransactionInput), Ignored = true, IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "Transaction1559Signed", Type = typeof(Transaction), IgnoredProperties = new[] { "yParity" } }); //yParity is v
            list.Add(new ComplexTypeValidation { Name = "Transaction2930Signed", Type = typeof(Transaction), IgnoredProperties = new[] { "yParity" }}); //yParity is v
            list.Add(new ComplexTypeValidation { Name = "TransactionLegacySigned", Type = typeof(Transaction) });
            list.Add(new ComplexTypeValidation { Name = "TransactionInfo", Type = typeof(Transaction) });
            list.Add(new ComplexTypeValidation { Name = "TransactionSigned", Type = typeof(Transaction), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "AccountProof", Type = typeof(AccountProof), Ignored = false });
            list.Add(new ComplexTypeValidation { Name = "StorageProof", Type = typeof(StorageProof), Ignored = false });
            list.Add(new ComplexTypeValidation { Name = "Access list result", Type = typeof(AccessListGasUsed), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "BadBlock", Type = typeof(BadBlock), Ignored = false });

            return list;

        }
        

        public Dictionary<string, Type> SimpleComponentTypeMapping()
        {
            var mappings = new Dictionary<string, Type>();
            mappings.Add("address", typeof(string));
            mappings.Add("addresses", typeof(string[]));
            mappings.Add("byte", typeof(string));
            mappings.Add("bytes", typeof(string));
            mappings.Add("bytes8", typeof(string));
            mappings.Add("bytes32", typeof(string));
            mappings.Add("bytes256", typeof(string));
            mappings.Add("bytes65", typeof(string));
            mappings.Add("uint", typeof(HexBigInteger));
            mappings.Add("uint64", typeof(HexBigInteger));
            mappings.Add("uint256", typeof(HexBigInteger));
            mappings.Add("hash32", typeof(string));

            return mappings;
        }

    }
}