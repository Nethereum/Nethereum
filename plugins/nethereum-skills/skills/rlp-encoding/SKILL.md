---
name: rlp-encoding
description: Encode and decode data using RLP (Recursive Length Prefix) with Nethereum. Use when the user needs RLP encoding for transactions, state tries, or custom binary serialization in Ethereum.
user-invocable: true
---

# RLP Encoding

NuGet: `Nethereum.RLP`

Source: `tests/Nethereum.ABI.UnitTests/RlpDocExampleTests.cs`

**Important:** Alias the encoder to avoid ambiguity with the namespace:
```csharp
using RlpEncoder = Nethereum.RLP.RLP;
```

## Required Usings

```csharp
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using RlpEncoder = Nethereum.RLP.RLP;
```

## Encode Strings

```csharp
// Empty string encodes to 0x80
byte[] emptyBytes = "".ToBytesForRLPEncoding();
byte[] encodedEmpty = RlpEncoder.EncodeElement(emptyBytes);
// encodedEmpty.ToHex() == "80"

// Single character (< 0x80) encodes as itself
byte[] singleBytes = "d".ToBytesForRLPEncoding();
byte[] encodedSingle = RlpEncoder.EncodeElement(singleBytes);
// encodedSingle.ToHex() == "64"

// Short string: length prefix + data
byte[] dogBytes = "dog".ToBytesForRLPEncoding();
byte[] encodedDog = RlpEncoder.EncodeElement(dogBytes);
// encodedDog.ToHex() == "83646f67"

// Decode back
IRLPElement decoded = RlpEncoder.Decode(encodedDog);
string decodedStr = decoded.RLPData.ToStringFromRLPDecoded();
// decodedStr == "dog"
```

## Encode Integers

```csharp
// Zero encodes to 0x80 (same as empty)
byte[] zeroBytes = 0.ToBytesForRLPEncoding();
byte[] encodedZero = RlpEncoder.EncodeElement(zeroBytes);
// encodedZero.ToHex() == "80"

// Small int (< 128) encodes as single byte
byte[] smallBytes = 15.ToBytesForRLPEncoding();
byte[] encodedSmall = RlpEncoder.EncodeElement(smallBytes);
// encodedSmall.ToHex() == "0f"

// Larger int gets length prefix
byte[] mediumBytes = 1000.ToBytesForRLPEncoding();
byte[] encodedMedium = RlpEncoder.EncodeElement(mediumBytes);
// encodedMedium.ToHex() == "8203e8"

// Decode back
IRLPElement decoded = RlpEncoder.Decode(encodedMedium);
int decodedInt = decoded.RLPData.ToIntFromRLPDecoded();
// decodedInt == 1000
```

## Encode BigInteger

```csharp
byte[] hexBytes = "100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
    .HexToByteArray();
BigInteger bigInt = hexBytes.ToBigIntegerFromRLPDecoded();

byte[] bigIntBytes = bigInt.ToBytesForRLPEncoding();
byte[] encoded = RlpEncoder.EncodeElement(bigIntBytes);
// encoded.ToHex() == "a0100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"

// Decode back
IRLPElement decoded = RlpEncoder.Decode(encoded);
BigInteger decodedBigInt = decoded.RLPData.ToBigIntegerFromRLPDecoded();
// decodedBigInt == bigInt
```

## Encode Lists

```csharp
// Empty list encodes to 0xc0
byte[][] emptyList = new byte[0][];
byte[] encodedEmpty = RlpEncoder.EncodeList(emptyList);
// encodedEmpty.ToHex() == "c0"

// List of strings: encode each element, then encode as list
string[] strings = { "cat", "dog" };
byte[][] stringBytes = strings.ToBytesForRLPEncoding();

byte[][] encodedElements = new byte[stringBytes.Length][];
for (int i = 0; i < stringBytes.Length; i++)
{
    encodedElements[i] = RlpEncoder.EncodeElement(stringBytes[i]);
}

byte[] encodedList = RlpEncoder.EncodeList(encodedElements);
// encodedList.ToHex() == "c88363617483646f67"

// Decode list
RLPCollection decodedList = RlpEncoder.Decode(encodedList) as RLPCollection;
string first = decodedList[0].RLPData.ToStringFromRLPDecoded();  // "cat"
string second = decodedList[1].RLPData.ToStringFromRLPDecoded(); // "dog"
```

## Encode Raw Bytes

```csharp
byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
byte[] encoded = RlpEncoder.EncodeElement(data);

IRLPElement decoded = RlpEncoder.Decode(encoded);
byte[] decodedBytes = decoded.RLPData;
// decodedBytes == data

// Empty bytes
byte[] encodedEmpty = RlpEncoder.EncodeElement(new byte[0]);
// encodedEmpty.ToHex() == "80"
```

## Extension Method Reference

| Method                        | Input        | Output       |
|-------------------------------|--------------|--------------|
| `ToBytesForRLPEncoding()`     | string/int/BigInteger | byte[] |
| `RlpEncoder.EncodeElement()`  | byte[]       | byte[] (RLP) |
| `RlpEncoder.EncodeList()`     | byte[][]     | byte[] (RLP) |
| `RlpEncoder.Decode()`         | byte[]       | IRLPElement  |
| `.ToStringFromRLPDecoded()`   | byte[]       | string       |
| `.ToIntFromRLPDecoded()`      | byte[]       | int          |
| `.ToBigIntegerFromRLPDecoded()` | byte[]     | BigInteger   |

## RLP Encoding Rules Summary

1. Single byte < 0x80: encoded as itself
2. String 0-55 bytes: `[0x80 + length]` + data
3. String > 55 bytes: `[0xb7 + length-of-length]` + length + data
4. List total payload 0-55 bytes: `[0xc0 + length]` + items
5. List total payload > 55 bytes: `[0xf7 + length-of-length]` + length + items
