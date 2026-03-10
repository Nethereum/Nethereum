---
name: unit-conversion
description: Convert between Wei, Ether, Gwei, and other Ethereum denominations with Nethereum. Use when the user needs to convert ETH units, handle token decimals, or work with BigDecimal precision for large values.
user-invocable: true
---

# Unit Conversion

NuGet: `Nethereum.Util` (included in `Nethereum.Web3`)

Source: `tests/Nethereum.ABI.UnitTests/UtilDocExampleTests.cs`

## Wei to Ether and Back

```csharp
using System.Numerics;
using Nethereum.Util;

var convert = UnitConversion.Convert;

// Ether to Wei
var oneEtherInWei = convert.ToWei(1, UnitConversion.EthUnit.Ether);
// Result: 1000000000000000000

// Wei to Ether
var etherValue = convert.FromWei(BigInteger.Parse("1500000000000000000"));
// Result: 1.5m
```

## Gwei Conversion

```csharp
var convert = UnitConversion.Convert;

// Gwei to Wei (e.g., gas price)
var gweiInWei = convert.ToWei(21, UnitConversion.EthUnit.Gwei);
// Result: 21000000000

// Wei to Gwei
var gweiValue = convert.FromWei(BigInteger.Parse("21000000000"), UnitConversion.EthUnit.Gwei);
// Result: 21m
```

## Other Denominations

```csharp
var convert = UnitConversion.Convert;

var finneyInWei = convert.ToWei(1, UnitConversion.EthUnit.Finney);
// Result: 1000000000000000

var szaboInWei = convert.ToWei(1, UnitConversion.EthUnit.Szabo);
// Result: 1000000000000

var ketherInWei = convert.ToWei(1, UnitConversion.EthUnit.Kether);
// Result: 1000000000000000000000
```

## Custom Decimals for ERC-20 Tokens

Use integer decimal places instead of `EthUnit` for tokens with non-18 decimals (e.g., USDC = 6, WBTC = 8).

```csharp
var convert = UnitConversion.Convert;

// USDC (6 decimals): raw value to human-readable
var usdcValue = convert.FromWei(BigInteger.Parse("1000000"), 6);
// Result: 1m

// Human-readable to raw value
var usdcRaw = convert.ToWei(1m, 6);
// Result: 1000000
```

## BigDecimal Precision

For very large values where `decimal` would lose precision, use `FromWeiToBigDecimal`.

```csharp
var convert = UnitConversion.Convert;
var largeWei = BigInteger.Parse("123456789012345678901234567890");

var bigDecimal = convert.FromWeiToBigDecimal(largeWei, UnitConversion.EthUnit.Ether);

// Round-trip back to Wei without precision loss
var backToWei = convert.ToWei(bigDecimal, UnitConversion.EthUnit.Ether);
// backToWei == largeWei
```

## Denomination Reference Table

| Unit    | EthUnit Enum               | Wei                         |
|---------|----------------------------|-----------------------------|
| Wei     | `EthUnit.Wei`              | 1                           |
| Gwei    | `EthUnit.Gwei`             | 1,000,000,000               |
| Szabo   | `EthUnit.Szabo`            | 1,000,000,000,000           |
| Finney  | `EthUnit.Finney`           | 1,000,000,000,000,000       |
| Ether   | `EthUnit.Ether`            | 1,000,000,000,000,000,000   |
| Kether  | `EthUnit.Kether`           | 1,000,000,000,000,000,000,000 |

For custom token decimals, pass an `int` instead of `EthUnit`:
- USDC/USDT: 6
- WBTC: 8
- DAI/ETH: 18
