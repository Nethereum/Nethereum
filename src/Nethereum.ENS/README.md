# Nethereum.ENS

**Typed contract services for the Ethereum Name Service contracts (registry, resolvers, registrars, controller), the `.eth` registration helper `EthTLSService`, and the `TextDataKey` record keys**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.ENS.svg)](https://www.nuget.org/packages/Nethereum.ENS/)

## Overview

`Nethereum.ENS` is a set of generated Nethereum contract services, one per ENS contract, plus one hand-written helper for registering `.eth` names. Use it when you need to talk to the ENS contracts directly: deploy your own registry, call a specific resolver function, or run the commit-and-register flow on the `.eth` registrar controller.

To simply resolve names, reverse-resolve addresses and read or write text records, you do not need this package. The package description says it holds "old ENS interfaces not included in Nethereum.Contracts.Standards.ENS, if in doubt use Nethereum.Contracts". The recommended entry point is `ENSService` in `Nethereum.Contracts.Standards.ENS`, obtained from `web3.Eth.GetEnsService()` (`IEthApiContractService.GetEnsService(string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS, IEnsCCIPService ensCCIPService = null)`). `ENSService` offers `ResolveAddressAsync`, `ResolveTextAsync`, `ReverseResolveAsync`, `GetContentHashAsync`, `SetTextRequestAsync`, `SetAddressRequestAsync` and more.

## Installation

```bash
dotnet add package Nethereum.ENS
```

Depends on `Nethereum.Web3`. Target frameworks come from the build's `ENSFrameworks` property (net451, net461, netstandard2.0, net6.0, net8.0, net9.0, net10.0 in the default build).

## Contract services

Each service is a `partial class` in the `Nethereum.ENS` namespace with its message, DTO and deployment types in `Nethereum.ENS.<ContractName>.ContractDefinition`.

| Service | Contract definition namespace |
|---|---|
| `ENSRegistryService` | `Nethereum.ENS.ENSRegistry.ContractDefinition` |
| `ENSRegistryWithFallbackService` | `Nethereum.ENS.ENSRegistryWithFallback.ContractDefinition` |
| `PublicResolverService` | `Nethereum.ENS.PublicResolver.ContractDefinition` |
| `DefaultReverseResolverService` | `Nethereum.ENS.DefaultReverseResolver.ContractDefinition` |
| `ReverseRegistrarService` | `Nethereum.ENS.ReverseRegistrar.ContractDefinition` |
| `DNSResolverService`, `DNSRegisterService` | `Nethereum.ENS.DNSResolver...`, `Nethereum.ENS.DNSRegister...` |
| `ETHRegistrarControllerService` | `Nethereum.ENS.ETHRegistrarController.ContractDefinition` |
| `BaseRegistrarImplementationService` | `Nethereum.ENS.BaseRegistrarImplementation.ContractDefinition` |
| `FIFSRegistrarService`, `RegistrarService`, `TestRegistrarService`, `EthRegistrarSubdomainRegistrarService` | one `ContractDefinition` namespace per service |
| `RootService`, `ShortNameClaimsService`, `StablePriceOracleService`, `DeedService` | one `ContractDefinition` namespace per service |

Every service has the same shape, shown here for `ENSRegistryService`:

```csharp
public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(
    Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment,
    CancellationTokenSource cancellationTokenSource = null)
public static Task<string> DeployContractAsync(
    Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment)
public static async Task<ENSRegistryService> DeployContractAndGetServiceAsync(
    Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment,
    CancellationTokenSource cancellationTokenSource = null)

public ContractHandler ContractHandler { get; }
public ENSRegistryService(Nethereum.Web3.Web3 web3, string contractAddress)

public Task<string> ResolverQueryAsync(byte[] node, BlockParameter blockParameter = null)
public Task<string> OwnerQueryAsync(byte[] node, BlockParameter blockParameter = null)
public Task<string> SetSubnodeOwnerRequestAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction)
public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(
    SetSubnodeOwnerFunction setSubnodeOwnerFunction, CancellationTokenSource cancellationToken = null)
```

Each contract function appears as `<Name>QueryAsync` (reads) or `<Name>RequestAsync` / `<Name>RequestAndWaitForReceiptAsync` (transactions), each with an overload taking the function message and one taking the parameters directly. The static `Deploy...` methods are on services whose contract has a deployment message (here `ENSRegistryDeployment`). `PublicResolverService` carries the resolver records, for example:

```csharp
public Task<string> AddrQueryAsync(byte[] node, BlockParameter blockParameter = null)
public Task<TransactionReceipt> SetAddrRequestAndWaitForReceiptAsync(
    byte[] node, string a, CancellationTokenSource cancellationToken = null)
public Task<string> TextQueryAsync(byte[] node, string key, BlockParameter blockParameter = null)
public Task<TransactionReceipt> SetTextRequestAndWaitForReceiptAsync(
    byte[] node, string key, string value, CancellationTokenSource cancellationToken = null)
public Task<byte[]> ContenthashQueryAsync(byte[] node, BlockParameter blockParameter = null)
public Task<string> InterfaceImplementerQueryAsync(byte[] node, byte[] interfaceID, BlockParameter blockParameter = null)
```

The hash of a name is computed with `EnsUtil` from `Nethereum.Contracts.Standards.ENS`.

## Deploy ENS, register a name and resolve it

This is the full flow on an in-process `DevChainNode`: deploy the registry, a first-in-first-served registrar owning `eth` and a public resolver, hand `eth` to the registrar, register `myname.eth`, set its resolver and address, then resolve it as an end user would.

```csharp
using var node = await DevChainNode.CreateAndStartAsync(new EthECKey(PrivateKey));
var web3 = (Web3.Web3)node.CreateWeb3(PrivateKey);

var ensRegistryService = await ENSRegistryService.DeployContractAndGetServiceAsync(web3, new ENSRegistryDeployment());

var ensUtil = new EnsUtil();
var ethNode = ensUtil.GetNameHash("eth");

var fifsRegistrarService = await FIFSRegistrarService.DeployContractAndGetServiceAsync(web3,
    new FIFSRegistrarDeployment
    {
        EnsAddr = ensRegistryService.ContractHandler.ContractAddress,
        Node = ethNode.HexToByteArray()
    });

var publicResolverService = await PublicResolverService.DeployContractAndGetServiceAsync(web3,
    new PublicResolverDeployment { Ens = ensRegistryService.ContractHandler.ContractAddress });

await ensRegistryService.SetSubnodeOwnerRequestAndWaitForReceiptAsync(
    ensUtil.GetNameHash("").HexToByteArray(),
    ensUtil.GetLabelHash("eth").HexToByteArray(),
    fifsRegistrarService.ContractHandler.ContractAddress);

await fifsRegistrarService.RegisterRequestAndWaitForReceiptAsync(new RegisterFunction
{
    Owner = AddressToResolve,
    Subnode = ensUtil.GetLabelHash("myname").HexToByteArray()
});

var fullNameNode = ensUtil.GetNameHash("myname.eth").HexToByteArray();

await ensRegistryService.SetResolverRequestAndWaitForReceiptAsync(new SetResolverFunction
{
    Resolver = publicResolverService.ContractHandler.ContractAddress,
    Node = fullNameNode
});

await publicResolverService.SetAddrRequestAndWaitForReceiptAsync(fullNameNode, AddressToResolve);

var resolverAddress = await ensRegistryService.ResolverQueryAsync(fullNameNode);
var resolvedAddress = await new PublicResolverService(web3, resolverAddress).AddrQueryAsync(fullNameNode);
var owner = await ensRegistryService.OwnerQueryAsync(fullNameNode);
```

Backed by `ENSDevChainTests.ShouldDeployEnsRegisterAndResolveANameOnAnInProcessChain` (`DocSection.SmartContracts`, `ens-contract-services`) in `tests/Nethereum.ENS.IntegrationTests/ENS/ENSDevChainTests.cs`. The example uses `Nethereum.DevChain` for `DevChainNode` and `CreateWeb3`; the ENS services themselves take a `Nethereum.Web3.Web3` and work against any node.

## Name hashes

`EnsUtil` (in `Nethereum.Contracts.Standards.ENS`) produces the `node` and label values these services take:

```csharp
var ensUtil = new EnsUtil();

var empty = ensUtil.GetNameHash("");
var eth = ensUtil.GetNameHash("eth");
var fooEth = ensUtil.GetNameHash("foo.eth");
```

`GetNameHash("")` is `0x00...00`, `GetNameHash("eth")` is `0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae` and `GetNameHash("foo.eth")` is `0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f`. Backed by `TextDataKeyTests.ShouldCalculateTheNameHashAndLabelHash` (`ens-contract-services`, Order 2) in `tests/Nethereum.ENS.UnitTests`.

## Text record keys

```csharp
public enum TextDataKey
{
    email, url, avatar, description, notice, keywords,
    vnd_twitter, vnd_github, com_twitter, com_github, com_discord, com_reddit,
    org_telegram, eth_ens_delegate, display, mail, location, phone,
    com_peepeth, com_linkedin, io_keybase
}

public static class TextDataKeyExtensions
{
    public static string GetDataKeyAsString(this TextDataKey textDataKey)
}
```

`GetDataKeyAsString` replaces `_` with `.`, giving the ENSIP-5 key string to pass as the `key` of `TextQueryAsync` / `SetTextRequestAsync`. Note that `Nethereum.Contracts.Standards.ENS` has its own `TextDataKey` enum, which is what `ENSService.ResolveTextAsync` takes.

```csharp
var url = Nethereum.ENS.TextDataKey.url.GetDataKeyAsString();
var twitter = Nethereum.ENS.TextDataKey.com_twitter.GetDataKeyAsString();
var delegateKey = Nethereum.ENS.TextDataKey.eth_ens_delegate.GetDataKeyAsString();
```

The results are `"url"`, `"com.twitter"` and `"eth.ens.delegate"`. Backed by `TextDataKeyTests.ShouldConvertUnderscoresToDotsInTheTextRecordKey` (`ens-contract-services`).

## ABI content types

```csharp
public enum AbiTypeContentType
{
    JSON = 1,
    Zlib_JSON = 2,
    CBOR = 4,
    Url = 8
}
```

`Nethereum.ENS.AbiTypeContentType` is the set of content-type flags used with the resolver's `ABI` record (`PublicResolverService.ABIQueryAsync(byte[] node, BigInteger contentTypes, ...)` takes the bit mask as a `BigInteger`). `Nethereum.Contracts.Standards.ENS` has its own `AbiTypeContentType` enum, which is the one `ENSService.ResolveABIAsync` takes.

## Registering a `.eth` name: `EthTLSService`

`EthTLSService` wraps the `.eth` registrar controller's commit-reveal registration. Its constructor takes a `Nethereum.Web3.Web3` and the registry address (default `0x00000000000C2E074eC69A0dFb2997BA6C7d2e1e`); `InitialiseAsync()` looks up the `eth` owner, resolver and the controller registered as interface `0x018fac06`, and reads the minimum registration duration. These declarations are not backed by a runnable example: they need an ENS deployment with a `.eth` controller (mainnet), and the mainnet tests in the repository require a live endpoint.

```csharp
public class EthTLSService
{
    public EthTLSService(Web3.Web3 web3, string ensRegistryAddress = "0x00000000000C2E074eC69A0dFb2997BA6C7d2e1e")

    public Web3.Web3 Web3 { get; }
    public string EnsRegistryAddress { get; }
    public string TLS { get; private set; }
    public byte[] TLSNameHash { get; private set; }
    public string TLSRegisterAddress { get; private set; }
    public string TLSResolverAddress { get; private set; }
    public string TLSControllerAddress { get; private set; }
    public ENSRegistryService ENSRegistryService { get; private set; }
    public PublicResolverService TLSResolverService { get; private set; }
    public ETHRegistrarControllerService TLSRegistrarControllerService { get; private set; }
    public int MinimunDurationRegistrationInSeconds { get; private set; }

    public async Task InitialiseAsync()
    public int GetMinimumDurationInDays()
    public async Task<BigInteger> CalculateRentPriceAsync(string name, int durationInDays)
    public async Task<decimal> CalculateRentPriceInEtherAsync(string name, int durationInDays)
    public int ConvertDurationToSeconds(int durationInDays)
    public int ConvertDurationToDays(int durationInSeconds)
    public Task<bool> IsNameAvailableAsync(string name)
    public byte[] ConvertSecretToHash(string secret)
    public Task<byte[]> CalculateCommitmentAsync(string name, string owner, string secret)

    public async Task<string> CommitRequestAsync(string name, string owner, string secret)
    public async Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(string name, string owner, string secret)
    public Task<string> CommitRequestAsync(byte[] commitment)
    public Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(byte[] commitment)

    public Task<string> RegisterRequestAsync(string name, string owner, int durationInDays, string secret, decimal etherRentPrice)
    public Task<string> RegisterRequestAsync(string name, string owner, int durationInDays, string secret, BigInteger weiRentPrice)
    public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(string name, string owner, int durationInDays, string secret, decimal etherRentPrice)
    public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(string name, string owner, int durationInDays, string secret, BigInteger weiRentPrice)

    public Task<string> RenewRequestAsync(string name, int durationInDays, decimal etherRentPrice)
    public Task<string> RenewRequestAsync(string name, int durationInDays, BigInteger weiRentPrice)
}
```

Behaviour from the source: durations are given in days and converted to seconds; a duration below the controller's minimum throws `Exception("Duration has to be bigger than the minimum duration: ... days")`. The secret you pass is hashed with Keccak-256 (`ConvertSecretToHash`) before it is used in the commitment and in `register`. The flow is: `InitialiseAsync()`, `IsNameAvailableAsync`, `CalculateRentPriceInEtherAsync`, `CommitRequestAndWaitForReceiptAsync(name, owner, secret)`, then `RegisterRequestAsync(name, owner, durationInDays, secret, price)`.

## Other files in the package

`abi/` holds the `.abi` (and, for the registry, public resolver, default reverse resolver and controller, `.bin`) artifacts the services were generated from, and `oldcontracts/` the original Solidity sources (`ENS.sol`, `ENSRegistry.sol`, `FIFSRegistrar.sol`, `PublicResolver.sol`, `ReverseRegistrar.sol`, `Deed.sol`, `HashRegistrarSimplified.sol`, `DNSResolver.sol`, `DefaultReverseResolver.sol`, `ResolverInterface.sol`).
