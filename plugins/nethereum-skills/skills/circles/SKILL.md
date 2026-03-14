---
name: circles
description: Interact with the Circles UBI protocol using Nethereum (.NET/C#). Use this skill whenever the user asks about Circles protocol, Universal Basic Income, CRC tokens, Circles balances, trust relationships, personal minting, demurrage tokens, or any Circles UBI interaction on Gnosis Chain with C# or .NET.
user-invocable: true
---

# Circles: UBI Protocol

[Circles](https://aboutcircles.com/faqs) is a decentralized Universal Basic Income protocol on Gnosis Chain. Every registered avatar receives 1 CRC per hour, with ~7% annual demurrage. Tokens transfer through trust networks — you can only send CRC to someone who trusts you. Nethereum's `Nethereum.Circles` package provides contract services for on-chain operations and custom RPC methods for data queries.

NuGet: `Nethereum.Circles`

```bash
dotnet add package Nethereum.Circles
```

## Query Balances

The Circles RPC endpoint exposes custom methods. The simplest balance check:

```csharp
var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
var getTotalBalance = new GetTotalBalanceV2(client);
string balance = await getTotalBalance.SendRequestAsync("0xAvatarAddress");
```

This returns the total balance across all Circles token types — personal CRC plus any group currencies.

## Hub Contract Interaction

The Hub is the central contract for avatar registration, trust, and minting:

```csharp
var hubService = new HubService(web3, "0xc12C1E50ABB450d6205Ea2C3Fa861b3B834d13e8");

bool isHuman = await hubService.IsHumanQueryAsync("0xAddress");
bool isTrusted = await hubService.IsTrustedQueryAsync("0xTruster", "0xTrustee");
var issuance = await hubService.CalculateIssuanceQueryAsync("0xHumanAddress");
```

These are read-only calls — no gas needed.

## Mint Personal CRC

Circles avatars typically use Gnosis Safe wallets. Mint through Safe execution using the `ChangeContractHandlerToSafeExecTransaction` extension from `Nethereum.GnosisSafe`:

```csharp
var hubService = new HubService(web3, hubAddress);
hubService.ChangeContractHandlerToSafeExecTransaction(safeAddress, privateKey);
await hubService.PersonalMintRequestAndWaitForReceiptAsync();
```

The Safe is the registered avatar, not the EOA. The extension method wraps the mint call in a Safe `execTransaction`.

## Transaction History

Paginated transaction history through the Circles RPC:

```csharp
var historyQuery = new GetTransactionHistoryQuery(client);
var transactions = await historyQuery.SendRequestAsync("0xAvatarAddress", 100);

foreach (var tx in transactions.Response)
    Console.WriteLine($"{tx.From} → {tx.To}: {tx.Value}");

// Next page
var nextPage = await historyQuery.MoveNextPageAsync(transactions);
```

## Trust Relationships

Query who trusts whom:

```csharp
var trustQuery = new GetTrustRelationsQuery(client);
var relations = await trustQuery.SendRequestAsync("0xAvatarAddress", 20);

foreach (var r in relations.Response)
    Console.WriteLine($"Truster: {r.Truster} → Trustee: {r.Trustee}");
```

Trust is directional — if A trusts B, B can send personal CRC to A.

## Get Avatar Info

Query metadata about a Circles avatar:

```csharp
var avatarInfo = new GetAvatarInfoQuery(client);
var info = await avatarInfo.SendRequestAsync("0xAvatarAddress");
```

This returns the avatar's name, type (human, organization, group), and associated token information.

## Token Types

| Type | Service | Description |
|------|---------|-------------|
| Demurrage | `DemurrageCirclesService` | Balance decreases ~7%/year — represents real purchasing power |
| Inflationary | `InflationaryCirclesService` | Nominal balance constant — minted amount increases over time |

Both are ERC-20 wrappers around the same underlying balance.

## Contract Addresses

| Contract | Network | Address |
|----------|---------|---------|
| Hub V2 | Gnosis Chain | `0xc12C1E50ABB450d6205Ea2C3Fa861b3B834d13e8` |
| RPC | Gnosis Chain | `https://rpc.aboutcircles.com/` |
| RPC | Chiado Testnet | `https://chiado-rpc.aboutcircles.com` |

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-circles
