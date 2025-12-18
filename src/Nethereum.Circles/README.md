# Nethereum.Circles

Nethereum service library for interacting with [Circles](https://circles.garden/) UBI (Universal Basic Income) protocol contracts on Gnosis Chain.

## About Circles

[Circles](https://aboutcircles.com/faqs) is a decentralized digital currency protocol that implements a fair Universal Basic Income system. Key features include:

- **Universal Issuance**: Every participant receives 1 CRC (Circles token) per hour automatically
- **Daily Burn Mechanism (Demurrage)**: All balances decrease by approximately 7% annually to ensure newly-created CRC remains valuable
- **Trust-Based Transactions**: Users establish trust connections enabling token transfers through social networks
- **Group Currencies**: Members can convert personal CRC into fungible group currencies for community commerce

The protocol operates on Gnosis Chain through smart contracts, with users maintaining complete self-custody of their funds.

## Installation

```bash
dotnet add package Nethereum.Circles
```

## Core Components

### Contract Services

The library provides generated contract services for all Circles V2 contracts:

- **HubService**: Main hub contract for avatar registration, trust management, token minting, and transfers
- **DemurrageCirclesService**: ERC20-wrapped Circles tokens with demurrage (time-value decay)
- **InflationaryCirclesService**: ERC20-wrapped Circles tokens with inflationary representation
- **MigrationService**: Contract for migrating from Circles V1 to V2
- **NameRegistryService**: Service for avatar name registration

### RPC Extensions

Custom RPC methods for Circles-specific data queries:

- **GetTotalBalance / GetTotalBalanceV2**: Get the total CRC balance for an avatar
- **CirclesQuery**: Generic paginated query interface for Circles data
- **GetTransactionHistoryQuery**: Paginated transaction history for an account
- **GetTrustRelationsQuery**: Query trust relationships for an avatar
- **GetAvatarInfoQuery**: Get avatar information including name, type, and token ID

## Usage Examples

### Query Balance

```csharp
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using Nethereum.Circles.RPC.Requests;

var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));

// Get total balance (V2)
var getTotalBalanceV2 = new GetTotalBalanceV2(client);
string balance = await getTotalBalanceV2.SendRequestAsync("0xYourAvatarAddress");
Console.WriteLine($"Total Balance: {balance}");
```

### Interact with Hub Contract

```csharp
using Nethereum.Web3;
using Nethereum.Circles.Contracts.Hub;

var web3 = new Web3("https://rpc.aboutcircles.com/");
var hubAddress = "0xc12C1E50ABB450d6205Ea2C3Fa861b3B834d13e8"; // Gnosis Chain V2 Hub

var hubService = new HubService(web3, hubAddress);

// Check if address is a human avatar
bool isHuman = await hubService.IsHumanQueryAsync("0xYourAddress");

// Check trust relationship
bool isTrusted = await hubService.IsTrustedQueryAsync("0xTruster", "0xTrustee");

// Calculate pending issuance for a human
var issuance = await hubService.CalculateIssuanceQueryAsync("0xHumanAddress");
```

### Personal Mint with Gnosis Safe

```csharp
using Nethereum.Web3;
using Nethereum.GnosisSafe;
using Nethereum.Circles.Contracts.Hub;

var privateKey = "0x...";
var safeAddress = "0xYourSafeAddress";

var web3 = new Web3(new Nethereum.Web3.Accounts.Account(privateKey), "https://rpc.aboutcircles.com/");
var hubService = new HubService(web3, hubAddress);

// Configure to execute through Gnosis Safe
hubService.ChangeContractHandlerToSafeExecTransaction(safeAddress, privateKey);

// Mint personal Circles
await hubService.PersonalMintRequestAndWaitForReceiptAsync();
```

### Query Transaction History

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.Circles.RPC.Requests;

var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
var transactionHistoryQuery = new GetTransactionHistoryQuery(client);

// Get first page
var transactions = await transactionHistoryQuery.SendRequestAsync("0xAvatarAddress", 100);

foreach (var tx in transactions.Response)
{
    Console.WriteLine($"Hash: {tx.TransactionHash}, Value: {tx.Value}, From: {tx.From}, To: {tx.To}");
}

// Get next page
transactions = await transactionHistoryQuery.MoveNextPageAsync(transactions);
```

### Query Trust Relations

```csharp
using Nethereum.JsonRpc.Client;
using Nethereum.Circles.RPC.Requests;

var client = new RpcClient(new Uri("https://rpc.aboutcircles.com/"));
var trustQuery = new GetTrustRelationsQuery(client);

var trustRelations = await trustQuery.SendRequestAsync("0xAvatarAddress", 20);

foreach (var relation in trustRelations.Response)
{
    Console.WriteLine($"Trustee: {relation.Trustee}, Truster: {relation.Truster}");
}
```

## Contract Addresses

### Gnosis Chain (Production)
- Hub V2: `0xc12C1E50ABB450d6205Ea2C3Fa861b3B834d13e8`
- RPC Endpoint: `https://rpc.aboutcircles.com/`

### Chiado Testnet
- RPC Endpoint: `https://chiado-rpc.aboutcircles.com`

## Dependencies

- **Nethereum.Web3**: Core Web3 functionality
- **Nethereum.GnosisSafe**: For Safe-based transaction execution (optional)

## References

- [Circles Website](https://circles.garden/)
- [About Circles - FAQs](https://aboutcircles.com/faqs)
- [Circles V2 Documentation](https://docs.aboutcircles.com/)
