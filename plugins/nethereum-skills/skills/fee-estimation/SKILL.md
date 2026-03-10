---
name: fee-estimation
description: Estimate EIP-1559 gas fees with Nethereum. Use when the user asks about gas fees, fee estimation, maxFeePerGas, maxPriorityFeePerGas, base fee, or choosing a fee strategy for transactions.
user-invocable: true
---

# EIP-1559 Fee Estimation with Nethereum

NuGet: `Nethereum.RPC`

Source: `Fee1559SuggestionDocExampleTests`

## Required Usings

```csharp
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
```

## Fee Components

EIP-1559 splits gas fees into **baseFee** (protocol-set, burned) and **priorityFee** (tip to validator). Users set `maxPriorityFeePerGas` (tip ceiling) and `maxFeePerGas` (total ceiling). Actual cost = min(baseFee + priorityFee, maxFeePerGas).

## Fee1559 Model

Holds all fee components in a single object.

```csharp
var fee = new Fee1559
{
    BaseFee = 20_000_000_000,
    MaxPriorityFeePerGas = 2_000_000_000,
    MaxFeePerGas = 42_000_000_000
};
```

## Strategy 1: Simple (Default)

Fixed priority fee of 2 Gwei. MaxFee = 2 * baseFee + priorityFee. Fast, no RPC calls beyond latest block.

```csharp
var defaultPriority = SimpleFeeSuggestionStrategy.DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
// 2,000,000,000 (2 Gwei)

// Usage with Web3:
// var strategy = new SimpleFeeSuggestionStrategy(web3.Client);
// Fee1559 fee = await strategy.SuggestFeeAsync();
```

## Strategy 2: Median Priority Fee History

Uses `eth_feeHistory` to compute median priority fee from recent blocks. Applies a baseFee multiplier that decreases as baseFee rises.

### Base Fee Multiplier Tiers

```csharp
var strategy = new MedianPriorityFeeHistorySuggestionStrategy();

strategy.GetBaseFeeMultiplier(30_000_000_000);   // 2.0x  (< 40 Gwei)
strategy.GetBaseFeeMultiplier(50_000_000_000);   // 1.6x  (40-100 Gwei)
strategy.GetBaseFeeMultiplier(150_000_000_000);  // 1.4x  (100-200 Gwei)
strategy.GetBaseFeeMultiplier(300_000_000_000);  // 1.2x  (>= 200 Gwei)
```

### Estimate Priority Fee from Fee History

```csharp
var feeHistory = new FeeHistoryResult
{
    OldestBlock = new HexBigInteger(100),
    BaseFeePerGas = new[]
    {
        new HexBigInteger(20_000_000_000),
        new HexBigInteger(21_000_000_000)
    },
    GasUsedRatio = new decimal[] { 0.5m },
    Reward = new[]
    {
        new[] { new HexBigInteger(1_000_000_000) },
        new[] { new HexBigInteger(1_500_000_000) },
        new[] { new HexBigInteger(2_000_000_000) },
        new[] { new HexBigInteger(2_500_000_000) },
        new[] { new HexBigInteger(3_000_000_000) }
    }
};

var estimate = strategy.EstimatePriorityFee(feeHistory);
```

### Suggest Max Fee Using Multiplier

```csharp
var maxPriorityFee = new BigInteger(2_000_000_000);
var baseFee = new HexBigInteger(30_000_000_000);

Fee1559 result = strategy.SuggestMaxFeeUsingMultiplier(maxPriorityFee, baseFee);
// result.MaxFeePerGas > baseFee (includes multiplier headroom)
// result.MaxPriorityFeePerGas == maxPriorityFee
```

## Strategy 3: Time Preference

Returns an array of Fee1559 suggestions factoring in urgency. Uses 100 blocks of fee history. Best for UIs offering slow/medium/fast options.

```csharp
var strategy = new TimePreferenceFeeSuggestionStrategy();

// Build fee history (normally from eth_feeHistory RPC)
var feeHistory = new FeeHistoryResult
{
    OldestBlock = new HexBigInteger(1000),
    BaseFeePerGas = baseFees,   // HexBigInteger[101]
    GasUsedRatio = gasUsedRatio // decimal[100]
};

var tip = new BigInteger(2_000_000_000);
Fee1559[] fees = strategy.SuggestFees(feeHistory, tip);

foreach (var fee in fees)
{
    // fee.MaxFeePerGas >= fee.MaxPriorityFeePerGas
}
```

## Default Behavior in Web3

Web3's `TransactionManagerBase` defaults:
- **Strategy**: `TimePreferenceFeeSuggestionStrategy` (NOT Simple)
- **Transaction type**: EIP-1559 (`UseLegacyAsDefault = false`)
- **Auto-calculate**: `CalculateOrSetDefaultGasPriceFeesIfNotSet = true`

Override the strategy:
```csharp
web3.TransactionManager.Fee1559SuggestionStrategy =
    new SimpleFeeSuggestionStrategy(web3.Client);
```

Force legacy transactions:
```csharp
web3.TransactionManager.UseLegacyAsDefault = true;
// Or provide GasPrice in TransactionInput — legacy is used automatically
```

## Strategy Comparison

| Strategy | RPC Calls | Best For | Accuracy |
|----------|-----------|----------|----------|
| Simple | 1 (latest block) | Quick transactions, stable networks | Low |
| Median | 1 (eth_feeHistory) | General-purpose, backend services | Medium |
| TimePreference | 1 (eth_feeHistory, 100 blocks) | UI with slow/medium/fast options | High |

## Using with Web3 (Live)

```csharp
// Simple
var simple = new SimpleFeeSuggestionStrategy(web3.Client);
Fee1559 fee = await simple.SuggestFeeAsync();

// Median
var median = new MedianPriorityFeeHistorySuggestionStrategy(web3.Client);
Fee1559 fee = await median.SuggestFeeAsync();

// TimePreference
var timePref = new TimePreferenceFeeSuggestionStrategy(web3.Client);
Fee1559[] fees = await timePref.SuggestFeesAsync();
```
