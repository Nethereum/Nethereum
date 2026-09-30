using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.RPC.TxPool.DTOs;
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

            Assert.True(JToken.DeepEquals(currentLocalRpc, repoRpc), DescribeDrift(currentLocalRpc, repoRpc));
        }

        private static string DescribeDrift(JObject local, JObject upstream)
        {
            var localMethods = NamesOfMethods(local);
            var upstreamMethods = NamesOfMethods(upstream);
            var localSchemas = NamesOfSchemas(local);
            var upstreamSchemas = NamesOfSchemas(upstream);

            return
                "The pinned openrpc.json is behind execution-apis." + Environment.NewLine +
                "  methods upstream has and we do not: " + Join(upstreamMethods.Except(localMethods)) + Environment.NewLine +
                "  methods we have and upstream dropped: " + Join(localMethods.Except(upstreamMethods)) + Environment.NewLine +
                "  schemas upstream has and we do not: " + Join(upstreamSchemas.Except(localSchemas)) + Environment.NewLine +
                "  schemas we have and upstream dropped: " + Join(localSchemas.Except(upstreamSchemas)) + Environment.NewLine +
                "Re-pinning the file also requires an ApiMethods or UnsupportedApiMethods entry for every new " +
                "method and a component mapping for every new schema, which the other tests here check.";
        }

        private static IEnumerable<string> NamesOfMethods(JObject spec) =>
            ((JArray)spec["methods"]).Select(m => m["name"].ToString());

        private static IEnumerable<string> NamesOfSchemas(JObject spec) =>
            ((JObject)spec["components"]["schemas"]).Properties().Select(p => p.Name);

        private static string Join(IEnumerable<string> names)
        {
            var ordered = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
            return ordered.Count == 0 ? "none" : string.Join(", ", ordered);
        }


        [Fact]
        public void ShouldHaveAllMethodsListedInApiMethodsWhenNotUnsupported()
        {
            var openRpc = GetOpenRpc();
            JArray methods = (JArray) openRpc["methods"];
            var failed = false;
            foreach (var method in methods)
            {
                var methodName = method["name"].ToString();
                if (!Enum.TryParse(methodName, out UnsupportedApiMethods unsupportedApiMethod))
                {
                    if (!Enum.TryParse(methodName, out ApiMethods apiMethod))
                    {
                        Debug.WriteLine(methodName);
                        failed = true;
                    };
                }
            }

            Assert.False(failed);
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
                    if(!complexValidators.Any()) throw new Exception("Complex Object Not Mapped: " + schemaName);
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

                            if (complexTypeValidation.ValidateOneOf && schemas[schemaName]["oneOf"] is JArray oneOfArray)
                            {
                                foreach (var item in oneOfArray)
                                {
                                    if (item["properties"] is JObject oneOfProperties)
                                    {
                                        ValidateProperties(oneOfProperties, complexTypeValidation, dataMembers);
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
            public bool ValidateOneOf { get; set; }
        }

        private static readonly string[] EngineApiComponents =
        {
            "BlobsBundleV1",
            "ExecutionPayloadBodyV1",
            "PayloadStatusNoInvalidBlockHash",
            "RestrictedPayloadStatusV1",
            "TransitionConfigurationV1",
            "WithdrawalV1",
            "ExecutionPayloadBodyV2",
            "BlobsBundleV2",
            "BlobAndProofV1",
            "BlobAndProofV2",
            "BlobCellsAndProofsV1"
        };

        public List<ComplexTypeValidation> ComplexComponentTypeMappings()
        {
            var list = new List<ComplexTypeValidation>();
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(Block), IgnoredProperties = new []{"transactions"}});
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(BlockWithTransactionHashes)}); // transactions included here as hashes
            list.Add( new ComplexTypeValidation{Name = "Block", Type =typeof(BlockWithTransactions) }); // transactions full object included here
            list.Add(new ComplexTypeValidation { Name = "SyncingStatus", Type = typeof(SyncingOutput), Ignored = true}); // Custom object
            list.Add(new ComplexTypeValidation { Name = "BlockTag", Type = typeof(BlockParameter.BlockParameterType), Ignored = true}); // Custom object these are the enum values
            list.Add(new ComplexTypeValidation { Name = "BlockNumberOrTag", Type = typeof(BlockParameter), Ignored = true}); // Custom object
            list.Add(new ComplexTypeValidation { Name = "BlockNumberOrTagOrHash", Type = typeof(BlockParameter), Ignored = true});
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
            list.Add(new ComplexTypeValidation { Name = "Transaction4844Unsigned", Type = typeof(TransactionInput), IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "Transaction4844Signed", Type = typeof(Transaction), IgnoredProperties = new[] { "yParity" } });
            list.Add(new ComplexTypeValidation { Name = "Withdrawal", Type = typeof(Withdrawal) });

            list.Add(new ComplexTypeValidation { Name = "ExecutionPayloadV1", Type = typeof(ExecutionPayloadV1) });
            list.Add(new ComplexTypeValidation { Name = "ExecutionPayloadV2", Type = typeof(ExecutionPayloadV2) });
            list.Add(new ComplexTypeValidation { Name = "ExecutionPayloadV3", Type = typeof(ExecutionPayloadV3) });
            list.Add(new ComplexTypeValidation { Name = "ExecutionPayloadV4", Type = typeof(ExecutionPayloadV4) });
            list.Add(new ComplexTypeValidation { Name = "ForkchoiceStateV1", Type = typeof(ForkchoiceStateV1) });
            list.Add(new ComplexTypeValidation { Name = "ForkchoiceUpdatedResponseV1", Type = typeof(ForkchoiceUpdatedResponseV1) });
            list.Add(new ComplexTypeValidation { Name = "PayloadAttributesV1", Type = typeof(PayloadAttributesV1) });
            list.Add(new ComplexTypeValidation { Name = "PayloadAttributesV2", Type = typeof(PayloadAttributesV2) });
            list.Add(new ComplexTypeValidation { Name = "PayloadAttributesV3", Type = typeof(PayloadAttributesV3) });
            list.Add(new ComplexTypeValidation { Name = "PayloadAttributesV4", Type = typeof(PayloadAttributesV4) });
            list.Add(new ComplexTypeValidation { Name = "PayloadStatusV1", Type = typeof(PayloadStatusV1) });

            foreach (var engineApiComponent in EngineApiComponents)
                list.Add(new ComplexTypeValidation { Name = engineApiComponent, Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "AccountProof", Type = typeof(AccountProof), Ignored = false });
            list.Add(new ComplexTypeValidation { Name = "StorageProof", Type = typeof(StorageProof), Ignored = false });
            list.Add(new ComplexTypeValidation { Name = "Access list result", Type = typeof(AccessListGasUsed), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "BadBlock", Type = typeof(BadBlock), Ignored = false });
            list.Add(new ComplexTypeValidation { Name = "notFound", Ignored = true });

            list.Add(new ComplexTypeValidation { Name = "ConfigurationResponse", Type = typeof(ChainConfiguration) });
            list.Add(new ComplexTypeValidation { Name = "ConfigObject", Type = typeof(ChainConfigurationEntry) });
            list.Add(new ComplexTypeValidation { Name = "BlobSchedule", Type = typeof(BlobScheduleConfiguration) });

            list.Add(new ComplexTypeValidation { Name = "BlockAccessList", Type = typeof(List<AccountAccess>), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "AccountAccess", Type = typeof(AccountAccess) });
            list.Add(new ComplexTypeValidation { Name = "SlotChanges", Type = typeof(SlotChanges) });
            list.Add(new ComplexTypeValidation { Name = "StorageChange", Type = typeof(StorageChange) });
            list.Add(new ComplexTypeValidation { Name = "BalanceChange", Type = typeof(BalanceChange) });
            list.Add(new ComplexTypeValidation { Name = "NonceChange", Type = typeof(NonceChange) });
            list.Add(new ComplexTypeValidation { Name = "CodeChange", Type = typeof(CodeChange) });
            list.Add(new ComplexTypeValidation { Name = "AccountStorage", Ignored = true });

            list.Add(new ComplexTypeValidation { Name = "Transaction7702Unsigned", Type = typeof(TransactionInput), IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "Transaction7702Signed", Type = typeof(Transaction), IgnoredProperties = new[] { "yParity" } });
            list.Add(new ComplexTypeValidation { Name = "AuthorizationList", Type = typeof(List<Authorisation>), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "Withdrawals", Type = typeof(List<Withdrawal>), Ignored = true });

            list.Add(new ComplexTypeValidation { Name = "EthSimulatePayload", Type = typeof(EthSimulateInput) });
            list.Add(new ComplexTypeValidation { Name = "BlockStateCalls", Type = typeof(BlockStateCall), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "BlockOverrides", Type = typeof(BlockOverrides) });
            list.Add(new ComplexTypeValidation { Name = "StateOverrides", Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "AccountOverride", Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "AccountOverrideState", Type = typeof(AccountOverride) });
            list.Add(new ComplexTypeValidation { Name = "AccountOverrideStateDiff", Type = typeof(AccountOverride) });
            list.Add(new ComplexTypeValidation { Name = "GenericCallTransaction", Type = typeof(TransactionInput), IgnoredProperties = new[] { "input" } });
            list.Add(new ComplexTypeValidation { Name = "EthSimulateResult", Type = typeof(List<EthSimulateBlockResult>), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "EthSimulateBlockResultSingleSuccess", Type = typeof(EthSimulateBlockResult) });
            list.Add(new ComplexTypeValidation { Name = "CallResults", Type = typeof(List<EthSimulateCallResult>), Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "CallResultSuccess", Type = typeof(EthSimulateCallResult) });
            list.Add(new ComplexTypeValidation { Name = "CallResultFailure", Type = typeof(EthSimulateCallResult) });

            list.Add(new ComplexTypeValidation { Name = "EthCapabilities", Type = typeof(EthCapabilitiesResult) });
            list.Add(new ComplexTypeValidation { Name = "EthCapabilitiesHead", Type = typeof(EthCapabilitiesHead) });
            list.Add(new ComplexTypeValidation { Name = "EthCapabilitiesEffectiveResource", Type = typeof(EthCapabilitiesEffectiveResource) });
            list.Add(new ComplexTypeValidation { Name = "EthCapabilitiesDeleteStrategy", Type = typeof(EthCapabilitiesDeleteStrategy), ValidateOneOf = true });
            list.Add(new ComplexTypeValidation { Name = "TxpoolContent", Type = typeof(TxPoolContentResponse) });
            list.Add(new ComplexTypeValidation { Name = "TxpoolContentFromResult", Type = typeof(TxPoolContentFromResponse) });
            list.Add(new ComplexTypeValidation { Name = "TxpoolStatus", Type = typeof(TxPoolStatusResponse) });
            list.Add(new ComplexTypeValidation { Name = "PendingTransactionInfo", Type = typeof(PendingTransactionInfo) });
            list.Add(new ComplexTypeValidation { Name = "TxpoolContentByAddress", Ignored = true });
            list.Add(new ComplexTypeValidation { Name = "TxpoolContentAddressMap", Ignored = true });

            return list;

        }
        

        public Dictionary<string, Type> SimpleComponentTypeMapping()
        {
            var mappings = new Dictionary<string, Type>();
            mappings.Add("address", typeof(string));
            mappings.Add("addresses", typeof(string[]));
            mappings.Add("byte", typeof(string));
            mappings.Add("bytesMax32", typeof(string));
            mappings.Add("bytes", typeof(string)); 
            mappings.Add("bytes48", typeof(string));
            mappings.Add("bytes8", typeof(string));
            mappings.Add("bytes32", typeof(string));
            mappings.Add("bytes256", typeof(string));
            mappings.Add("bytes65", typeof(string));
            mappings.Add("bytes4", typeof(string));
            mappings.Add("bytes16", typeof(string));
            mappings.Add("bytes96", typeof(string));
            mappings.Add("ratio", typeof(HexBigInteger));
            mappings.Add("uint", typeof(HexBigInteger));
            mappings.Add("uint32", typeof(HexBigInteger));
            mappings.Add("uint64", typeof(HexBigInteger));
            mappings.Add("uint256", typeof(HexBigInteger));
            mappings.Add("uintDecimal", typeof(string));
            mappings.Add("hash32", typeof(string));

            return mappings;
        }

    }
}