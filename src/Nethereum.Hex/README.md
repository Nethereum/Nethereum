# Nethereum.Hex

Hexadecimal encoding and decoding utilities for Ethereum-specific types including String, BigInteger, and byte arrays.

## Overview

Nethereum.Hex provides foundational hexadecimal conversion capabilities specifically designed for Ethereum development. It handles the encoding and decoding of values according to Ethereum's hex encoding standards, including proper 0x prefixing, compact representation (no leading zeros except for zero itself), and big-endian byte ordering.

### Key Features

- **HexBigInteger**: Type-safe wrapper for BigInteger with automatic hex encoding/decoding
- **Hex Byte Conversions**: Extension methods for converting between byte arrays and hex strings
- **Ethereum Standards Compliance**: Follows Ethereum JSON-RPC hex encoding specifications
- **JSON Serialization**: Built-in support for both Newtonsoft.Json and System.Text.Json
- **Cross-Platform**: Supports .NET Framework, .NET Core, .NET 5+, Unity, and Xamarin

## Installation

```bash
dotnet add package Nethereum.Hex
```

### Dependencies

- **Newtonsoft.Json** (JSON serialization)
- **System.Text.Json** (.NET 6.0+, optional for modern JSON serialization)

## Key Concepts

### HexBigInteger

`HexBigInteger` is a type-safe wrapper around `System.Numerics.BigInteger` that automatically handles hex encoding and decoding. It's commonly used for representing Ethereum quantities like wei amounts, block numbers, gas values, and nonces.

**Ethereum Hex Encoding Rules:**
- Prefix with `0x`
- Use most compact representation (no leading zeros)
- Exception: zero is represented as `0x0`
- Big-endian byte ordering

### Hex Byte Extensions

Extension methods for working with hex strings and byte arrays:
- `ToHex()` - Convert byte array to hex string
- `HexToByteArray()` - Convert hex string to byte array
- `EnsureHexPrefix()` - Add 0x prefix if missing
- `RemoveHexPrefix()` - Strip 0x prefix if present
- `IsHex()` - Validate hex string format

### Hex BigInteger Extensions

Extension methods for BigInteger hex conversion:
- `ToHex()` - Convert BigInteger to hex string
- `HexToBigInteger()` - Convert hex string to BigInteger
- Support for both little-endian and big-endian byte ordering

## Quick Start

```csharp
using Nethereum.Hex.HexTypes;
using System.Numerics;

// Create from BigInteger
var amount = new HexBigInteger(1000000000000000000); // 1 ETH in wei
Console.WriteLine(amount.HexValue); // "0xde0b6b3a7640000"

// Create from hex string
var blockNumber = new HexBigInteger("0x400");
Console.WriteLine(blockNumber.Value); // 1024

// Access both representations
var gas = new HexBigInteger(21000);
Console.WriteLine($"Decimal: {gas.Value}"); // 21000
Console.WriteLine($"Hex: {gas.HexValue}"); // "0x5208"
```

## Usage Examples

### Example 1: Working with Wei Amounts

```csharp
using Nethereum.Hex.HexTypes;
using System.Numerics;

// Encoding wei amounts for transactions
var oneEther = BigInteger.Parse("1000000000000000000");
var encoded = new HexBigInteger(oneEther);
Assert.Equal("0xde0b6b3a7640000", encoded.HexValue);

// Decoding wei amounts from JSON-RPC responses
var hexValue = "0x8ac7230489e80000";
var decoded = new HexBigInteger(hexValue);
Assert.Equal("10000000000000000000", decoded.Value.ToString()); // 10 ETH
```

### Example 2: Hex String Conversions

```csharp
using Nethereum.Hex.HexConvertors.Extensions;

// Convert byte array to hex
byte[] data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
string hex = data.ToHex(prefix: true);
// Result: "0x12345678"

// Convert hex to byte array
string hexString = "0xabcdef";
byte[] bytes = hexString.HexToByteArray();
// Result: [0xab, 0xcd, 0xef]

// Ensure proper formatting
string withoutPrefix = "ff00aa";
string formatted = withoutPrefix.EnsureHexPrefix();
// Result: "0xff00aa"

// Validate hex strings
bool isValid = "0x1234abcd".IsHex(); // true
bool isInvalid = "0xghij".IsHex(); // false
```

### Example 3: Compact Encoding (Ethereum Standard)

```csharp
using Nethereum.Hex.HexTypes;
using System.Numerics;

// Ethereum requires compact encoding (no leading zeros)
var value = new HexBigInteger(new BigInteger(1024));
Assert.Equal("0x400", value.HexValue); // NOT "0x0400"

// Zero is always represented as "0x0"
var zero = new HexBigInteger(new BigInteger(0));
Assert.Equal("0x0", zero.HexValue);

// Decoding handles both compact and padded formats
var compact = new HexBigInteger("0x400");
var padded = new HexBigInteger("0x0400");
Assert.Equal(compact.Value, padded.Value); // Both equal 1024
```

### Example 4: BigInteger to Hex Conversion with Extensions

```csharp
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

// Convert BigInteger to hex (big-endian, compact)
BigInteger value = 1000000;
string hex = value.ToHex(littleEndian: false, compact: true);
// Result: "0xf4240"

// Convert hex to BigInteger (big-endian)
string hexString = "0xde0b6b3a7640000";
BigInteger result = hexString.HexToBigInteger(isHexLittleEndian: false);
// Result: 1000000000000000000

// Convert to byte array with endianness control
byte[] bytes = value.ToByteArray(littleEndian: false);
```

### Example 5: Equality Comparisons

```csharp
using Nethereum.Hex.HexTypes;

// HexBigInteger supports value equality
var val1 = new HexBigInteger(100);
var val2 = new HexBigInteger(100);
Assert.True(val1 == val2);
Assert.True(val1.Equals(val2));

// Different values are not equal
var val3 = new HexBigInteger(101);
Assert.False(val1 == val3);

// Can compare values created from different sources
var fromInt = new HexBigInteger(256);
var fromHex = new HexBigInteger("0x100");
Assert.True(fromInt == fromHex); // Both represent 256
```

### Example 6: JSON Serialization

```csharp
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Numerics;

// Automatic JSON serialization with Newtonsoft.Json
public class Transaction
{
    public HexBigInteger Value { get; set; }
    public HexBigInteger GasPrice { get; set; }
}

var tx = new Transaction
{
    Value = new HexBigInteger(1000000000000000000),
    GasPrice = new HexBigInteger(20000000000)
};

string json = JsonConvert.SerializeObject(tx);
// Result: {"Value":"0xde0b6b3a7640000","GasPrice":"0x4a817c800"}

// Deserialization works automatically
var deserialized = JsonConvert.DeserializeObject<Transaction>(json);
Assert.Equal(1000000000000000000, deserialized.Value.Value);
```

## API Reference

### Core Types

#### HexBigInteger
Ethereum-compliant hex-encoded BigInteger wrapper.

```csharp
public class HexBigInteger : HexRPCType<BigInteger>
{
    public HexBigInteger(string hex);
    public HexBigInteger(BigInteger value);

    public BigInteger Value { get; set; }
    public string HexValue { get; set; }
}
```

### Extension Methods

#### HexByteConvertorExtensions

```csharp
public static class HexByteConvertorExtensions
{
    // Byte array conversions
    public static string ToHex(this byte[] value, bool prefix = false);
    public static byte[] HexToByteArray(this string value);
    public static string ToHexCompact(this byte[] value);

    // Prefix handling
    public static bool HasHexPrefix(this string value);
    public static string EnsureHexPrefix(this string value);
    public static string RemoveHexPrefix(this string value);

    // Validation
    public static bool IsHex(this string value);
    public static bool IsTheSameHex(this string first, string second);
}
```

#### HexBigIntegerConvertorExtensions

```csharp
public static class HexBigIntegerConvertorExtensions
{
    // BigInteger to hex
    public static string ToHex(this BigInteger value, bool littleEndian, bool compact = true);
    public static byte[] ToByteArray(this BigInteger value, bool littleEndian);

    // Hex to BigInteger
    public static BigInteger HexToBigInteger(this string hex, bool isHexLittleEndian);

    // HexBigInteger helpers
    public static BigInteger? GetValue(this HexBigInteger hexBigInteger);
}
```

## Related Packages

### Used By (Consumers)
Almost all Nethereum packages depend on Nethereum.Hex as it provides fundamental encoding:

- **Nethereum.ABI** - ABI encoding/decoding requires hex conversions
- **Nethereum.RPC** - JSON-RPC uses hex encoding for all numeric values
- **Nethereum.Util** - Utility functions build on hex primitives
- **Nethereum.Signer** - Signature components use hex encoding
- **Nethereum.Contracts** - Contract interactions require hex encoding
- **Nethereum.Web3** - Main facade uses hex types throughout

### Dependencies
- **Newtonsoft.Json** - JSON serialization support
- **System.Text.Json** (.NET 6.0+) - Modern JSON serialization
- **Nethereum.BigInteger.N351** (embedded) - BigInteger implementation for legacy frameworks

## Additional Resources

- [Ethereum JSON-RPC Specification](https://ethereum.org/en/developers/docs/apis/json-rpc/)
- [Ethereum Yellow Paper](https://ethereum.github.io/yellowpaper/paper.pdf)
- [Nethereum Documentation](https://docs.nethereum.com)
