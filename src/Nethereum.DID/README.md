# Nethereum.DID

W3C Decentralized Identifiers (DID) Core specification models, DID URL parser, and document serialization for .NET.

## Features

- **DID Document model** - Full W3C DID Core v1.0 specification support
- **DID URL parser** - Parse and validate DID URLs with method, path, query, fragment, and params
- **Dual JSON serialization** - Both Newtonsoft.Json and System.Text.Json (NET6.0+)
- **Verification methods** - Support for EcdsaSecp256k1, Ed25519, JsonWebKey2020, and more
- **Verification relationships** - Authentication, assertion, key agreement, capability invocation/delegation
- **Service endpoints** - Flexible service endpoint representation
- **Backwards compatible** - Targets net451, net461, netstandard2.0, net6.0, net8.0, net9.0, net10.0

## Usage

```csharp
using Nethereum.DID;

// Create a DID document
var doc = DidDocument.CreateDefault("did:ethr:0x1234...");

// Parse a DID URL
var didUrl = DidUrlParser.Parse("did:ethr:1:0xabc...#controller");

// Serialize/deserialize with Newtonsoft.Json
var json = JsonConvert.SerializeObject(doc);
var parsed = JsonConvert.DeserializeObject<DidDocument>(json);
```
