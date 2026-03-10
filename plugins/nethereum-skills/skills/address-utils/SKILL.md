---
name: address-utils
description: Validate and format Ethereum addresses with Nethereum. Use when the user needs address validation, EIP-55 checksum, address comparison, zero address handling, UniqueAddressList, or address padding.
user-invocable: true
---

# Address Utilities with Nethereum

NuGet: `Nethereum.Util`

Source: `UtilDocExampleTests`

## Required Usings

```csharp
using System.Collections.Generic;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
```

## EIP-55 Checksum Addresses

Mixed-case encoding that provides error detection without changing the address value.

```csharp
var addressUtil = AddressUtil.Current;

var checksum = addressUtil.ConvertToChecksumAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");
// "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"

// Works from all-uppercase too
var fromUpper = addressUtil.ConvertToChecksumAddress("0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED");
// Same result: "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"

// Verify checksum
addressUtil.IsChecksumAddress("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"); // true
addressUtil.IsChecksumAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"); // false

// Convert from raw bytes
var bytes = "5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed".HexToByteArray();
var checksumFromBytes = addressUtil.ConvertToChecksumAddress(bytes);
```

## Validate Address Format

```csharp
var addressUtil = AddressUtil.Current;

// Full validation (hex format, length, prefix)
addressUtil.IsValidEthereumAddressHexFormat("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"); // true
addressUtil.IsValidEthereumAddressHexFormat("not-an-address"); // false

// Length-only check (40 hex chars + 0x prefix)
addressUtil.IsValidAddressLength("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"); // true
addressUtil.IsValidAddressLength("0x1234"); // false
```

## Compare Addresses (Case-Insensitive)

Ethereum addresses are case-insensitive for equality. Always use these methods instead of `==`.

```csharp
var addressUtil = AddressUtil.Current;

// Static method
addressUtil.AreAddressesTheSame(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed",
    "0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"); // true

// Extension method
"0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"
    .IsTheSameAddress("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed"); // true
```

## Empty / Zero Address Handling

```csharp
var addressUtil = AddressUtil.Current;

// Check for empty/null/zero
addressUtil.IsAnEmptyAddress(null);  // true
addressUtil.IsAnEmptyAddress("");    // true
addressUtil.IsAnEmptyAddress("0x0"); // true

// Extension methods
"0x0".IsAnEmptyAddress();           // true
"0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed".IsNotAnEmptyAddress(); // true

// Constants
AddressUtil.ZERO_ADDRESS;      // "0x0000000000000000000000000000000000000000"
AddressUtil.AddressEmptyAsHex; // Same zero address

// Safe accessor (returns zero address for null)
string nullAddr = null;
addressUtil.AddressValueOrEmpty(nullAddr);
// "0x0000000000000000000000000000000000000000"
```

## UniqueAddressList and AddressEqualityComparer

Case-insensitive address collections that prevent duplicates regardless of casing.

```csharp
// UniqueAddressList deduplicates automatically
var uniqueList = new UniqueAddressList();
uniqueList.Add("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed");
uniqueList.Add("0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED");
uniqueList.Add("0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed");
// uniqueList.Count == 1

// AddressEqualityComparer for Dictionary/HashSet
var comparer = new AddressEqualityComparer();
comparer.Equals(
    "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed",
    "0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"); // true

// Use in Dictionary for case-insensitive key lookup
var dict = new Dictionary<string, int>(comparer);
dict["0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"] = 100;
dict["0x5AAEB6053F3E94C9B9A09F33669435E7EF1BEAED"]; // 100
```

## Pad Short Addresses (ConvertToValid20ByteAddress)

Left-pads a short hex value to a full 20-byte address. Useful when dealing with contract return values or CREATE2 results.

```csharp
var addressUtil = AddressUtil.Current;

addressUtil.ConvertToValid20ByteAddress("0x1234");
// "0x0000000000000000000000000000000000001234"

addressUtil.ConvertToValid20ByteAddress(null);
// "0x0000000000000000000000000000000000000000"
```

## Keccak-256 Hashing

Used internally for checksum calculation. Also available for general-purpose hashing.

```csharp
var keccak = Sha3Keccack.Current;

// Hash a string
keccak.CalculateHash("hello");
// "1c8aff950685c2ed4bc3174f3472287b56d9517b9c948127319a09a7a36deac8"

// Hash raw bytes
var bytes = new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f };
keccak.CalculateHash(bytes).ToHex();

// Hash from hex input
keccak.CalculateHashFromHex("0x68656c6c6f");

// Get result as byte array (32 bytes)
byte[] hashBytes = keccak.CalculateHashAsBytes("hello");
```
