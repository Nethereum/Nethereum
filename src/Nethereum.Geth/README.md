# Nethereum.Geth

Extended Web3 library for Go Ethereum (Geth) client. Provides RPC client methods for Admin, Debug, Miner, TxnPool, and Geth-specific Eth APIs.

## Overview

Nethereum.Geth extends `Nethereum.Web3` with Geth-specific JSON-RPC methods. Use `Web3Geth` instead of `Web3` to access additional APIs for node administration, transaction tracing, mining control, and mempool inspection.

**API Services:**
- **Admin** - Peer management, RPC/HTTP/WS server control, chain import/export
- **Debug** - Transaction and block tracing, profiling, memory/garbage collection stats
- **Miner** - Mining control (start, stop, set gas price)
- **TxnPool** - Transaction pool inspection (status, content)
- **GethEth** - Geth-specific eth methods (pending transactions, eth_call with state overrides)

## Installation

```bash
dotnet add package Nethereum.Geth
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Geth
```

## Dependencies

**Package References:**
- Nethereum.RPC
- Nethereum.Web3

## Usage

### Web3Geth Initialization

Replace `Web3` with `Web3Geth`:

```csharp
using Nethereum.Geth;

var web3 = new Web3Geth("http://localhost:8545");
```

With account:

```csharp
using Nethereum.Geth;
using Nethereum.Web3.Accounts;

var account = new Account("PRIVATE_KEY");
var web3 = new Web3Geth(account, "http://localhost:8545");
```

**From:** `src/Nethereum.Geth/Web3Geth.cs:10`

## Admin API

Manage node peers, RPC/HTTP/WS servers, and chain data.

### Check Connected Peers

```csharp
var peers = await web3.Admin.Peers.SendRequestAsync();
Console.WriteLine($"Connected peers: {peers.Count}");
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/AdminPeersTester.cs:18`

### Add Peer

```csharp
var enode = "enode://pubkey@ip:port";
var result = await web3.Admin.AddPeer.SendRequestAsync(enode);
```

**From:** `src/Nethereum.Geth/RPC/Admin/AdminAddPeer.cs`

### Get Node Info

```csharp
var nodeInfo = await web3.Admin.NodeInfo.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/IAdminApiService.cs:16`

### Start/Stop RPC Server

```csharp
// Start RPC server
var started = await web3.Admin.StartRPC.SendRequestAsync("localhost", 8545, "*", "web3,eth,net");

// Stop RPC server
var stopped = await web3.Admin.StopRPC.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/IAdminApiService.cs:17-19`

### Export/Import Chain

```csharp
// Export chain to file
var exported = await web3.Admin.ExportChain.SendRequestAsync("/path/to/export.rlp");

// Import chain from file
var imported = await web3.Admin.ImportChain.SendRequestAsync("/path/to/chain.rlp");
```

**From:** `src/Nethereum.Geth/IAdminApiService.cs:13-14`

## Debug API

Transaction and block tracing, profiling, memory statistics.

### Trace Transaction

```csharp
using Nethereum.Geth.RPC.Debug.DTOs;
using Newtonsoft.Json.Linq;

var txHash = "0x...";
var tracingOptions = new TracingOptions();
var trace = await web3.GethDebug.TraceTransaction.SendRequestAsync<JToken>(txHash, tracingOptions);
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:36`

### Trace Transaction with Tracers

Geth supports built-in tracers for structured transaction analysis.

**Call Tracer:**

```csharp
using Nethereum.Geth.RPC.Debug.Tracers;

var trace = await web3.GethDebug.TraceTransaction.SendRequestAsync<CallTracerResponse>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new CallTracerInfo(onlyTopCalls: false, withLogs: true)
    });
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:75`

**4Byte Tracer (function selector frequency):**

```csharp
var trace = await web3.GethDebug.TraceTransaction.SendRequestAsync<FourByteTracerResponse>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new FourByteTracerInfo()
    });
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:89`

**Opcode Tracer (EVM opcode execution):**

```csharp
var trace = await web3.GethDebug.TraceTransaction.SendRequestAsync<OpcodeTracerResponse>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new OpcodeTracerInfo(
            enableMemory: true,
            disableStack: false,
            disableStorage: false,
            enableReturnData: true,
            debug: false,
            limit: 10)
    });
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:166`

**Prestate Tracer (account state before execution):**

```csharp
// Prestate mode
var prestateTrace = await web3.GethDebug.TraceTransaction.SendRequestAsync<PrestateTracerResponsePrestateMode>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new PrestateTracerInfo(diffMode: false)
    });

// Diff mode
var diffTrace = await web3.GethDebug.TraceTransaction.SendRequestAsync<PrestateTracerResponseDiffMode>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new PrestateTracerInfo(diffMode: true)
    });
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:180`

**Additional Tracers:**
- `UnigramTracerInfo` - Opcode frequency (single opcodes)
- `BigramTracerInfo` - Opcode pairs frequency
- `TrigramTracerInfo` - Opcode triples frequency
- `OpcountTracerInfo` - Total opcode count

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:115-140`

### Trace Block

```csharp
var blockNumber = new Nethereum.Hex.HexTypes.HexBigInteger(12345);
var blockTrace = await web3.GethDebug.TraceBlockByNumber.SendRequestAsync(blockNumber, tracingOptions);
```

**From:** `src/Nethereum.Geth/IDebugApiService.cs:25`

### Custom JavaScript Tracer

```csharp
var customTracerCode = @"{
    data: [],
    fault: function(log) {},
    step: function(log) { this.data.push(log.op.toString()); },
    result: function() { return this.data; }
}";

var trace = await web3.GethDebug.TraceTransaction.SendRequestAsync<JToken>(
    txHash,
    new TracingOptions
    {
        Timeout = "1m",
        Reexec = 128,
        TracerInfo = new CustomTracerInfo(customTracerCode)
    });
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/DebugTraceTransactionTester.cs:205`

### Get Memory Stats

```csharp
var memStats = await web3.GethDebug.MemStats.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/IDebugApiService.cs:14`

### Get Block RLP

```csharp
var blockNumber = new Nethereum.Hex.HexTypes.HexBigInteger(100);
var rlp = await web3.GethDebug.GetBlockRlp.SendRequestAsync(blockNumber);
```

**From:** `src/Nethereum.Geth/IDebugApiService.cs:12`

### CPU Profiling

```csharp
// Start CPU profiling
await web3.GethDebug.StartCPUProfile.SendRequestAsync("/path/to/profile.prof");

// Stop profiling
await web3.GethDebug.StopCPUProfile.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/IDebugApiService.cs:19-21`

## Miner API

Control mining operations.

### Start Mining

```csharp
// Start with 1 thread (argument is optional, default is 1)
var result = await web3.Miner.Start.SendRequestAsync();
```

**From:** `tests/Nethereum.Geth.IntegrationTests/Testers/MinerStartTester.cs:16`

### Stop Mining

```csharp
var result = await web3.Miner.Stop.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/RPC/Miner/MinerStop.cs`

### Set Gas Price

```csharp
using Nethereum.Hex.HexTypes;

var gasPrice = new HexBigInteger(1000000000); // 1 gwei
var result = await web3.Miner.SetGasPrice.SendRequestAsync(gasPrice);
```

**From:** `src/Nethereum.Geth/RPC/Miner/MinerSetGasPrice.cs`

## TxnPool API

Inspect transaction pool (mempool).

### Get Pool Status

```csharp
var status = await web3.TxnPool.PoolStatus.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/ITxnPoolApiService.cs:9`

### Get Pool Content

```csharp
var content = await web3.TxnPool.PoolContent.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/ITxnPoolApiService.cs:7`

### Inspect Pool

```csharp
var inspect = await web3.TxnPool.PoolInspect.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/ITxnPoolApiService.cs:8`

## GethEth API

Geth-specific eth methods.

### Get Pending Transactions

```csharp
var pendingTxs = await web3.GethEth.PendingTransactions.SendRequestAsync();
```

**From:** `src/Nethereum.Geth/IGethEthApiService.cs:7`

### eth_call with State Overrides

Execute contract call with temporary state modifications (e.g., replace contract code, override balances).

```csharp
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

var stateChanges = new Dictionary<string, StateChange>
{
    ["0xContractAddress"] = new StateChange
    {
        Code = "0x6080604052..." // Override contract code
    }
};

var result = await web3.GethEth.Call.SendRequestAsync(
    new CallInput
    {
        To = "0xContractAddress",
        Data = "0x893d20e8" // Function selector
    },
    BlockParameter.CreateLatest(),
    stateChanges);
```

**From:** `tests/Nethereum.Contracts.IntegrationTests/SmartContracts/GethCallTest.cs:39`

## VM Stack Error Checking

Analyze VM execution traces for errors.

```csharp
var stackErrorChecker = web3.GethDebug.StackErrorChecker;
// Use with trace results to detect stack errors
```

**From:** `src/Nethereum.Geth/IDebugApiService.cs:17`

## Admin API Service

**Interface:** `IAdminApiService` (`src/Nethereum.Geth/IAdminApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| AddPeer | admin_addPeer | Add peer by enode URL |
| RemovePeer | admin_removePeer | Remove peer |
| AddTrustedPeer | admin_addTrustedPeer | Add trusted peer |
| RemoveTrustedPeer | admin_removeTrustedPeer | Remove trusted peer |
| Peers | admin_peers | List connected peers |
| NodeInfo | admin_nodeInfo | Get node information |
| Datadir | admin_datadir | Get data directory path |
| StartRPC | admin_startRPC | Start RPC server |
| StopRPC | admin_stopRPC | Stop RPC server |
| StartHTTP | admin_startHTTP | Start HTTP server |
| StopHTTP | admin_stopHTTP | Stop HTTP server |
| StartWS | admin_startWS | Start WebSocket server |
| StopWS | admin_stopWS | Stop WebSocket server |
| ExportChain | admin_exportChain | Export blockchain to file |
| ImportChain | admin_importChain | Import blockchain from file |

## Debug API Service

**Interface:** `IDebugApiService` (`src/Nethereum.Geth/IDebugApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| TraceTransaction | debug_traceTransaction | Trace transaction execution |
| TraceBlock | debug_traceBlock | Trace all transactions in block |
| TraceBlockByNumber | debug_traceBlockByNumber | Trace block by number |
| TraceBlockByHash | debug_traceBlockByHash | Trace block by hash |
| TraceBlockFromFile | debug_traceBlockFromFile | Trace block from RLP file |
| TraceCall | debug_traceCall | Trace eth_call execution |
| GetBlockRlp | debug_getBlockRlp | Get RLP-encoded block |
| DumpBlock | debug_dumpBlock | Dump block state |
| SeedHash | debug_seedHash | Get PoW seed hash |
| BacktraceAt | debug_backtraceAt | Set logging backtrace location |
| Verbosity | debug_verbosity | Set logging verbosity |
| Vmodule | debug_vmodule | Set per-module verbosity |
| Stacks | debug_stacks | Get goroutine stack traces |
| MemStats | debug_memStats | Get memory allocation statistics |
| GcStats | debug_gcStats | Get garbage collection statistics |
| CpuProfile | debug_cpuProfile | Write CPU profile |
| StartCPUProfile | debug_startCPUProfile | Start CPU profiling |
| StopCPUProfile | debug_stopCPUProfile | Stop CPU profiling |
| StartGoTrace | debug_startGoTrace | Start Go execution trace |
| StopGoTrace | debug_stopGoTrace | Stop Go execution trace |
| BlockProfile | debug_blockProfile | Write goroutine blocking profile |
| SetBlockProfileRate | debug_setBlockProfileRate | Set blocking profile rate |
| GoTrace | debug_goTrace | Write Go execution trace |

## Miner API Service

**Interface:** `IMinerApiService` (`src/Nethereum.Geth/IMinerApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| Start | miner_start | Start mining |
| Stop | miner_stop | Stop mining |
| SetGasPrice | miner_setGasPrice | Set minimum gas price |

## TxnPool API Service

**Interface:** `ITxnPoolApiService` (`src/Nethereum.Geth/ITxnPoolApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| PoolStatus | txpool_status | Get transaction pool status (pending/queued counts) |
| PoolContent | txpool_content | Get full transaction pool content |
| PoolInspect | txpool_inspect | Get transaction pool summary |

## GethEth API Service

**Interface:** `IGethEthApiService` (`src/Nethereum.Geth/IGethEthApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| PendingTransactions | eth_pendingTransactions | Get pending transactions |
| Call | eth_call | Execute call with state overrides |

## Related Packages

- **Nethereum.Web3** - Base Web3 implementation
- **Nethereum.RPC** - JSON-RPC client infrastructure
- **Nethereum.Besu** - Hyperledger Besu-specific APIs
- **Nethereum.Parity** - OpenEthereum/Parity-specific APIs

## Additional Resources

- [Geth JSON-RPC Documentation](https://geth.ethereum.org/docs/rpc/server)
- [Geth Debug API](https://geth.ethereum.org/docs/interacting-with-geth/rpc/ns-debug)
- [Nethereum Documentation](http://docs.nethereum.com)
