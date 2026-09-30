# Nethereum.DID.EthrDID

did:ethr method implementation using the ERC-1056 EthereumDIDRegistry contract for Decentralized Identifiers.

## Features

- **EthereumDIDRegistryService** - Typed contract service for ERC-1056 identity management
- **EthrDidResolver** - Resolves did:ethr identifiers to W3C DID Documents from on-chain events
- **Registry addresses** - `EthrDidConstants` provides default registry addresses for Ethereum mainnet, Sepolia and Polygon (`GetDefaultRegistryAddresses()`); `EthrDidResolver` takes one `registryAddress` (default: the mainnet registry).
- **Full ERC-1056 support** - Owner management, delegates, attributes (including signed variants)

## Usage

```csharp
using Nethereum.DID.EthrDID;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var resolver = new EthrDidResolver(web3);

var didDocument = await resolver.ResolveAsync("did:ethr:0x1234...");
```
