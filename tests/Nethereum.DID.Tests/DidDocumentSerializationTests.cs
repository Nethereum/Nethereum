using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.DID.Tests
{
    public class DidDocumentSerializationTests
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private string LoadTestData(string filename)
        {
            return File.ReadAllText(Path.Combine("TestData", filename));
        }

        [Fact]
        public void ShouldDeserializeBasicDocument()
        {
            var json = LoadTestData("did-document-basic.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            Assert.NotNull(doc);
            Assert.Equal("did:example:123456789abcdefghi", doc.Id);
            Assert.Single(doc.Context);
            Assert.Equal(DidConstants.DidContextV1, doc.Context[0]);
        }

        [Fact]
        public void ShouldRoundtripBasicDocument()
        {
            var json = LoadTestData("did-document-basic.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);
            var serialized = JsonConvert.SerializeObject(doc, Settings);
            var reparsed = JsonConvert.DeserializeObject<DidDocument>(serialized);

            Assert.Equal(doc.Id, reparsed.Id);
            Assert.Equal(doc.Context.Count, reparsed.Context.Count);
        }

        [Fact]
        public void ShouldDeserializeFullDocument()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            Assert.NotNull(doc);
            Assert.Equal("did:example:123456789abcdefghi", doc.Id);
            Assert.Equal(3, doc.Context.Count);
            Assert.Single(doc.AlsoKnownAs);
            Assert.Equal("https://example.com/users/alice", doc.AlsoKnownAs[0]);
            Assert.Equal(2, doc.Controller.Count);
            Assert.Equal(3, doc.VerificationMethod.Count);
            Assert.Equal(2, doc.Authentication.Count);
            Assert.Single(doc.AssertionMethod);
            Assert.Single(doc.KeyAgreement);
            Assert.Single(doc.CapabilityInvocation);
            Assert.Single(doc.CapabilityDelegation);
            Assert.Equal(2, doc.Service.Count);
        }

        [Fact]
        public void ShouldDeserializeVerificationMethodWithHexKey()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            var vm = doc.VerificationMethod[0];
            Assert.Equal("did:example:123456789abcdefghi#keys-1", vm.Id);
            Assert.Equal(DidConstants.EcdsaSecp256k1VerificationKey2019, vm.Type);
            Assert.Equal("did:example:123456789abcdefghi", vm.Controller);
            Assert.Equal("02b97c30de767f084ce3080168ee293053ba33b235d7116a3263d29f1450936b71", vm.PublicKeyHex);
            Assert.Null(vm.PublicKeyMultibase);
            Assert.Null(vm.PublicKeyJwk);
        }

        [Fact]
        public void ShouldDeserializeVerificationMethodWithMultibase()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            var vm = doc.VerificationMethod[1];
            Assert.Equal(DidConstants.Ed25519VerificationKey2020, vm.Type);
            Assert.Equal("zH3C2AVvLMv6gmMNam3uVAjZpfkcJCwDwnZn6z3wXmqPV", vm.PublicKeyMultibase);
        }

        [Fact]
        public void ShouldDeserializeVerificationMethodWithJwk()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            var vm = doc.VerificationMethod[2];
            Assert.Equal(DidConstants.JsonWebKey2020, vm.Type);
            Assert.NotNull(vm.PublicKeyJwk);
        }

        [Fact]
        public void ShouldDeserializeMixedVerificationRelationships()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            Assert.True(doc.Authentication[0].IsReference);
            Assert.Equal("did:example:123456789abcdefghi#keys-1", doc.Authentication[0].VerificationMethodReference);

            Assert.True(doc.Authentication[1].IsEmbedded);
            Assert.Equal("did:example:123456789abcdefghi#keys-auth-1", doc.Authentication[1].EmbeddedVerificationMethod.Id);
        }

        [Fact]
        public void ShouldSerializeSingleControllerAsString()
        {
            var doc = new DidDocument
            {
                Context = new List<object> { DidConstants.DidContextV1 },
                Id = "did:example:123",
                Controller = new List<string> { "did:example:controller" }
            };

            var json = JsonConvert.SerializeObject(doc, Settings);
            var jobj = JObject.Parse(json);

            Assert.Equal(JTokenType.String, jobj["controller"].Type);
            Assert.Equal("did:example:controller", jobj["controller"].Value<string>());
        }

        [Fact]
        public void ShouldSerializeMultipleControllersAsArray()
        {
            var doc = new DidDocument
            {
                Context = new List<object> { DidConstants.DidContextV1 },
                Id = "did:example:123",
                Controller = new List<string> { "did:example:c1", "did:example:c2" }
            };

            var json = JsonConvert.SerializeObject(doc, Settings);
            var jobj = JObject.Parse(json);

            Assert.Equal(JTokenType.Array, jobj["controller"].Type);
            Assert.Equal(2, ((JArray)jobj["controller"]).Count);
        }

        [Fact]
        public void ShouldSerializeSingleContextAsString()
        {
            var doc = DidDocument.CreateDefault("did:example:123");

            var json = JsonConvert.SerializeObject(doc, Settings);
            var jobj = JObject.Parse(json);

            Assert.Equal(JTokenType.String, jobj["@context"].Type);
        }

        [Fact]
        public void ShouldSerializeMultipleContextsAsArray()
        {
            var doc = new DidDocument
            {
                Context = new List<object> { DidConstants.DidContextV1, DidConstants.DidContextSecuritySuitesSecp256k1_2019 },
                Id = "did:example:123"
            };

            var json = JsonConvert.SerializeObject(doc, Settings);
            var jobj = JObject.Parse(json);

            Assert.Equal(JTokenType.Array, jobj["@context"].Type);
        }

        [Fact]
        public void ShouldDeserializeEthrDidDocument()
        {
            var json = LoadTestData("did-document-ethr.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            Assert.Equal("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a", doc.Id);
            Assert.Equal(2, doc.VerificationMethod.Count);

            var controller = doc.VerificationMethod[0];
            Assert.Equal(DidConstants.EcdsaSecp256k1RecoveryMethod2020, controller.Type);
            Assert.Equal("eip155:1:0xb9c5714089478a327f09197987f16f9e5d936e8a", controller.BlockchainAccountId);

            Assert.Single(doc.Service);
            Assert.Equal("https://messenger.example.com", doc.Service[0].ServiceEndpoint);
        }

        [Fact]
        public void ShouldRoundtripFullDocument()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);
            var serialized = JsonConvert.SerializeObject(doc, Settings);
            var doc2 = JsonConvert.DeserializeObject<DidDocument>(serialized);

            Assert.Equal(doc.Id, doc2.Id);
            Assert.Equal(doc.Context.Count, doc2.Context.Count);
            Assert.Equal(doc.VerificationMethod.Count, doc2.VerificationMethod.Count);
            Assert.Equal(doc.Authentication.Count, doc2.Authentication.Count);
            Assert.Equal(doc.Service.Count, doc2.Service.Count);
            Assert.Equal(doc.Controller.Count, doc2.Controller.Count);
        }

        [Fact]
        public void ShouldPreserveUnknownPropertiesViaExtensionData()
        {
            var json = @"{
                ""@context"": ""https://www.w3.org/ns/did/v1"",
                ""id"": ""did:example:123"",
                ""customProperty"": ""customValue"",
                ""customNumber"": 42
            }";

            var doc = JsonConvert.DeserializeObject<DidDocument>(json);
            Assert.NotNull(doc.AdditionalData);
            Assert.True(doc.AdditionalData.ContainsKey("customProperty"));

            var serialized = JsonConvert.SerializeObject(doc, Settings);
            Assert.Contains("customProperty", serialized);
            Assert.Contains("customValue", serialized);
        }

        [Fact]
        public void ShouldHandleServiceEndpointAsObject()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonConvert.DeserializeObject<DidDocument>(json);

            var hubService = doc.Service[1];
            Assert.Equal("IdentityHub", hubService.Type);
            Assert.NotNull(hubService.ServiceEndpoint);
        }

        [Fact]
        public void ShouldSerializeVerificationRelationshipReference()
        {
            var rel = new VerificationRelationship("did:example:123#key-1");
            var json = JsonConvert.SerializeObject(rel);
            Assert.Equal("\"did:example:123#key-1\"", json);
        }

        [Fact]
        public void ShouldSerializeVerificationRelationshipEmbedded()
        {
            var vm = new VerificationMethod
            {
                Id = "did:example:123#key-1",
                Type = DidConstants.EcdsaSecp256k1VerificationKey2019,
                Controller = "did:example:123",
                PublicKeyHex = "abcd"
            };
            var rel = new VerificationRelationship(vm);
            var json = JsonConvert.SerializeObject(rel, Settings);
            Assert.Contains("\"id\":", json);
            Assert.Contains("\"type\":", json);
        }

        [Fact]
        public void CreateDefaultShouldSetContextAndId()
        {
            var doc = DidDocument.CreateDefault("did:example:123");
            Assert.Equal("did:example:123", doc.Id);
            Assert.Single(doc.Context);
            Assert.Equal(DidConstants.DidContextV1, doc.Context[0]);
        }

        [Fact]
        public void ShouldOmitNullCollections()
        {
            var doc = DidDocument.CreateDefault("did:example:123");
            var json = JsonConvert.SerializeObject(doc, Settings);
            var jobj = JObject.Parse(json);

            Assert.Null(jobj["verificationMethod"]);
            Assert.Null(jobj["authentication"]);
            Assert.Null(jobj["service"]);
            Assert.Null(jobj["controller"]);
        }
    }
}
