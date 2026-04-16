# Nethereum.DID.EthrDID

did:ethr method implementation using the ERC-1056 EthereumDIDRegistry contract for Decentralized Identifiers.

## Features

- **EthereumDIDRegistryService** - Typed contract service for ERC-1056 identity management
- **EthrDidResolver** - Resolves did:ethr identifiers to W3C DID Documents from on-chain events
- **Multi-chain support** - Configurable registry addresses per chain
- **Full ERC-1056 support** - Owner management, delegates, attributes (including signed variants)

## Usage

```csharp
using Nethereum.DID.EthrDID;
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var resolver = new EthrDidResolver(web3);

var didDocument = await resolver.ResolveAsync("did:ethr:0x1234...");
```
