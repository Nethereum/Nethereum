# Nethereum.AccountAbstraction.Bundler.InProcess

In-process DevChain + ERC-4337 bundler bootstrap, for tests and examples.

## Overview

`InProcessBundlerHost` spins up an in-process `Nethereum.DevChain` node together with an in-process
`Nethereum.AccountAbstraction.Bundler.BundlerService`, with ERC-4337 validation switched on by
default. It exists so integration tests and example apps can exercise the full bundler pipeline -
mempool, validation, gas estimation, bundle execution - without a wire round-trip to a separate
process, and without needing an external `anvil`/`geth` dev node.

This package is test/example infrastructure and is deliberately kept out of
`Nethereum.AccountAbstraction.Bundler` (the shippable core bundler library) and its production-facing
packages (`Nethereum.AccountAbstraction.Bundler.RpcServer` for the standalone JSON-RPC host, `.RocksDB`
for persistent mempool/reputation storage), none of which need an in-process DevChain.

### Key Features

- **`InProcessBundlerHost`**: two-phase startup - `StartAsync` brings up the DevChain and an operator
  `Web3` for deploying the AA contract stack, then `StartBundler` wires the bundler once the
  EntryPoint address is known
- **Validation ON by default**: unlike fixtures that disable validation for speed, the bootstrap
  defaults to `UnsafeMode=false`, `SimulateValidation=true`, `StrictValidation=true`
- **Owns lifetime**: disposing the bootstrap disposes the DevChain node and its temp storage

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler.InProcess
```

### Dependencies

- **Nethereum.AccountAbstraction.Bundler** - the bundler service this package wires up in-process
- **Nethereum.DevChain** - the in-process devchain node this package bootstraps

## Quick Start

```csharp
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;

var chainId = new BigInteger(31337);
var operatorAccount = new Account(EthECKey.GenerateKey(), chainId);
var bundlerAccount = new Account(EthECKey.GenerateKey(), chainId);

// Phase 1: bring up the in-process DevChain and fund the operator/bundler accounts
await using var host = await InProcessBundlerHost.StartAsync(
    operatorAccount,
    chainId,
    new[] { operatorAccount.Address, bundlerAccount.Address },
    Nethereum.Web3.Web3.Convert.ToWei(10000));

// Deploy the EntryPoint through the operator Web3
var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
    host.OperatorWeb3, new EntryPointDeployment());

// Phase 2: wire the in-process bundler now that the EntryPoint address is known
var bundler = host.StartBundler(entryPointService.ContractAddress, bundlerAccount);

// bundler is an IAccountAbstractionBundlerService - send/query UserOperations against it
// exactly as you would against a hosted Nethereum.AccountAbstraction.Bundler.RpcServer
var supportedEntryPoints = await bundler.SupportedEntryPoints.SendRequestAsync();

// host.DisposeAsync() (or the `await using` above) disposes the DevChain node and its temp storage
```

## API Reference

```csharp
public class InProcessBundlerHost : IDisposable, IAsyncDisposable
{
    public DevChainNode Node { get; }
    public IWeb3 OperatorWeb3 { get; }
    public BigInteger ChainId { get; }
    public BundlerService BundlerService { get; }
    public IAccountAbstractionBundlerService Bundler { get; }

    public static Task<InProcessBundlerHost> StartAsync(
        Web3Account operatorAccount,
        BigInteger chainId,
        IEnumerable<string> prefundedAddresses,
        BigInteger prefundBalanceWei,
        DevChainConfig config = null);

    public IAccountAbstractionBundlerService StartBundler(
        string entryPointAddress,
        Web3Account bundlerAccount,
        bool enableErc7562Validation = false,
        Action<BundlerConfig> configureOverrides = null);

    public static IAccountAbstractionBundlerService UseHostedUrl(string bundlerRpcUrl, BigInteger chainId);
}
```

- **`Node`** is the underlying `DevChainNode` the host started - use it for anything beyond what
  `OperatorWeb3` exposes (mining control, additional `Web3` instances for other accounts, direct node
  shutdown independent of `Dispose`).
- **`StartBundler`**'s two extra optional parameters go beyond the Quick Start's two required ones:
  `enableErc7562Validation` turns on ERC-7562 full-validation-rule enforcement (off by default, even
  though `SimulateValidation`/`StrictValidation` are already on), and `configureOverrides` is invoked
  against the `BundlerConfig` the host builds internally *before* the bundler is constructed - use it to
  tweak any other `BundlerConfig` field (mempool size, bundle gas, reputation) without hand-assembling
  the whole config yourself.
- **`UseHostedUrl(bundlerRpcUrl, chainId)`** is the counterpart for a bundler that is *not*
  in-process: it builds an `IAccountAbstractionBundlerService` from a plain JSON-RPC URL (an
  `Nethereum.AccountAbstraction.Bundler.RpcServer` instance, for example), so code written against
  `IAccountAbstractionBundlerService` can switch between an in-process host and a hosted bundler by
  changing only how the interface is obtained.

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction.Bundler](../Nethereum.AccountAbstraction.Bundler/README.md)** - The bundler service (`BundlerService`, `BundlerConfig`) wired up in-process
- **[Nethereum.DevChain](../Nethereum.DevChain/README.md)** - The in-process devchain node this package bootstraps

### See Also
- **[Nethereum.AccountAbstraction.Bundler.RpcServer](../Nethereum.AccountAbstraction.Bundler.RpcServer/README.md)** - The standalone, deployable/hosted bundler for production use - point real apps at this, not at `InProcessBundlerHost`
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 client-side types and the `IAAClient` on-ramp

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
