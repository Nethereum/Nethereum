---
name: events
description: Filter, query, and decode smart contract events and logs using Nethereum (.NET). Use this skill whenever the user asks about event filtering, log querying, listening for contract events, decoding event logs, Transfer events, Approval events, or any EVM log processing with C# or .NET.
user-invocable: true
---

# Smart Contract Events

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Define an Event DTO

Map a Solidity event to a C# class using `[Event]` and `[Parameter]` attributes. The fourth argument (`true`) marks indexed (topic) parameters.

```csharp
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "_from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "_to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, false)]
    public BigInteger Value { get; set; }
}
```

Nethereum ships `TransferEventDTO` and `ApprovalEventDTO` for ERC-20 in `Nethereum.Contracts.Standards.ERC20.ContractDefinition`.

## Get an Event Handler

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
var contractAddress = "0xYourContractAddress";

// From web3
var transferEvent = web3.Eth.GetEvent<TransferEventDTO>(contractAddress);

// From contract handler
var contractHandler = web3.Eth.GetContractHandler(contractAddress);
var transferEvent = contractHandler.GetEvent<TransferEventDTO>();
```

## Query All Historical Events

```csharp
var filterInput = transferEvent.CreateFilterInput(
    BlockParameter.CreateEarliest(),
    BlockParameter.CreateLatest());

var allTransfers = await transferEvent.GetAllChangesAsync(filterInput);

foreach (var transfer in allTransfers)
{
    Console.WriteLine(
        $"From: {transfer.Event.From} To: {transfer.Event.To} Value: {transfer.Event.Value}");
}
```

## Filter by Indexed Parameters

Indexed parameters become topics the node filters server-side.

### By sender (first indexed param)

```csharp
var filterByFrom = transferEvent.CreateFilterInput(fromAddress);
var results = await transferEvent.GetAllChangesAsync(filterByFrom);
```

### By recipient (second indexed param)

Pass `null` for the first topic to skip it:

```csharp
var filterByTo = transferEvent.CreateFilterInput<string, string>(null, toAddress);
var results = await transferEvent.GetAllChangesAsync(filterByTo);
```

### By both sender and recipient

```csharp
var filterBoth = transferEvent.CreateFilterInput(fromAddress, toAddress);
var results = await transferEvent.GetAllChangesAsync(filterBoth);
```

### By arrays of addresses

```csharp
var filterFromArray = transferEvent.CreateFilterInput(new[] { addr1, addr2 });

var filterToArray = transferEvent.CreateFilterInput(null, new[] { recipA, recipB });

var filterBothArrays = transferEvent.CreateFilterInput(
    new[] { addr1, addr2 },
    new[] { recipA, recipB });
```

## Live Filter (Poll for New Events)

Install a filter and poll for changes instead of querying history:

```csharp
var contractHandler = web3.Eth.GetContractHandler(contractAddress);
var eventFilter = contractHandler.GetEvent<TransferEventDTO>();

var filterId = await eventFilter.CreateFilterAsync();

// ... transactions happen ...

var newEvents = await eventFilter.GetFilterChangesAsync(filterId);
foreach (var evt in newEvents)
{
    Console.WriteLine($"New transfer: {evt.Event.Value}");
}
```

## Decode Events from Transaction Receipt

```csharp
var receipt = await web3.Eth.GetTransactionReceipt
    .SendRequestAsync(transactionHash);

var transfers = receipt.DecodeAllEvents<TransferEventDTO>();

foreach (var transfer in transfers)
{
    Console.WriteLine(
        $"From: {transfer.Event.From} To: {transfer.Event.To} Value: {transfer.Event.Value}");
}
```

From a JArray of logs:

```csharp
var decoded = receipt.Logs.DecodeAllEvents<TransferEventDTO>();
```

## Custom Event DTO

```csharp
[Event("ItemCreated")]
public class ItemCreatedEventDTO : IEventDTO
{
    [Parameter("uint256", "itemId", 1, true)]
    public BigInteger ItemId { get; set; }

    [Parameter("address", "result", 2, false)]
    public string Result { get; set; }
}

var items = receipt.DecodeAllEvents<ItemCreatedEventDTO>();
```

## Query Events Across All Contracts

```csharp
// No contract address -- matches all contracts
var eventForAny = new Event<TransferEventDTO>(web3.Client);
var filterInput = eventForAny.CreateFilterInput();
var allTransfers = await eventForAny.GetAllChangesAsync(filterInput);

// Scoped to one contract
var eventForOne = new Event<TransferEventDTO>(web3.Client, contractAddress);
var results = await eventForOne.GetAllChangesAsync(eventForOne.CreateFilterInput());
```

## Built-in ERC-20 Event DTOs

```csharp
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

// TransferEventDTO - Transfer(address indexed _from, address indexed _to, uint256 _value)
// ApprovalEventDTO - Approval(address indexed _owner, address indexed _spender, uint256 _value)

var transfers = receipt.DecodeAllEvents<TransferEventDTO>();
var approvals = receipt.DecodeAllEvents<ApprovalEventDTO>();
```

## Typed Topic Filtering with FilterInputBuilder

Fluent, lambda-based API for building event filters -- reference indexed properties by name with compile-time checking:

```csharp
// Filter by sender
var filter = new FilterInputBuilder<TransferEventDTO>()
    .AddTopic(t => t.From, "0xSenderAddress")
    .Build(contractAddress);

var results = await transferEvent.GetAllChangesAsync(filter);

// OR matching
var filter = new FilterInputBuilder<TransferEventDTO>()
    .AddTopic(t => t.From, new[] { "0xAddr1", "0xAddr2" })
    .Build(contractAddress);

// Multiple indexed params + block range
var filter = new FilterInputBuilder<TransferEventDTO>()
    .AddTopic(t => t.From, "0xFrom")
    .AddTopic(t => t.To, "0xTo")
    .Build(contractAddress, BlockParameter.CreateEarliest(), BlockParameter.CreateLatest());

// Multiple contracts
var filter = new FilterInputBuilder<TransferEventDTO>()
    .AddTopic(t => t.From, "0xSender")
    .Build(new[] { contract1, contract2 });
```

## Key Types

- `Event<T>` -- typed event handler, created from web3 or client
- `FilterInputBuilder<T>` -- fluent lambda-based filter builder for indexed topics
- `IEventDTO` -- marker interface for event DTOs
- `[Event("Name")]` -- maps DTO to Solidity event name
- `[Parameter("type", "name", order, indexed)]` -- maps DTO property to event parameter
- `EventLog<T>` -- wrapper with `.Event` (decoded DTO), `.Log` (raw FilterLog)
- `NewFilterInput` -- filter configuration with address, topics, block range
- `BlockParameter.CreateEarliest()` / `BlockParameter.CreateLatest()` -- block range bounds

## Common Patterns

| Task | Method |
|------|--------|
| Get event handler from web3 | `web3.Eth.GetEvent<T>(address)` |
| Get event handler from contract | `contractHandler.GetEvent<T>()` |
| Query historical events | `event.GetAllChangesAsync(filterInput)` |
| Install live filter | `event.CreateFilterAsync()` |
| Poll live filter | `event.GetFilterChangesAsync(filterId)` |
| Decode from receipt | `receipt.DecodeAllEvents<T>()` |
| Decode from JArray logs | `receipt.Logs.DecodeAllEvents<T>()` |
| Filter by first indexed param | `event.CreateFilterInput(value)` |
| Filter by second indexed param | `event.CreateFilterInput<T1,T2>(null, value)` |
| Filter by both indexed params | `event.CreateFilterInput(val1, val2)` |
| Filter by address arrays | `event.CreateFilterInput(new[]{...}, new[]{...})` |
| Cross-contract query | `new Event<T>(web3.Client)` |
