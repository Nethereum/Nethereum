using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Nethereum.DID.Tests
{
    public class DidDocumentSystemTextJsonSerializationTests
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        private string LoadTestData(string filename)
        {
            return File.ReadAllText(Path.Combine("TestData", filename));
        }

        [Fact]
        public void ShouldDeserializeBasicDocument()
        {
            var json = LoadTestData("did-document-basic.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);

            Assert.NotNull(doc);
            Assert.Equal("did:example:123456789abcdefghi", doc.Id);
            Assert.Single(doc.Context);
            Assert.Equal(DidConstants.DidContextV1, (string)doc.Context[0]);
        }

        [Fact]
        public void ShouldRoundtripBasicDocument()
        {
            var json = LoadTestData("did-document-basic.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);
            var serialized = JsonSerializer.Serialize(doc, Options);
            var reparsed = JsonSerializer.Deserialize<DidDocument>(serialized, Options);

            Assert.Equal(doc.Id, reparsed.Id);
            Assert.Equal(doc.Context.Count, reparsed.Context.Count);
        }

        [Fact]
        public void ShouldDeserializeFullDocument()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);

            Assert.NotNull(doc);
            Assert.Equal("did:example:123456789abcdefghi", doc.Id);
            Assert.Equal(3, doc.Context.Count);
            Assert.Single(doc.AlsoKnownAs);
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
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);

            var vm = doc.VerificationMethod[0];
            Assert.Equal("did:example:123456789abcdefghi#keys-1", vm.Id);
            Assert.Equal(DidConstants.EcdsaSecp256k1VerificationKey2019, vm.Type);
            Assert.Equal("02b97c30de767f084ce3080168ee293053ba33b235d7116a3263d29f1450936b71", vm.PublicKeyHex);
        }

        [Fact]
        public void ShouldDeserializeMixedVerificationRelationships()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);

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

            var json = JsonSerializer.Serialize(doc, Options);
            using var jsonDoc = JsonDocument.Parse(json);
            var controllerElement = jsonDoc.RootElement.GetProperty("controller");

            Assert.Equal(JsonValueKind.String, controllerElement.ValueKind);
            Assert.Equal("did:example:controller", controllerElement.GetString());
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

            var json = JsonSerializer.Serialize(doc, Options);
            using var jsonDoc = JsonDocument.Parse(json);
            var controllerElement = jsonDoc.RootElement.GetProperty("controller");

            Assert.Equal(JsonValueKind.Array, controllerElement.ValueKind);
            Assert.Equal(2, controllerElement.GetArrayLength());
        }

        [Fact]
        public void ShouldDeserializeEthrDidDocument()
        {
            var json = LoadTestData("did-document-ethr.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);

            Assert.Equal("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8a", doc.Id);
            Assert.Equal(2, doc.VerificationMethod.Count);

            var controller = doc.VerificationMethod[0];
            Assert.Equal(DidConstants.EcdsaSecp256k1RecoveryMethod2020, controller.Type);
            Assert.Equal("eip155:1:0xb9c5714089478a327f09197987f16f9e5d936e8a", controller.BlockchainAccountId);

            Assert.Single(doc.Service);
        }

        [Fact]
        public void ShouldRoundtripFullDocument()
        {
            var json = LoadTestData("did-document-full.json");
            var doc = JsonSerializer.Deserialize<DidDocument>(json, Options);
            var serialized = JsonSerializer.Serialize(doc, Options);
            var doc2 = JsonSerializer.Deserialize<DidDocument>(serialized, Options);

            Assert.Equal(doc.Id, doc2.Id);
            Assert.Equal(doc.Context.Count, doc2.Context.Count);
            Assert.Equal(doc.VerificationMethod.Count, doc2.VerificationMethod.Count);
            Assert.Equal(doc.Authentication.Count, doc2.Authentication.Count);
            Assert.Equal(doc.Service.Count, doc2.Service.Count);
        }

        [Fact]
        public void ShouldSerializeVerificationRelationshipReference()
        {
            var rel = new VerificationRelationship("did:example:123#key-1");
            var json = JsonSerializer.Serialize(rel, Options);
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
            var json = JsonSerializer.Serialize(rel, Options);
            Assert.Contains("\"id\":", json);
            Assert.Contains("\"type\":", json);
        }

        [Fact]
        public void CreateDefaultShouldSetContextAndId()
        {
            var doc = DidDocument.CreateDefault("did:example:123");
            var json = JsonSerializer.Serialize(doc, Options);
            var reparsed = JsonSerializer.Deserialize<DidDocument>(json, Options);

            Assert.Equal("did:example:123", reparsed.Id);
            Assert.Single(reparsed.Context);
        }

        [Fact]
        public void ShouldOmitNullCollections()
        {
            var doc = DidDocument.CreateDefault("did:example:123");
            var json = JsonSerializer.Serialize(doc, Options);

            Assert.DoesNotContain("\"verificationMethod\"", json);
            Assert.DoesNotContain("\"authentication\"", json);
            Assert.DoesNotContain("\"service\"", json);
            Assert.DoesNotContain("\"controller\"", json);
        }
    }
}
