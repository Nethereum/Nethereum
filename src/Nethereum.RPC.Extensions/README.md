# Nethereum.RPC.Extensions

**Nethereum.RPC.Extensions** provides RPC extensions for Ethereum development tools and test environments. It includes support for Hardhat, Anvil (Foundry), and generic EVM manipulation methods that allow developers to control blockchain state during testing and development.

## Features

- **Hardhat Network Support** - Full support for hardhat_* RPC methods
- **Anvil/Foundry Support** - Complete anvil_* RPC method implementations
- **Generic EVM Tools** - evm_* methods for state manipulation
- **Account Impersonation** - Sign transactions as any address (testing)
- **Time Travel** - Manipulate block timestamps and mine blocks
- **State Manipulation** - Set balances, nonces, storage, and contract code
- **Snapshot/Revert** - Save and restore blockchain state
- **Mining Control** - Mine blocks on demand, set base fees

## Installation

```bash
dotnet add package Nethereum.RPC.Extensions
```

## Dependencies

- `Nethereum.RPC` - Core RPC functionality

## Supported Development Environments

This package works with local Ethereum test nodes that support dev/test RPC methods:

- **Hardhat Network** - Node.js-based development environment
- **Anvil** - Foundry's local node (Rust-based, high performance)
- **Ganache** - Some methods supported
- **Custom Test Nodes** - Any node implementing evm_* or hardhat_* methods

## Quick Start

### Using with Hardhat

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545"); // Hardhat default port

// Access Hardhat service through extension method
var hardhat = web3.Eth.Hardhat();

// Impersonate any account (for testing)
await hardhat.ImpersonateAccount.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");

// Set account balance
await hardhat.SetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    new Nethereum.Hex.HexTypes.HexBigInteger(1000000000000000000) // 1 ETH
);

// Stop impersonating
await hardhat.StopImpersonatingAccount.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
```

### Using with Anvil (Foundry)

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545"); // Anvil default port

// Access Anvil service through extension method
var anvil = new AnvilService(web3.Eth);

// Impersonate account
await anvil.ImpersonateAccount.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");

// Set balance
await anvil.SetBalance.SendRequestAsync(
    "0x742d35Cc6634C0532925a3b844Bc454e4438f44e",
    new Nethereum.Hex.HexTypes.HexBigInteger(1000000000000000000)
);
```

### Using Generic EVM Tools

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545");

// Access EVM tools through extension method
var evmTools = web3.Eth.DevToolsEvm();

// Increase blockchain time
await evmTools.IncreaseTime.SendRequestAsync(86400); // Fast-forward 1 day

// Mine a block
await evmTools.Mine.SendRequestAsync();

// Snapshot state
var snapshotId = await evmTools.Snapshot.SendRequestAsync();

// ... perform some operations ...

// Revert to snapshot
await evmTools.EvmRevert.SendRequestAsync(snapshotId);
```

## Services Overview

### HardhatService

Complete implementation of Hardhat Network RPC methods:

| Method | Description |
|--------|-------------|
| `ImpersonateAccount` | Sign transactions as any address |
| `StopImpersonatingAccount` | Stop impersonating account |
| `SetBalance` | Set account ETH balance |
| `SetCode` | Set account bytecode |
| `SetNonce` | Set account nonce |
| `SetStorageAt` | Set contract storage slot |
| `SetCoinbase` | Set block.coinbase address |
| `SetNextBlockBaseFeePerGas` | Set EIP-1559 base fee |
| `SetPrevRandao` | Set PREVRANDAO value |
| `Mine` | Mine blocks instantly |
| `Reset` | Reset blockchain to initial state |
| `DropTransaction` | Remove transaction from mempool |
| `IncreaseTimeAsync` | Helper to increase time and mine |

### AnvilService

Extends HardhatService with Anvil-specific method names (same functionality, different RPC method names):

- Uses `anvil_*` method names instead of `hardhat_*`
- Fully compatible with Foundry's Anvil
- Inherits all HardhatService functionality

### EvmToolsService

Generic EVM manipulation methods (works across different dev tools):

| Method | Description |
|--------|-------------|
| `SetNextBlockTimestamp` | Set next block timestamp |
| `IncreaseTime` | Increase blockchain time |
| `Mine` | Mine a new block |
| `Snapshot` | Save blockchain state |
| `EvmRevert` | Restore blockchain state |
| `SetAccountBalance` | Set account balance |
| `SetAccountCode` | Set account code |
| `SetAccountNonce` | Set account nonce |
| `SetAccountStorageAt` | Set storage slot |
| `SetBlockGasLimit` | Set block gas limit |
| `AddAccount` | Add account to node |
| `RemoveAccount` | Remove account from node |

## Examples

### Example 1: Account Impersonation (Testing Whale Accounts)

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;
using Nethereum.Hex.HexTypes;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Impersonate a whale account (testing only!)
var whaleAddress = "0x47ac0Fb4F2D84898e4D9E7b4DaB3C24507a6D503";
await hardhat.ImpersonateAccount.SendRequestAsync(whaleAddress);

// Now you can send transactions as the whale
var usdcContract = web3.Eth.GetContract(usdcAbi, usdcAddress);
var transferFunction = usdcContract.GetFunction("transfer");

var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
    whaleAddress, // from (impersonated)
    new HexBigInteger(300000), // gas
    null, // value
    null, // gas price
    "0xYourTestAccount", // to
    1000000 // amount
);

// Stop impersonating when done
await hardhat.StopImpersonatingAccount.SendRequestAsync(whaleAddress);
```

**Use Case:** Test interactions with existing contracts without needing real tokens

### Example 2: Setting Account Balances for Testing

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Create test accounts
var account1 = new Account("0x" + new string('1', 64));
var account2 = new Account("0x" + new string('2', 64));

// Give them ETH for testing
await hardhat.SetBalance.SendRequestAsync(
    account1.Address,
    new HexBigInteger(10000000000000000000) // 10 ETH
);

await hardhat.SetBalance.SendRequestAsync(
    account2.Address,
    new HexBigInteger(5000000000000000000) // 5 ETH
);

// Verify balances
var balance1 = await web3.Eth.GetBalance.SendRequestAsync(account1.Address);
var balance2 = await web3.Eth.GetBalance.SendRequestAsync(account2.Address);

Console.WriteLine($"Account 1: {Web3.Convert.FromWei(balance1)} ETH");
Console.WriteLine($"Account 2: {Web3.Convert.FromWei(balance2)} ETH");
```

**Use Case:** Quickly setup test accounts with specific balances

### Example 3: Time Travel - Testing Time-Dependent Contracts

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Deploy a time-locked contract
var contract = await DeployTimeLockContract(web3);

// Try to unlock before time - should fail
try
{
    await contract.GetFunction("unlock").SendTransactionAndWaitForReceiptAsync(account.Address);
}
catch (Exception ex)
{
    Console.WriteLine("Unlock failed as expected: " + ex.Message);
}

// Fast-forward 7 days (604800 seconds)
await hardhat.IncreaseTimeAsync(604800);

// Now unlock should succeed
var receipt = await contract.GetFunction("unlock").SendTransactionAndWaitForReceiptAsync(account.Address);
Console.WriteLine($"Unlocked successfully after time travel: {receipt.Status == 1}");
```

**From:** `src/Nethereum.RPC.Extensions/HardhatService.cs:49`

**Use Case:** Test vesting schedules, time locks, auction deadlines without waiting

### Example 4: Snapshot and Revert - Testing Different Scenarios

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545");
var evmTools = web3.Eth.DevToolsEvm();

// Deploy contract and setup initial state
var contract = await DeployContract(web3);
await contract.GetFunction("initialize").SendTransactionAndWaitForReceiptAsync(account.Address);

// Save state
var snapshotId = await evmTools.Snapshot.SendRequestAsync();
Console.WriteLine($"Snapshot created: {snapshotId}");

// Test scenario 1: Destructive operation
await contract.GetFunction("withdraw").SendTransactionAndWaitForReceiptAsync(
    account.Address,
    amount: 1000000
);
var balance1 = await contract.GetFunction("getBalance").CallAsync<int>();
Console.WriteLine($"Balance after withdraw: {balance1}");

// Revert to saved state
await evmTools.EvmRevert.SendRequestAsync(snapshotId);

// Test scenario 2: Different operation
await contract.GetFunction("deposit").SendTransactionAndWaitForReceiptAsync(
    account.Address,
    amount: 500000
);
var balance2 = await contract.GetFunction("getBalance").CallAsync<int>();
Console.WriteLine($"Balance after deposit: {balance2}");
```

**Use Case:** Test multiple scenarios without redeploying contracts

### Example 5: Mining Blocks on Demand

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;
using Nethereum.RPC.Eth.DTOs;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

var startBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Starting block: {startBlock.Value}");

// Mine 10 blocks instantly
await hardhat.Mine.SendRequestAsync(
    numberOfBlocks: 10,
    interval: 12 // 12 seconds between blocks
);

var endBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Ending block: {endBlock.Value}");
Console.WriteLine($"Mined {endBlock.Value - startBlock.Value} blocks");
```

**Use Case:** Quickly advance blockchain for testing block-dependent logic

### Example 6: Setting Contract Storage Directly

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Contract address
var contractAddress = "0x1234...";

// Storage slot 0: Usually the owner address in many contracts
var storageSlot = "0x0000000000000000000000000000000000000000000000000000000000000000";

// Set storage to your address (make yourself the owner!)
var yourAddress = "0xYourAddress";
var paddedAddress = "0x" + yourAddress.Substring(2).PadLeft(64, '0');

await hardhat.SetStorageAt.SendRequestAsync(
    contractAddress,
    storageSlot,
    paddedAddress
);

// Now you're the owner and can call onlyOwner functions
Console.WriteLine("Storage slot updated - you are now the contract owner!");
```

**Use Case:** Bypass authorization for testing, manipulate contract state directly

### Example 7: Setting Contract Code (Hot Swapping)

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Original contract
var contractAddress = "0x1234...";

// Compile a new version with a bug fix or new feature
var newBytecode = "0x608060405234801561001057600080fd5b50..."; // New bytecode

// Hot-swap the contract code
await hardhat.SetCode.SendRequestAsync(contractAddress, newBytecode);

Console.WriteLine("Contract code updated - test the new version!");

// Contract now executes the new code at the same address
// Storage and balance are preserved
```

**Use Case:** Test contract upgrades, fix bugs mid-test without redeploying

### Example 8: Controlling EIP-1559 Base Fee

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;
using Nethereum.Hex.HexTypes;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Test with low base fee
await hardhat.SetNextBlockBaseFeePerGas.SendRequestAsync(
    new HexBigInteger(1000000000) // 1 gwei
);
await hardhat.Mine.SendRequestAsync();

var block1 = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
    BlockParameter.CreateLatest()
);
Console.WriteLine($"Block base fee: {block1.BaseFeePerGas.Value} wei");

// Test with high base fee (network congestion simulation)
await hardhat.SetNextBlockBaseFeePerGas.SendRequestAsync(
    new HexBigInteger(100000000000) // 100 gwei
);
await hardhat.Mine.SendRequestAsync();

var block2 = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
    BlockParameter.CreateLatest()
);
Console.WriteLine($"Block base fee: {block2.BaseFeePerGas.Value} wei");
```

**Use Case:** Test EIP-1559 transaction behavior under different fee conditions

### Example 9: Resetting Blockchain State

```csharp
using Nethereum.Web3;
using Nethereum.RPC.Extensions;

var web3 = new Web3("http://127.0.0.1:8545");
var hardhat = web3.Eth.Hardhat();

// Perform various operations
await DeployContracts(web3);
await ExecuteTransactions(web3);

var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Current block: {blockNumber.Value}");

// Reset to genesis
await hardhat.Reset.SendRequestAsync();

var newBlockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
Console.WriteLine($"Block after reset: {newBlockNumber.Value}"); // Should be 0 or 1

// Optionally fork from mainnet at specific block
var forkConfig = new
{
    forking = new
    {
        jsonRpcUrl = "https://mainnet.infura.io/v3/YOUR-PROJECT-ID",
        blockNumber = 18000000
    }
};

await hardhat.Reset.SendRequestAsync(forkConfig);
Console.WriteLine("Reset and forked from mainnet block 18000000");
```

**Use Case:** Clean slate between test runs, fork mainnet for testing

### Example 10: Complete Test Setup Workflow

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Extensions;
using Nethereum.Hex.HexTypes;

public class TestSetup
{
    private readonly Web3 web3;
    private readonly HardhatService hardhat;
    private string snapshotId;

    public TestSetup(string rpcUrl = "http://127.0.0.1:8545")
    {
        web3 = new Web3(rpcUrl);
        hardhat = web3.Eth.Hardhat();
    }

    public async Task SetupTestEnvironment()
    {
        // 1. Reset to clean state
        await hardhat.Reset.SendRequestAsync();

        // 2. Create and fund test accounts
        var testAccounts = new[]
        {
            "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC"
        };

        foreach (var account in testAccounts)
        {
            await hardhat.SetBalance.SendRequestAsync(
                account,
                new HexBigInteger(100000000000000000000) // 100 ETH
            );
        }

        // 3. Deploy contracts
        var contractAddress = await DeployTestContract();

        // 4. Setup initial contract state
        var contract = web3.Eth.GetContract(abi, contractAddress);
        await contract.GetFunction("initialize").SendTransactionAndWaitForReceiptAsync(
            testAccounts[0]
        );

        // 5. Save snapshot for quick reset between tests
        snapshotId = await web3.Eth.DevToolsEvm().Snapshot.SendRequestAsync();

        Console.WriteLine("Test environment ready!");
    }

    public async Task ResetToSnapshot()
    {
        await web3.Eth.DevToolsEvm().EvmRevert.SendRequestAsync(snapshotId);

        // Create new snapshot
        snapshotId = await web3.Eth.DevToolsEvm().Snapshot.SendRequestAsync();
    }

    private async Task<string> DeployTestContract()
    {
        // Contract deployment logic
        return "0xContractAddress";
    }
}

// Usage in tests
var setup = new TestSetup();
await setup.SetupTestEnvironment();

// Test 1
await RunTest1();
await setup.ResetToSnapshot();

// Test 2
await RunTest2();
await setup.ResetToSnapshot();
```

**Use Case:** Production-quality test setup with snapshots for fast test isolation

## Extension Methods

The package provides convenient extension methods on `IEthApiService`:

```csharp
using Nethereum.RPC.Extensions;

// Access Hardhat service
var hardhat = web3.Eth.Hardhat();

// Access EVM tools
var evmTools = web3.Eth.DevToolsEvm();

// Access Anvil service (create directly)
var anvil = new AnvilService(web3.Eth);
```

## API Reference

### HardhatService Methods

#### ImpersonateAccount
```csharp
Task SendRequestAsync(string address, object id = null)
```
Allows Hardhat Network to sign transactions as the given address.

#### SetBalance
```csharp
Task SendRequestAsync(string address, HexBigInteger balance, object id = null)
```
Set the balance of an account (in Wei).

#### SetCode
```csharp
Task SendRequestAsync(string address, string code, object id = null)
```
Set the bytecode of an account.

#### SetNonce
```csharp
Task SendRequestAsync(string address, HexBigInteger nonce, object id = null)
```
Set the nonce of an account.

#### SetStorageAt
```csharp
Task SendRequestAsync(string address, string slot, string value, object id = null)
```
Set a storage slot of a contract.

#### Mine
```csharp
Task SendRequestAsync(int? numberOfBlocks = null, int? interval = null, object id = null)
```
Mine one or more blocks.

#### IncreaseTimeAsync
```csharp
Task<HexBigInteger> IncreaseTimeAsync(uint numberInSeconds)
```
Helper method to increase time and mine a block.

**From:** `src/Nethereum.RPC.Extensions/HardhatService.cs:49`

### EvmToolsService Methods

#### Snapshot
```csharp
Task<string> SendRequestAsync(object id = null)
```
Save the current blockchain state. Returns a snapshot ID.

#### EvmRevert
```csharp
Task<bool> SendRequestAsync(string snapshotId, object id = null)
```
Restore blockchain state to a snapshot. Returns true if successful.

#### IncreaseTime
```csharp
Task<HexBigInteger> SendRequestAsync(uint seconds, object id = null)
```
Increase the blockchain time by the specified number of seconds.

#### Mine
```csharp
Task SendRequestAsync(object id = null)
```
Mine a single block.

## Best Practices

1. **Use for Testing Only**: These methods are ONLY for local development/test nodes. They will not work on mainnet or public testnets.

2. **Snapshot Before Destructive Operations**:
   ```csharp
   var snapshot = await evmTools.Snapshot.SendRequestAsync();
   try {
       await PerformDestructiveTest();
   } finally {
       await evmTools.EvmRevert.SendRequestAsync(snapshot);
   }
   ```

3. **Clean Up Impersonations**: Always stop impersonating accounts when done to avoid confusion in subsequent tests.

4. **Use Appropriate Tool Service**:
   - **HardhatService** - For Hardhat Network
   - **AnvilService** - For Foundry's Anvil (faster, more features)
   - **EvmToolsService** - For generic/portable dev tools

5. **Reset Between Test Suites**: Start each test suite with a clean state:
   ```csharp
   await hardhat.Reset.SendRequestAsync();
   ```

6. **Document Test Manipulations**: When using these tools, clearly comment what state you're manipulating and why.

## Differences Between Hardhat and Anvil

Both services provide the same functionality with different RPC method names:

| HardhatService | AnvilService | Description |
|----------------|--------------|-------------|
| `hardhat_impersonateAccount` | `anvil_impersonateAccount` | Impersonate account |
| `hardhat_setBalance` | `anvil_setBalance` | Set balance |
| `hardhat_mine` | `anvil_mine` | Mine blocks |
| `hardhat_reset` | `anvil_reset` | Reset blockchain |

**Anvil** (Foundry) is generally faster and has better performance, while **Hardhat** has more JavaScript ecosystem integration.

## Limitations

1. **Test Nodes Only**: These methods only work with development nodes that support them
2. **Not Standardized**: These are not part of official Ethereum JSON-RPC spec
3. **Node-Specific Behavior**: Some methods may behave slightly differently across different dev tools
4. **State Consistency**: Manipulating state directly can lead to inconsistent blockchain state if not careful

## Troubleshooting

### Method Not Found Error

```
Error: Method hardhat_impersonateAccount not found
```

**Solution**: Make sure you're connected to a Hardhat Network or Anvil node, not a regular Ethereum node.

### Invalid Snapshot ID

```
Error: Invalid snapshot id
```

**Solution**: Snapshot IDs are volatile. Don't reuse snapshot IDs after reverting. Create a new snapshot after each revert.

### Impersonation Not Working

```
Error: sender doesn't have enough funds
```

**Solution**: Set the impersonated account's balance before sending transactions:
```csharp
await hardhat.SetBalance.SendRequestAsync(address, new HexBigInteger(1000000000000000000));
```

## Related Packages

- **Nethereum.RPC** - Core RPC functionality
- **Nethereum.Web3** - High-level Web3 API
- **Nethereum.Contracts** - Smart contract interaction

## Development Tools

This package is designed to work with:

- **[Hardhat](https://hardhat.org/)** - Ethereum development environment
- **[Foundry](https://getfoundry.sh/)** - Blazing fast Ethereum toolkit
- **[Anvil](https://github.com/foundry-rs/foundry/tree/master/anvil)** - Local Ethereum node (part of Foundry)

## Additional Resources

- [Hardhat Network Reference](https://hardhat.org/hardhat-network/docs)
- [Anvil Documentation](https://book.getfoundry.sh/anvil/)
- [Foundry Documentation](https://book.getfoundry.sh/)
- [Nethereum Documentation](http://docs.nethereum.com)

## License

MIT License - see LICENSE file for details
