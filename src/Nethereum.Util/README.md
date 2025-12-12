# Nethereum.Util

Core utilities for Ethereum development including Keccak-256 hashing, address checksum validation, and wei/ether unit conversions.

## Overview

Nethereum.Util provides essential utility functions for Ethereum development. It includes the Keccak-256 (SHA-3) hashing implementation used throughout Ethereum, EIP-55 mixed-case checksum address handling, and comprehensive unit conversion between wei, gwei, ether, and other denominations.

### Key Features

- **Keccak-256 Hashing (SHA-3)**: Ethereum's primary cryptographic hash function
- **Address Utilities**: EIP-55 checksum address creation and validation
- **Unit Conversion**: Convert between wei, gwei, ether, and 20+ Ethereum unit denominations
- **BigDecimal Support**: High-precision decimal arithmetic for large value conversions
- **Address Comparison**: Case-insensitive address equality with checksum awareness
- **Transaction Utilities**: Helper methods for transaction handling

## Installation

```bash
dotnet add package Nethereum.Util
```

### Dependencies

- **Nethereum.Hex** - Hexadecimal encoding/decoding
- **Nethereum.RLP** - RLP (Recursive Length Prefix) encoding

## Key Concepts

### Keccak-256 (SHA-3)

Ethereum uses Keccak-256, the original SHA-3 submission before FIPS 202 standardization. This hash function is used for:
- Generating Ethereum addresses from public keys
- Creating function selectors for smart contracts
- Computing storage slot keys
- Generating message hashes for signing

### EIP-55 Checksum Addresses

EIP-55 defines a backward-compatible mixed-case checksum format for Ethereum addresses:
- Uses Keccak-256 hash of the lowercase address
- Capitalizes hex digits at positions where the hash has a value ≥ 8
- Provides error detection without changing address format
- Example: `0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed`

### Unit Conversion

Ethereum uses wei as the smallest unit (10^-18 ether). Common denominations:

| Unit | Wei Value | Common Usage |
|------|-----------|--------------|
| Wei | 1 | Smart contract precision |
| Gwei | 10^9 | Gas prices |
| Ether | 10^18 | User-facing amounts |

The `UnitConversion` class handles conversions between 20+ denominations including wei, kwei, mwei, gwei, szabo, finney, ether, kether, and more.

## Quick Start

```csharp
using Nethereum.Util;
using System.Numerics;

// Keccak-256 hashing
var hasher = new Sha3Keccack();
string hash = hasher.CalculateHash("Hello, Ethereum!");
// Result: hex string of the keccak-256 hash

// Create checksum address
var addressUtil = new AddressUtil();
string checksumAddress = addressUtil.ConvertToChecksumAddress(
    "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"
);
// Result: "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"

// Convert ether to wei
var conversion = new UnitConversion();
BigInteger weiAmount = conversion.ToWei(1.5m, UnitConversion.EthUnit.Ether);
// Result: 1500000000000000000 wei

// Convert wei to ether
decimal etherAmount = conversion.FromWei(
    BigInteger.Parse("1500000000000000000"),
    UnitConversion.EthUnit.Ether
);
// Result: 1.5 ether
```

## Usage Examples

### Example 1: Keccak-256 Hashing

```csharp
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

var keccak = new Sha3Keccack();

// Hash a string (UTF-8 encoded)
string textHash = keccak.CalculateHash("Ethereum");
Console.WriteLine($"Hash: 0x{textHash}");

// Hash byte array
byte[] data = new byte[] { 0x01, 0x02, 0x03 };
byte[] hashBytes = keccak.CalculateHash(data);
string hashHex = hashBytes.ToHex();

// Hash from hex values (useful for contract data)
string combinedHash = keccak.CalculateHashFromHex(
    "0x1234",
    "0x5678",
    "0xabcd"
);
// Hashes the concatenation: 0x12345678abcd

// Get hash as bytes (for further processing)
byte[] hashAsBytes = keccak.CalculateHashAsBytes("test");
```

### Example 2: EIP-55 Checksum Addresses

```csharp
using Nethereum.Util;

var addressUtil = new AddressUtil();

// Create checksum address from lowercase
string address1 = "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";
string checksum1 = addressUtil.ConvertToChecksumAddress(address1);
// Result: "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"

// Create checksum address from uppercase
string address2 = "0xFB6916095CA1DF60BB79CE92CE3EA74C37C5D359";
string checksum2 = addressUtil.ConvertToChecksumAddress(address2);
// Result: "0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359"

// Verify if address is properly checksummed
bool isValid = addressUtil.IsChecksumAddress(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
); // true

bool isInvalid = addressUtil.IsChecksumAddress(
    "0x5aaeb6053F3E94C9b9A09f33669435E7Ef1BeAed" // wrong case
); // false
```

### Example 3: Address Validation and Comparison

```csharp
using Nethereum.Util;

var addressUtil = new AddressUtil();

// Validate address format (40 hex chars with 0x prefix)
bool valid = addressUtil.IsValidEthereumAddressHexFormat(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
); // true

bool invalid = addressUtil.IsValidEthereumAddressHexFormat(
    "0x5aAeb6053F3E" // too short
); // false

// Check address length
bool correctLength = addressUtil.IsValidAddressLength(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
); // true

// Compare addresses (case-insensitive)
string addr1 = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed";
string addr2 = "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed";
bool same = addressUtil.AreAddressesTheSame(addr1, addr2); // true

// Using extension method
bool same2 = addr1.IsTheSameAddress(addr2); // true
```

### Example 4: Empty Address Handling

```csharp
using Nethereum.Util;

var addressUtil = new AddressUtil();

// Check if address is empty
bool isEmpty1 = addressUtil.IsAnEmptyAddress(null); // true
bool isEmpty2 = addressUtil.IsAnEmptyAddress(""); // true
bool isEmpty3 = addressUtil.IsAnEmptyAddress("0x0"); // true
bool isEmpty4 = addressUtil.IsAnEmptyAddress(" "); // true

// Get address or empty constant
string addr = null;
string result = addressUtil.AddressValueOrEmpty(addr);
// Result: "0x0" (AddressUtil.AddressEmptyAsHex)

// Check if not empty
bool notEmpty = addressUtil.IsNotAnEmptyAddress(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
); // true

// Using extension methods
bool isEmpty = "0x0".IsAnEmptyAddress(); // true
bool notEmpty2 = "0x1234...".IsNotAnEmptyAddress(); // true

// Zero address constant
string zeroAddress = AddressUtil.ZERO_ADDRESS;
// "0x0000000000000000000000000000000000000000"
```

### Example 5: Wei/Ether Conversions

From: [Nethereum Playground Example 1014](https://playground.nethereum.com/csharp/id/1014)

```csharp
using Nethereum.Util;
using System.Numerics;

var conversion = new UnitConversion();

// Convert ether to wei
BigInteger wei1 = conversion.ToWei(1m, UnitConversion.EthUnit.Ether);
// Result: 1000000000000000000

BigInteger wei2 = conversion.ToWei(0.001m, UnitConversion.EthUnit.Ether);
// Result: 1000000000000000 (0.001 ETH)

// Convert gwei to wei (for gas prices)
BigInteger gasWei = conversion.ToWei(
    20,
    UnitConversion.EthUnit.Gwei
);
// Result: 20000000000 (20 gwei)

// Convert wei to ether
decimal ether = conversion.FromWei(
    BigInteger.Parse("1000000000000000000"),
    UnitConversion.EthUnit.Ether
);
// Result: 1.0

// Convert wei to gwei
decimal gwei = conversion.FromWei(
    BigInteger.Parse("20000000000"),
    UnitConversion.EthUnit.Gwei
);
// Result: 20.0

// Using static accessor
BigInteger wei3 = UnitConversion.Convert.ToWei(
    2.5m,
    UnitConversion.EthUnit.Ether
);
// Result: 2500000000000000000
```

### Example 6: High-Precision Conversions with BigDecimal

```csharp
using Nethereum.Util;
using System.Numerics;

var conversion = new UnitConversion();

// Standard decimal has precision limits (29 digits)
// For very large or precise values, use BigDecimal

// Convert wei to BigDecimal (no precision loss)
BigDecimal precise = conversion.FromWeiToBigDecimal(
    BigInteger.Parse("1111111111111111111111111111111"),
    UnitConversion.EthUnit.Ether
);
// Result: 1111111111111.111111111111111111 (exact)

// Convert with more than 29 digits
BigDecimal veryLarge = conversion.FromWeiToBigDecimal(
    BigInteger.Parse("1111111111111111111111111111111111111111111111111"),
    UnitConversion.EthUnit.Tether
);
// Maintains full precision

// Convert BigDecimal back to wei
BigDecimal amount = new BigDecimal(1) / new BigDecimal(3);
BigInteger weiFromBigDecimal = conversion.ToWei(
    amount,
    UnitConversion.EthUnit.Ether
);
// Result: 333333333333333333 (1/3 ETH in wei)
```

### Example 7: Working with Different Units

```csharp
using Nethereum.Util;
using System.Numerics;

var conversion = new UnitConversion();

// Finney (milliether) - 0.001 ETH
BigInteger finneyWei = conversion.ToWei(
    100,
    UnitConversion.EthUnit.Finney
);
// Result: 100000000000000000 (0.1 ETH)

// Szabo (microether) - 0.000001 ETH
BigInteger szaboWei = conversion.ToWei(
    1000000,
    UnitConversion.EthUnit.Szabo
);
// Result: 1000000000000 (0.001 ETH)

// Kether (grand) - 1000 ETH
BigInteger ketherWei = conversion.ToWei(
    1,
    UnitConversion.EthUnit.Kether
);
// Result: 1000000000000000000000

// Convert using custom decimal places
BigInteger customWei = conversion.ToWei(
    100m,
    6 // USDC has 6 decimal places
);
// Result: 100000000

// Convert from custom decimal places
decimal customAmount = conversion.FromWei(
    BigInteger.Parse("100000000"),
    6 // USDC decimals
);
// Result: 100.0
```

### Example 8: Unique Address Collections

```csharp
using Nethereum.Util;

// UniqueAddressList uses case-insensitive address comparison
var addresses = new UniqueAddressList();

addresses.Add("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
addresses.Add("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"); // Same address, different case

Console.WriteLine(addresses.Count); // 1 (duplicates ignored)

// Check if contains (case-insensitive)
bool contains = addresses.Contains(
    "0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"
); // true

// Custom equality comparer for address dictionaries
var addressComparer = new AddressEqualityComparer();
var dict = new Dictionary<string, int>(addressComparer);

dict["0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"] = 100;
dict["0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"] = 200; // Overwrites

Console.WriteLine(dict.Count); // 1
```

### Example 9: Address Conversion and Padding

```csharp
using Nethereum.Util;

var addressUtil = new AddressUtil();

// Convert short address to valid 20-byte address (pad with zeros)
string shortAddr = "0x1234";
string paddedAddr = addressUtil.ConvertToValid20ByteAddress(shortAddr);
// Result: "0x0000000000000000000000000000000000001234"

// Handle null addresses
string nullAddr = null;
string paddedNull = addressUtil.ConvertToValid20ByteAddress(nullAddr);
// Result: "0x0000000000000000000000000000000000000000"

// Convert byte array to checksum address
byte[] addressBytes = new byte[20] {
    0x5a, 0xAe, 0xb6, 0x05, 0x3F, 0x3E, 0x94, 0xC9,
    0xb9, 0xA0, 0x9f, 0x33, 0x66, 0x94, 0x35, 0xE7,
    0xEf, 0x1B, 0xeA, 0xed
};
string checksumFromBytes = addressUtil.ConvertToChecksumAddress(addressBytes);
// Result: "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"

// Handles addresses longer than 20 bytes (takes last 20)
byte[] longAddress = new byte[32]; // e.g., from uint256 in Solidity
// ... populate longAddress ...
string fromLong = addressUtil.ConvertToChecksumAddress(longAddress);
// Uses last 20 bytes
```

## API Reference

### Sha3Keccack

Keccak-256 hashing implementation.

```csharp
public class Sha3Keccack
{
    public static Sha3Keccack Current { get; }

    // Hash string (UTF-8)
    public string CalculateHash(string value);
    public byte[] CalculateHashAsBytes(string value);

    // Hash bytes
    public byte[] CalculateHash(byte[] value);

    // Hash concatenated hex values
    public string CalculateHashFromHex(params string[] hexValues);
}
```

### AddressUtil

Ethereum address utilities and validation.

```csharp
public class AddressUtil
{
    public static AddressUtil Current { get; }
    public const string AddressEmptyAsHex = "0x0";
    public const string ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";

    // Checksum address creation
    public string ConvertToChecksumAddress(string address);
    public string ConvertToChecksumAddress(byte[] address);

    // Validation
    public bool IsValidEthereumAddressHexFormat(string address);
    public bool IsValidAddressLength(string address);
    public bool IsChecksumAddress(string address);

    // Empty address checks
    public bool IsAnEmptyAddress(string address);
    public bool IsNotAnEmptyAddress(string address);
    public string AddressValueOrEmpty(string address);

    // Comparison
    public bool AreAddressesTheSame(string address1, string address2);
    public bool IsEmptyOrEqualsAddress(string address1, string candidate);

    // Conversion
    public string ConvertToValid20ByteAddress(string address);
}
```

### UnitConversion

Convert between wei and various Ethereum denominations.

```csharp
public class UnitConversion
{
    public static UnitConversion Convert { get; }

    public enum EthUnit
    {
        Wei, Kwei, Mwei, Gwei, Szabo, Finney, Ether,
        Kether, Mether, Gether, Tether, /* and more */
    }

    // To Wei
    public BigInteger ToWei(decimal amount, EthUnit fromUnit = EthUnit.Ether);
    public BigInteger ToWei(BigDecimal amount, EthUnit fromUnit = EthUnit.Ether);
    public BigInteger ToWei(decimal amount, int decimalPlacesFromUnit);
    public BigInteger ToWeiFromUnit(BigDecimal amount, BigInteger fromUnit);

    // From Wei (returns decimal, may lose precision > 29 digits)
    public decimal FromWei(BigInteger value, EthUnit toUnit = EthUnit.Ether);
    public decimal FromWei(BigInteger value, int decimalPlacesToUnit);
    public decimal FromWei(BigInteger value, BigInteger toUnit);

    // From Wei to BigDecimal (no precision loss)
    public BigDecimal FromWeiToBigDecimal(BigInteger value, EthUnit toUnit = EthUnit.Ether);
    public BigDecimal FromWeiToBigDecimal(BigInteger value, int decimalPlacesToUnit);
    public BigDecimal FromWeiToBigDecimal(BigInteger value, BigInteger toUnit);

    // Unit value helpers
    public BigInteger GetEthUnitValue(EthUnit ethUnit);
    public bool TryValidateUnitValue(BigInteger ethUnit);
}
```

### Extension Methods

#### AddressExtensions

```csharp
public static class AddressExtensions
{
    // Address validation
    public static bool IsValidEthereumAddressHexFormat(this string address);
    public static bool IsChecksumAddress(this string address);

    // Empty checks
    public static bool IsAnEmptyAddress(this string address);
    public static bool IsNotAnEmptyAddress(this string address);
    public static string AddressValueOrEmpty(this string address);

    // Comparison
    public static bool IsTheSameAddress(this string address1, string address2);

    // Conversion
    public static string ConvertToEthereumChecksumAddress(this string address);
    public static string ConvertToValid20ByteAddress(this string address);
}
```

### BigDecimal

High-precision decimal arithmetic for large value conversions.

```csharp
public class BigDecimal
{
    public BigDecimal(BigInteger mantissa, int exponent);

    public BigInteger Mantissa { get; }
    public int Exponent { get; }

    // Arithmetic operators
    public static BigDecimal operator +(BigDecimal left, BigDecimal right);
    public static BigDecimal operator -(BigDecimal left, BigDecimal right);
    public static BigDecimal operator *(BigDecimal left, BigDecimal right);
    public static BigDecimal operator /(BigDecimal left, BigDecimal right);

    // Conversions
    public static explicit operator decimal(BigDecimal value);
    public static implicit operator BigDecimal(decimal value);
    public BigInteger FloorToBigInteger();
}
```

### Additional Utilities

#### DateTimeHelper
```csharp
public static class DateTimeHelper
{
    public static DateTime UnixTimeStampToDateTime(ulong unixTimeStamp);
    public static ulong GetUnixTimeStampSeconds(DateTime dateTime);
}
```

#### ByteUtil
```csharp
public static class ByteUtil
{
    public static byte[] Merge(params byte[][] arrays);
    public static bool AreEqual(byte[] a, byte[] b);
}
```

## Related Packages

### Used By (Consumers)

Almost all Nethereum packages depend on Nethereum.Util:

- **Nethereum.Signer** - Uses Keccak for signing and address derivation
- **Nethereum.RPC** - Uses unit conversion for transaction values
- **Nethereum.Contracts** - Uses address validation and hashing
- **Nethereum.Accounts** - Uses address utilities and checksums
- **Nethereum.Web3** - Uses all utility functions throughout
- **Nethereum.ABI** - Uses Keccak for function selector generation

### Dependencies

- **Nethereum.Hex** - Hexadecimal encoding/decoding
- **Nethereum.RLP** - RLP encoding (for some utilities)

## Important Notes

### Decimal Precision Limits

When converting from wei to decimal using `FromWei()`, C# decimal type has a 29-digit limit. Values with more than 29 significant digits will be rounded:

```csharp
// This rounds the least significant digits
decimal rounded = conversion.FromWei(
    BigInteger.Parse("111111111111111111111111111111"), // 30 digits
    18
);
// Use FromWeiToBigDecimal() for exact precision
```

For values requiring more than 29 digits of precision, use `FromWeiToBigDecimal()` which returns `BigDecimal` instead.

### Keccak vs SHA3

Ethereum uses **Keccak-256**, which is different from the final NIST SHA-3 standard (FIPS 202). Do not use standard SHA-3 libraries for Ethereum - always use Nethereum's `Sha3Keccack` class.

### Address Comparison Performance

Address comparison is case-insensitive and uses string comparison (not BigInteger). This is optimized for performance while maintaining correctness:

```csharp
// Fast: string comparison
bool same = address1.IsTheSameAddress(address2);

// Both handle mixed-case addresses correctly
```

### Unit Validation

Unit values must be powers of 10. The library validates this:

```csharp
conversion.ToWeiFromUnit(100m, new BigInteger(10)); // OK
conversion.ToWeiFromUnit(100m, new BigInteger(7));  // Throws Exception
```

## Additional Resources

- [EIP-55: Mixed-case checksum address encoding](https://eips.ethereum.org/EIPS/eip-55)
- [Keccak-256 Specification](https://keccak.team/keccak.html)
- [Ethereum Units](https://ethereum.org/en/developers/docs/intro-to-ether/#denominations)
- [Nethereum Documentation](https://docs.nethereum.com)
