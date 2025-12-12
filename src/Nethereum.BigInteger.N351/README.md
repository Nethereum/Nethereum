# Nethereum.BigInteger.N351

System.Numerics.BigInteger backport for .NET Framework 3.5, enabling arbitrary-precision integer arithmetic in legacy applications.

## Overview

Nethereum.BigInteger.N351 is a complete port of the .NET Foundation's `System.Numerics.BigInteger` implementation specifically for .NET Framework 3.5. This package enables Nethereum to support legacy platforms that predate the introduction of `System.Numerics.BigInteger` in .NET 4.0.

**Key Features:**
- Full BigInteger API compatibility with modern .NET
- Arbitrary-precision signed integer arithmetic
- Supports all standard operations: addition, subtraction, multiplication, division, modulus
- Advanced operations: GCD, modular exponentiation, power operations
- Conversions to/from all primitive numeric types and byte arrays
- Licensed under MIT by the .NET Foundation
- Only used when targeting .NET Framework 3.5 (controlled by `#if DOTNET35` directives)

**Important:** This package is **only required for .NET 3.5 applications**. Modern .NET applications (4.0+, .NET Core, .NET 5+) use the built-in `System.Numerics.BigInteger` instead.

## Installation

```bash
dotnet add package Nethereum.BigInteger.N351
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.BigInteger.N351
```

**Note:** This package is automatically referenced by Nethereum when building for .NET 3.5 targets. You typically don't need to reference it directly.

## Dependencies

**External:**
- .NET Framework 3.5 only
- No external dependencies

**Nethereum:**
- None (foundational package)

## Key Concepts

### What is BigInteger?

`BigInteger` represents an arbitrarily large signed integer. Unlike primitive types (`int`, `long`), BigInteger can represent numbers of any size, limited only by available memory.

### Why .NET 3.5 Support?

`System.Numerics.BigInteger` was introduced in .NET Framework 4.0. For applications targeting .NET 3.5 (including Unity3D with .NET 3.5 scripting backend), this backport provides the same functionality.

### Ethereum and BigInteger

Ethereum uses 256-bit unsigned integers (`uint256`) for many values:
- Token amounts (18+ decimal places for ERC-20 tokens)
- Wei values (1 ETH = 10^18 wei)
- Gas prices and limits
- Block numbers and timestamps
- Cryptographic operations

BigInteger is essential for handling these large values correctly in .NET.

### Namespace

The implementation lives in `System.Numerics` namespace (same as modern .NET) for API compatibility:

```csharp
using System.Numerics;

var value = new BigInteger(1000000000000000000); // 10^18
```

## Quick Start

```csharp
using System;
using System.Numerics;

// Create from integer
var small = new BigInteger(1000);

// Create from byte array (little-endian)
byte[] bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
var big = new BigInteger(bytes);

// Arithmetic operations
var sum = small + big;
var product = small * 1000000;

// Ethereum wei conversion (1 ETH = 10^18 wei)
var oneEth = BigInteger.Pow(10, 18);
Console.WriteLine($"1 ETH = {oneEth} wei");
```

## Usage Examples

### Example 1: Creating BigInteger Values

```csharp
using System;
using System.Numerics;

// From primitive types
var fromInt = new BigInteger(42);
var fromLong = new BigInteger(9223372036854775807L);
var fromULong = new BigInteger(18446744073709551615UL);

// From double (truncates decimal part)
var fromDouble = new BigInteger(123.456); // 123

// From decimal
var fromDecimal = new BigInteger(999999999999999999m);

// From byte array (little-endian, two's complement)
byte[] bytes = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 };
var fromBytes = new BigInteger(bytes); // 256

Console.WriteLine($"From int: {fromInt}");
Console.WriteLine($"From bytes: {fromBytes}");
```

### Example 2: Basic Arithmetic Operations

```csharp
using System;
using System.Numerics;

var a = new BigInteger(1000000);
var b = new BigInteger(500000);

// Addition
var sum = a + b; // 1,500,000

// Subtraction
var difference = a - b; // 500,000

// Multiplication
var product = a * b; // 500,000,000,000

// Division
var quotient = a / b; // 2

// Modulus
var remainder = a % 300000; // 100,000

Console.WriteLine($"Sum: {sum}");
Console.WriteLine($"Product: {product}");
Console.WriteLine($"Quotient: {quotient}");
```

### Example 3: Ethereum Wei Calculations (Token Amounts)

```csharp
using System;
using System.Numerics;

// ERC-20 tokens typically use 18 decimals (like ETH)
// 1 token = 10^18 base units

// Create 1 ETH worth of wei
var oneEth = BigInteger.Pow(10, 18);
Console.WriteLine($"1 ETH in wei: {oneEth}");

// Calculate 2.5 ETH in wei
var twoPointFiveEth = BigInteger.Multiply(25, BigInteger.Pow(10, 17));
Console.WriteLine($"2.5 ETH in wei: {twoPointFiveEth}");

// Convert wei back to ETH (with precision loss)
var weiAmount = new BigInteger("1234567890123456789"); // ~1.23 ETH
var ethAmount = weiAmount / oneEth;
var weiRemainder = weiAmount % oneEth;

Console.WriteLine($"{weiAmount} wei = {ethAmount}.{weiRemainder} ETH");

// For precise decimal conversion, use decimal arithmetic
decimal preciseEth = (decimal)weiAmount / (decimal)oneEth;
Console.WriteLine($"Precise: {preciseEth} ETH");
```

### Example 4: Working with Byte Arrays (Ethereum Compatibility)

```csharp
using System;
using System.Numerics;

// Ethereum uses big-endian byte arrays, but BigInteger uses little-endian
// Always be aware of byte order!

// Create BigInteger from little-endian bytes
byte[] littleEndian = new byte[] { 0x01, 0x00, 0x00, 0x00 }; // 1
var value1 = new BigInteger(littleEndian);
Console.WriteLine($"Little-endian [0x01, 0x00, 0x00, 0x00]: {value1}"); // 1

// Simulate big-endian (reverse for BigInteger)
byte[] bigEndian = new byte[] { 0x00, 0x00, 0x00, 0x01 };
Array.Reverse(bigEndian); // Convert to little-endian
var value2 = new BigInteger(bigEndian);
Console.WriteLine($"Big-endian [0x00, 0x00, 0x00, 0x01]: {value2}"); // 1

// Get bytes back (little-endian)
byte[] result = value1.ToByteArray();
Console.WriteLine($"ToByteArray: {BitConverter.ToString(result)}");
```

### Example 5: Comparison Operations

```csharp
using System;
using System.Numerics;

var a = new BigInteger(1000);
var b = new BigInteger(2000);
var c = new BigInteger(1000);

// Equality
bool equal = (a == c); // true
bool notEqual = (a != b); // true

// Comparison
bool lessThan = (a < b); // true
bool greaterThan = (a > b); // false
bool lessOrEqual = (a <= c); // true

// CompareTo method
int comparison = a.CompareTo(b); // -1 (a < b)

// Static comparison
if (BigInteger.Compare(a, b) < 0)
{
    Console.WriteLine("a is less than b");
}

// Zero comparison
var zero = BigInteger.Zero;
bool isZero = (a == BigInteger.Zero); // false
```

### Example 6: Power and Modular Exponentiation

```csharp
using System;
using System.Numerics;

// Simple power: 2^10
var power = BigInteger.Pow(2, 10); // 1024
Console.WriteLine($"2^10 = {power}");

// Large power: 10^50
var hugePower = BigInteger.Pow(10, 50);
Console.WriteLine($"10^50 = {hugePower}");

// Modular exponentiation (crucial for cryptography)
// (base^exponent) mod modulus
var baseValue = new BigInteger(5);
var exponent = new BigInteger(100);
var modulus = new BigInteger(13);

var modPow = BigInteger.ModPow(baseValue, exponent, modulus);
Console.WriteLine($"(5^100) mod 13 = {modPow}");

// This is MUCH faster than: (BigInteger.Pow(baseValue, 100) % modulus)
// because it uses efficient modular reduction during computation
```

### Example 7: Greatest Common Divisor (GCD)

```csharp
using System;
using System.Numerics;

// Find GCD of two numbers
var a = new BigInteger(48);
var b = new BigInteger(18);

var gcd = BigInteger.GreatestCommonDivisor(a, b);
Console.WriteLine($"GCD(48, 18) = {gcd}"); // 6

// Large numbers
var large1 = BigInteger.Pow(2, 100) - 1;
var large2 = BigInteger.Pow(2, 50);

var largeGcd = BigInteger.GreatestCommonDivisor(large1, large2);
Console.WriteLine($"GCD of large numbers: {largeGcd}");

// Check if numbers are coprime (GCD = 1)
var coprime1 = new BigInteger(17);
var coprime2 = new BigInteger(19);
bool areCoprime = BigInteger.GreatestCommonDivisor(coprime1, coprime2) == BigInteger.One;
Console.WriteLine($"17 and 19 are coprime: {areCoprime}"); // true
```

### Example 8: String Parsing and Formatting

```csharp
using System;
using System.Numerics;
using System.Globalization;

// Parse from string (decimal)
var parsed1 = BigInteger.Parse("123456789012345678901234567890");
Console.WriteLine($"Parsed: {parsed1}");

// Parse hexadecimal
var parsedHex = BigInteger.Parse("DEADBEEF", NumberStyles.HexNumber);
Console.WriteLine($"Hex parsed: {parsedHex}");

// Format as hexadecimal
string hexString = parsedHex.ToString("X");
Console.WriteLine($"Formatted as hex: 0x{hexString}");

// Format with thousand separators
var large = BigInteger.Pow(10, 15);
string formatted = large.ToString("N0"); // Uses current culture
Console.WriteLine($"Formatted: {formatted}");

// TryParse for safe parsing
BigInteger result;
bool success = BigInteger.TryParse("999999999999999999999999", out result);
if (success)
{
    Console.WriteLine($"Successfully parsed: {result}");
}
```

### Example 9: Ethereum Gas and Transaction Calculations

```csharp
using System;
using System.Numerics;

// Gas price in gwei (1 gwei = 10^9 wei)
var gasPriceGwei = new BigInteger(50); // 50 gwei
var gweiToWei = BigInteger.Pow(10, 9);
var gasPriceWei = gasPriceGwei * gweiToWei;

// Gas used for transaction
var gasUsed = new BigInteger(21000); // Standard ETH transfer

// Calculate transaction cost in wei
var transactionCostWei = gasPriceWei * gasUsed;

// Convert to ETH
var weiToEth = BigInteger.Pow(10, 18);
decimal transactionCostEth = (decimal)transactionCostWei / (decimal)weiToEth;

Console.WriteLine($"Gas price: {gasPriceGwei} gwei");
Console.WriteLine($"Gas used: {gasUsed}");
Console.WriteLine($"Transaction cost: {transactionCostWei} wei");
Console.WriteLine($"Transaction cost: {transactionCostEth:F6} ETH");

// Calculate maximum transaction cost for EIP-1559
var maxPriorityFeePerGas = new BigInteger(2) * gweiToWei; // 2 gwei
var maxFeePerGas = new BigInteger(100) * gweiToWei; // 100 gwei
var gasLimit = new BigInteger(300000);

var maxCost = maxFeePerGas * gasLimit;
decimal maxCostEth = (decimal)maxCost / (decimal)weiToEth;

Console.WriteLine($"Maximum possible cost: {maxCostEth:F6} ETH");
```

## API Reference

### Constructors

```csharp
public BigInteger(int value);
public BigInteger(uint value);
public BigInteger(long value);
public BigInteger(ulong value);
public BigInteger(float value);   // Truncates decimal part
public BigInteger(double value);  // Truncates decimal part
public BigInteger(decimal value); // Truncates decimal part
public BigInteger(byte[] value);  // Little-endian, two's complement
```

### Static Properties

```csharp
public static BigInteger Zero { get; }        // 0
public static BigInteger One { get; }         // 1
public static BigInteger MinusOne { get; }    // -1
```

### Arithmetic Operations

```csharp
public static BigInteger operator +(BigInteger left, BigInteger right);
public static BigInteger operator -(BigInteger left, BigInteger right);
public static BigInteger operator *(BigInteger left, BigInteger right);
public static BigInteger operator /(BigInteger left, BigInteger right);
public static BigInteger operator %(BigInteger left, BigInteger right);
public static BigInteger operator -(BigInteger value); // Negate
public static BigInteger operator ++(BigInteger value);
public static BigInteger operator --(BigInteger value);
```

### Comparison Operations

```csharp
public static bool operator ==(BigInteger left, BigInteger right);
public static bool operator !=(BigInteger left, BigInteger right);
public static bool operator <(BigInteger left, BigInteger right);
public static bool operator <=(BigInteger left, BigInteger right);
public static bool operator >(BigInteger left, BigInteger right);
public static bool operator >=(BigInteger left, BigInteger right);

public int CompareTo(BigInteger other);
public static int Compare(BigInteger left, BigInteger right);
```

### Mathematical Methods

```csharp
public static BigInteger Abs(BigInteger value);
public static BigInteger Pow(BigInteger value, int exponent);
public static BigInteger ModPow(BigInteger value, BigInteger exponent, BigInteger modulus);
public static BigInteger GreatestCommonDivisor(BigInteger left, BigInteger right);
public static BigInteger Divide(BigInteger dividend, BigInteger divisor);
public static BigInteger DivRem(BigInteger dividend, BigInteger divisor, out BigInteger remainder);
```

### Conversion Methods

```csharp
public byte[] ToByteArray(); // Returns little-endian
public override string ToString();
public string ToString(string format);
public string ToString(IFormatProvider provider);

public static BigInteger Parse(string value);
public static BigInteger Parse(string value, NumberStyles style);
public static bool TryParse(string value, out BigInteger result);
```

### Properties

```csharp
public bool IsZero { get; }
public bool IsOne { get; }
public bool IsEven { get; }
public int Sign { get; } // Returns -1, 0, or 1
```

## Related Packages

### Used By (Consumers)
- All Nethereum packages when building for .NET 3.5
- **Nethereum.Util** - Unit conversions (wei/gwei/ether)
- **Nethereum.ABI** - ABI encoding/decoding of uint256 values
- **Nethereum.RLP** - RLP encoding of large integers
- **Nethereum.Signer** - Cryptographic signature operations

### Dependencies
- None

### Modern Alternatives
- **System.Numerics.BigInteger** (.NET 4.0+) - Built-in, preferred for modern apps

## Important Notes

### Platform Targeting

This package is **only compiled and used for .NET 3.5**:

```csharp
#if DOTNET35
// BigInteger implementation here
#endif
```

For all other platforms (.NET 4.0+, .NET Core, .NET 5+), Nethereum uses the built-in `System.Numerics.BigInteger`.

### Byte Order (Endianness)

BigInteger uses **little-endian** byte arrays, but Ethereum typically uses **big-endian**:

```csharp
// WRONG - Don't use Ethereum bytes directly
byte[] ethereumBytes = ...; // Big-endian from Ethereum
var wrong = new BigInteger(ethereumBytes); // Incorrect!

// CORRECT - Reverse for Ethereum compatibility
byte[] ethereumBytes = ...;
Array.Reverse(ethereumBytes); // Convert to little-endian
var correct = new BigInteger(ethereumBytes);
```

**Better:** Use Nethereum.Util helper methods which handle endianness correctly.

### Division Truncates

Division always truncates toward zero:

```csharp
var a = new BigInteger(7);
var b = new BigInteger(3);
var result = a / b; // 2, not 2.33...

// Use decimal for precision
decimal precise = (decimal)a / (decimal)b; // 2.333...
```

### Performance Considerations

1. **Modular Exponentiation**: Always use `ModPow` instead of computing power then modulus:

```csharp
// SLOW
var slow = BigInteger.Pow(value, exp) % mod;

// FAST
var fast = BigInteger.ModPow(value, exp, mod);
```

2. **Reuse Values**: Cache frequently used powers:

```csharp
// GOOD - Cache the divisor
private static readonly BigInteger WeiPerEther = BigInteger.Pow(10, 18);

public decimal WeiToEther(BigInteger wei)
{
    return (decimal)wei / (decimal)WeiPerEther;
}
```

### Memory Usage

BigInteger values are heap-allocated. For high-performance scenarios with small values, consider using primitive types when possible:

```csharp
// For values that fit in long, use long
long small = 1000000; // Faster than BigInteger

// Only use BigInteger when necessary
BigInteger large = BigInteger.Parse("99999999999999999999999999");
```

### Thread Safety

BigInteger is **immutable** and therefore **thread-safe**. All operations return new instances.

## Additional Resources

- [.NET BigInteger Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger)
- [Ethereum Unit Conversions](https://ethereum.org/en/developers/docs/intro-to-ether/#denominations)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [Wei, Gwei, Ether Converter](https://eth-converter.com/)
- [.NET Foundation GitHub](https://github.com/dotnet/runtime)

## Migration Guide

### From .NET 3.5 to Modern .NET

When upgrading from .NET 3.5 to .NET 4.0+:

1. Remove `Nethereum.BigInteger.N351` reference (no longer needed)
2. Code using `System.Numerics.BigInteger` continues to work unchanged
3. Built-in BigInteger provides better performance

No code changes required - namespace and API are identical!
