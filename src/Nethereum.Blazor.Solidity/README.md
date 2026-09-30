# Nethereum.Blazor.Solidity

Blazor components for EVM transaction debugging with Solidity source mapping, step-through execution, and Monaco editor integration.

## Overview

Nethereum.Blazor.Solidity provides an interactive EVM debugger for Blazor Server applications. It replays Ethereum transactions through the Nethereum EVM simulator (or via `debug_traceTransaction` RPC), matches deployed bytecode to Solidity source files, and presents a step-through debugging experience with source highlighting, stack/memory/storage inspection, and opcode-level navigation.

### Key Features

- `EvmDebugger` composite component with control bar, opcode list, source panel, and state panel
- `IEvmDebugService` / `EvmDebugService` replays transactions via EVM simulation with fallback to `debug_traceTransaction` opcode tracer
- Source map matching via `FileSystemABIInfoStorage.FindABIInfoByRuntimeBytecode()` for automatic Solidity file discovery
- `SolidityCodeViewer` wraps the Monaco editor via JavaScript interop for syntax-highlighted source display with line highlighting
- Step controls: start, end, step forward/back, next/previous source line, go-to-step
- State inspection tabs: Stack, Memory (32-byte chunks), Storage (key/value pairs), Call Info (contract, caller, depth, gas, opcode, decoded parameters)
- `AddSolidityDebugger()` DI extension for service registration

## Installation

```bash
dotnet add package Nethereum.Blazor.Solidity
```

Targets `net10.0`.

### Dependencies

- **Nethereum.EVM** - `EVMDebuggerSession`, `ProgramTrace`, `ProgramInstructionsUtils` for EVM simulation and trace stepping
- **Nethereum.Web3** - RPC client for `debug_traceTransaction` fallback and transaction/receipt retrieval

## Quick Start

### 1. Register Services

```csharp
using Nethereum.Blazor.Solidity;

builder.Services.AddSolidityDebugger();
```

This registers `IEvmDebugService` as a scoped `EvmDebugService`.

### 2. Include Static Assets

In your `_Host.cshtml` or `App.razor`:

```html
<script src="_content/Nethereum.Blazor.Solidity/solidity-monaco-interop.js"></script>
```

The Monaco editor is loaded from a CDN by the interop script.

### 3. Replay a Transaction and Render the Component

The `IEvmDebugService` replays the transaction and produces an `EvmReplayResult`. The `EvmDebugger` component renders that already-built session - it does not accept a transaction hash and does not replay.

```razor
@using Nethereum.Blazor.Solidity.Components
@using Nethereum.Blazor.Solidity.Services
@inject IEvmDebugService debugService

@if (result != null)
{
    <EvmDebugger Session="result.Session"
                 IsRevert="result.IsRevert"
                 HasSourceMaps="result.HasSourceMaps"
                 SourceFiles="result.SourceFiles"
                 FileContents="result.FileContents" />
}

@code {
    private EvmReplayResult result;
    private string txHash = "0xabc...";

    protected override async Task OnInitializedAsync()
    {
        result = await debugService.ReplayTransactionAsync(txHash);
    }
}
```

## Components

### EvmDebugger

Main composite component that orchestrates the debugging session. It renders an already-built session across four sub-components in a split layout; replaying a transaction into an `EvmReplayResult` is done by `IEvmDebugService`, whose result is passed into these parameters.

**Parameters:**
- `Session` (`EVMDebuggerSession`, required) - The debugger session to render
- `IsRevert` (`bool`) - Whether the replayed transaction reverted
- `HasSourceMaps` (`bool`) - Whether Solidity source maps were matched
- `SourceFiles` (`List<string>`) - Source file paths for the source panel
- `FileContents` (`Dictionary<string, string>`) - Source file contents keyed by path
- `Theme` (`string?`) - Monaco editor theme (defaults to `nethereum-dark`)

### DebugControlBar

Navigation controls for stepping through execution trace.

- **GoToStart** / **GoToEnd** - Jump to first or last trace step
- **StepBack** / **StepForward** - Move one opcode at a time
- **PrevSourceLine** / **NextSourceLine** - Jump to the next step that maps to a different source location
- **GoToInputStep** - Jump to a specific step number

Displays the current step index, total steps, and current opcode.

### DebugOpcodeList

Scrollable list of all opcodes in the execution trace. Each entry shows the program counter, opcode mnemonic, and gas cost. The current step is highlighted and auto-scrolled into view.

### DebugSourcePanel

Displays Solidity source files with the current execution line highlighted. When multiple source files are involved (e.g., imported contracts), file tabs allow switching between them. Uses `SolidityCodeViewer` internally.

### DebugStatePanel

Tabbed panel showing EVM state at the current execution step:

| Tab | Content |
|-----|---------|
| Stack | Current stack values (top-down, hex-encoded) |
| Memory | Memory contents displayed in 32-byte rows |
| Storage | Key/value pairs written to storage at the current contract |
| Call Info | Current contract address, caller, call depth, remaining gas, current opcode, and decoded function parameters |

### SolidityCodeViewer

Monaco editor wrapper via JavaScript interop (`solidity-monaco-interop.js`). Provides:

- Solidity syntax highlighting
- Read-only display mode
- Line highlighting for current execution position
- File registration and switching for multi-file debugging sessions

## Services

### IEvmDebugService

```csharp
public interface IEvmDebugService
{
    bool IsAvailable { get; }
    Task<EvmReplayResult> ReplayTransactionAsync(string txHash);
}
```

### EvmDebugService

Replays a transaction through the following pipeline:

1. Fetches the transaction and receipt via `eth_getTransactionByHash` / `eth_getTransactionReceipt`
2. Runs the transaction through `EVMSimulator` to produce a `ProgramResult` with full execution trace
3. If a `FileSystemABIInfoStorage` is registered, calls its `FindABIInfoByRuntimeBytecode()` to match deployed bytecode against compiled artifacts
4. When source maps are found, maps trace steps to Solidity source locations and loads source file contents
5. Falls back to `debug_traceTransaction` (opcode tracer) when EVM simulation is not available
6. Returns an `EvmReplayResult` containing the `EVMDebuggerSession`, step count, source files, and error information

### EvmReplayResult

```csharp
public class EvmReplayResult
{
    public EVMDebuggerSession Session { get; set; }
    public int TotalSteps { get; set; }
    public bool IsRevert { get; set; }
    public bool HasSourceMaps { get; set; }
    public string Error { get; set; }
    public List<string> SourceFiles { get; set; }
    public Dictionary<string, string> FileContents { get; set; }
}
```

## Source Map Matching

The debugger automatically discovers Solidity source files when an ABI output directory is provided (e.g., Forge's `out/` or Hardhat's `artifacts/`). The matching process:

1. `FileSystemABIInfoStorage` scans the directory for `*.json` compiled contract artifacts (Foundry-style `deployedBytecode.object` / `sourceMap`) and for `build-info` files (standard JSON output with `evm.deployedBytecode`)
2. For each contract in the trace, the deployed bytecode is compared against the stored artifacts
3. When a match is found, the source map and source file paths from the compilation output are used to map program counter values to source locations
4. Source files are loaded and displayed in the `DebugSourcePanel`

This works with Foundry artifacts and build-info standard JSON output.

## Related Packages

### Dependencies

- **Nethereum.EVM** - EVM simulator, debugger session, and trace infrastructure
- **Nethereum.Web3** - RPC client for transaction retrieval and debug tracing

### See Also

- [Nethereum.EVM](../Nethereum.EVM/README.md) - EVM simulator and instruction set
- [Nethereum.Blazor](../Nethereum.Blazor/README.md) - Blazor wallet integration and authentication
- [Nethereum.Explorer](../Nethereum.Explorer/README.md) - Blazor Server block explorer that embeds the debugger
