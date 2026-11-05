# Nethereum.RLP

Recursive Length Prefix (RLP) encoding and decoding for Ethereum data serialization.

## Overview

Nethereum.RLP implements Ethereum's primary data serialization format. RLP encodes arbitrarily nested arrays of binary data into a compact byte representation. It is used throughout Ethereum for encoding transactions, blocks, state tries, and network messages.

### Key Features

- **Encode Elements**: Encode single items (bytes, strings, integers) to RLP format
- **Encode Lists**: Encode collections and nested structures to RLP format
- **Decode RLP**: Deserialize RLP-encoded data back to elements or collections, rejecting non-canonical encodings and bounding nesting depth and element count
- **Stream RLP**: `RlpReader` walks an RLP structure field by field without building a tree
- **Type Conversions**: Extension methods for encoding/decoding int, BigInteger, and string types
- **Ethereum Compatibility**: Full compliance with Ethereum's RLP specification

## Installation

```bash
dotnet add package Nethereum.RLP
```

### Dependencies

- **Nethereum.Hex** - Hexadecimal encoding/decoding

## Key Concepts

### What is RLP?

RLP (Recursive Length Prefix) is a space-efficient encoding method for arbitrarily nested arrays of binary data. It is the main encoding method used to serialize objects in Ethereum.

**Key Principles:**
- RLP only encodes structure (nested arrays)
- Atomic data types (integers, strings) are encoded as byte arrays
- Integers are represented in big-endian binary form
- Zero-valued bytes are trimmed from the front of integers

### RLP Encoding Rules

#### Single Bytes (0x00 - 0x7f)
For a single byte whose value is in the [0x00, 0x7f] range, that byte is its own RLP encoding.

```csharp
// Character 'd' (0x64) encodes as itself
RLP.EncodeElement(new byte[] { 0x64 }); // Result: [0x64]
```

#### Short Strings (0-55 bytes)
If a string is 0-55 bytes long, the RLP encoding consists of a single byte with value 0x80 plus the length of the string, followed by the string.

```csharp
// "dog" = 3 bytes
// Encoded as: 0x83 (0x80 + 3) + "dog" bytes
// Result: 0x83646f67
```

#### Long Strings (>55 bytes)
If a string is more than 55 bytes long, the RLP encoding consists of:
- Single byte with value 0xb7 plus the length of the length
- The length of the string
- The string itself

```csharp
// 56-byte string encodes with 0xb8 prefix
// 0xb8 (0xb7 + 1) + 0x38 (length = 56) + string bytes
```

#### Short Lists (0-55 bytes total payload)
Lists with total payload 0-55 bytes use a single byte with value 0xc0 plus the length, followed by concatenated RLP encodings of items.

```csharp
// ["cat", "dog"]
// Encoded: 0xc8 + RLP("cat") + RLP("dog")
```

#### Long Lists (>55 bytes total payload)
Lists with payload >55 bytes use 0xf7 plus length-of-length encoding, similar to long strings.

### RLP Elements

- **RLPItem**: Represents a single encoded value (byte array)
- **RLPCollection**: Represents a list of RLP elements (can contain RLPItems or nested RLPCollections)

## Quick Start

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// Encode a string
string text = "dog";
byte[] textBytes = text.ToBytesForRLPEncoding();
byte[] encoded = RLP.EncodeElement(textBytes);
// Result: 0x83646f67

// Encode an integer
int number = 1000;
byte[] numberBytes = number.ToBytesForRLPEncoding();
byte[] encodedNum = RLP.EncodeElement(numberBytes);
// Result: 0x8203e8

// Encode a list
string[] items = { "cat", "dog" };
byte[][] itemBytes = items.ToBytesForRLPEncoding();
byte[][] encodedItems = new byte[itemBytes.Length][];
for (int i = 0; i < itemBytes.Length; i++)
{
    encodedItems[i] = RLP.EncodeElement(itemBytes[i]);
}
byte[] encodedList = RLP.EncodeList(encodedItems);
// Result: 0xc88363617483646f67

// Decode RLP
IRLPElement decoded = RLP.Decode(encoded);
string decodedText = decoded.RLPData.ToStringFromRLPDecoded();
// Result: "dog"
```

## Usage Examples

### Example 1: Encoding Strings

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// Empty string
string empty = "";
byte[] emptyBytes = empty.ToBytesForRLPEncoding();
byte[] encoded = RLP.EncodeElement(emptyBytes);
// Result: 0x80

// Single character
string single = "d";
byte[] singleBytes = single.ToBytesForRLPEncoding();
byte[] encodedSingle = RLP.EncodeElement(singleBytes);
// Result: 0x64 (single byte in range 0x00-0x7f encodes as itself)

// Short string
string dog = "dog";
byte[] dogBytes = dog.ToBytesForRLPEncoding();
byte[] encodedDog = RLP.EncodeElement(dogBytes);
Assert.Equal("83646f67", encodedDog.ToHex());

// Decode back
IRLPElement decoded = RLP.Decode(encodedDog);
string decodedStr = decoded.RLPData.ToStringFromRLPDecoded();
Assert.Equal("dog", decodedStr);
```

### Example 2: Encoding Integers

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// Zero encodes as empty byte array
int zero = 0;
byte[] zeroBytes = zero.ToBytesForRLPEncoding();
byte[] encodedZero = RLP.EncodeElement(zeroBytes);
// Result: 0x80 (empty byte array)

// Small integer (< 128)
int small = 15;
byte[] smallBytes = small.ToBytesForRLPEncoding();
byte[] encodedSmall = RLP.EncodeElement(smallBytes);
// Result: 0x0f (single byte in range encodes as itself)

// Medium integer
int medium = 1000;
byte[] mediumBytes = medium.ToBytesForRLPEncoding();
byte[] encodedMedium = RLP.EncodeElement(mediumBytes);
Assert.Equal("8203e8", encodedMedium.ToHex());

// Decode back
IRLPElement decoded = RLP.Decode(encodedMedium);
int decodedInt = decoded.RLPData.ToIntFromRLPDecoded();
Assert.Equal(1000, decodedInt);
```

### Example 3: Encoding BigInteger

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;

// Large BigInteger
byte[] hexBytes = "100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
    .HexToByteArray();
BigInteger bigInt = hexBytes.ToBigIntegerFromRLPDecoded();

byte[] bigIntBytes = bigInt.ToBytesForRLPEncoding();
byte[] encoded = RLP.EncodeElement(bigIntBytes);
Assert.Equal(
    "a0100102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
    encoded.ToHex()
);

// Decode back
IRLPElement decoded = RLP.Decode(encoded);
BigInteger decodedBigInt = decoded.RLPData.ToBigIntegerFromRLPDecoded();
Assert.Equal(bigInt, decodedBigInt);
```

### Example 4: Encoding Lists

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// Empty list
byte[][] emptyList = new byte[0][];
byte[] encodedEmpty = RLP.EncodeList(emptyList);
Assert.Equal("c0", encodedEmpty.ToHex());

RLPCollection decodedEmpty = RLP.Decode(encodedEmpty) as RLPCollection;
Assert.Equal(0, decodedEmpty.Count);

// Short string list
string[] strings = { "cat", "dog" };
byte[][] stringBytes = strings.ToBytesForRLPEncoding();

// Encode each element
byte[][] encodedElements = new byte[stringBytes.Length][];
for (int i = 0; i < stringBytes.Length; i++)
{
    encodedElements[i] = RLP.EncodeElement(stringBytes[i]);
}

// Encode the list
byte[] encodedList = RLP.EncodeList(encodedElements);
Assert.Equal("c88363617483646f67", encodedList.ToHex());

// Decode back
RLPCollection decodedList = RLP.Decode(encodedList) as RLPCollection;
Assert.Equal("cat", decodedList[0].RLPData.ToStringFromRLPDecoded());
Assert.Equal("dog", decodedList[1].RLPData.ToStringFromRLPDecoded());
```

### Example 5: Encoding Long Strings

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// String longer than 55 bytes
string longString = "Lorem ipsum dolor sit amet, consectetur adipisicing elit"; // 56 bytes
byte[] longBytes = longString.ToBytesForRLPEncoding();
byte[] encoded = RLP.EncodeElement(longBytes);

// Result starts with 0xb8 (0xb7 + 1 for length-of-length)
// followed by 0x38 (length = 56 in hex)
// followed by the string bytes
Assert.Equal(
    "b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974",
    encoded.ToHex()
);

// Decode back
IRLPElement decoded = RLP.Decode(encoded);
string decodedStr = decoded.RLPData.ToStringFromRLPDecoded();
Assert.Equal(longString, decodedStr);
```

### Example 6: Mixed List with Long String

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// List containing short and long strings
string shortStr = "cat";
string longStr = "Lorem ipsum dolor sit amet, consectetur adipisicing elit";
string[] mixed = { shortStr, longStr };

byte[][] mixedBytes = mixed.ToBytesForRLPEncoding();

// Encode each element
byte[][] encodedElements = new byte[mixedBytes.Length][];
for (int i = 0; i < mixedBytes.Length; i++)
{
    encodedElements[i] = RLP.EncodeElement(mixedBytes[i]);
}

// Encode the list
byte[] encodedList = RLP.EncodeList(encodedElements);
Assert.Equal(
    "f83e83636174b8384c6f72656d20697073756d20646f6c6f722073697420616d65742c20636f6e7365637465747572206164697069736963696e6720656c6974",
    encodedList.ToHex()
);

// Decode back
RLPCollection decoded = RLP.Decode(encodedList) as RLPCollection;
Assert.Equal("cat", decoded[0].RLPData.ToStringFromRLPDecoded());
Assert.Equal(longStr, decoded[1].RLPData.ToStringFromRLPDecoded());
```

### Example 7: Multiple String List

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// List with three short strings
string[] animals = { "dog", "god", "cat" };
byte[][] animalBytes = animals.ToBytesForRLPEncoding();

byte[][] encodedElements = new byte[animalBytes.Length][];
for (int i = 0; i < animalBytes.Length; i++)
{
    encodedElements[i] = RLP.EncodeElement(animalBytes[i]);
}

byte[] encodedList = RLP.EncodeList(encodedElements);
Assert.Equal("cc83646f6783676f6483636174", encodedList.ToHex());

// Decode and verify
RLPCollection decoded = RLP.Decode(encodedList) as RLPCollection;
for (int i = 0; i < animals.Length; i++)
{
    Assert.Equal(animals[i], decoded[i].RLPData.ToStringFromRLPDecoded());
}
```

### Example 8: Working with Raw Bytes

```csharp
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

// Encode raw byte array
byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
byte[] encoded = RLP.EncodeElement(data);

// Decode
IRLPElement decoded = RLP.Decode(encoded);
byte[] decodedBytes = decoded.RLPData;

Assert.Equal(data, decodedBytes);

// Empty byte array
byte[] empty = new byte[0];
byte[] encodedEmpty = RLP.EncodeElement(empty);
Assert.Equal("80", encodedEmpty.ToHex());

IRLPElement decodedEmpty = RLP.Decode(encodedEmpty);
Assert.Null(decodedEmpty.RLPData); // Empty encodes/decodes as null
```

## API Reference

### RLP Class

Main class for encoding and decoding RLP data.

```csharp
public class RLP
{
    // Constants
    public const byte OFFSET_SHORT_LIST = 0xc0;
    public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
    public static readonly byte[] ZERO_BYTE_ARRAY = { 0 };

    // Encoding
    public static byte[] EncodeElement(byte[] srcData);
    public static byte[] EncodeList(params byte[][] elements);

    // Decoding
    public static IRLPElement Decode(byte[] msgData);
    public static IRLPElement Decode(byte[] msgData, int maxElements);
    public static IRLPElement DecodeCollection(byte[] msgData);
    public static IRLPElement DecodeCollection(byte[] msgData, int maxElements);

    // Limits for untrusted input
    public const int MaxDecodeDepth = 64;
    public const int MinUntrustedElementBudget = 1024;
    public static int UntrustedElementBudget(int inputLength);

    // Utilities
    public static int ByteArrayToInt(byte[] bytes);
}
```

`Decode` and `DecodeCollection` validate the whole structure before they build anything: a single byte below `0x80` carrying a string prefix, a long-form length under 56, a length with a leading zero, a length larger than the input and list nesting deeper than `MaxDecodeDepth` all throw `Exception("Invalid RLP ...")` with the cause as `InnerException`. The `maxElements` overloads count elements first and throw once the payload holds more, so a flood of one-byte elements is refused before any element is allocated; `UntrustedElementBudget(inputLength)` is the budget used for bytes read from a peer, `max(MinUntrustedElementBudget, inputLength * 3 / 4)`. `Decode` also refuses bytes after the top-level value, since it reads exactly one item; `DecodeCollection` reads a sequence of items and returns them as an `RLPCollection`. `RLPCollection.RLPData` of a nested list is a slice of the input copied on first access.

### IRLPElement Interface

Base interface for RLP elements.

```csharp
public interface IRLPElement
{
    byte[] RLPData { get; }
}
```

### RLPItem Class

Represents a single RLP-encoded value.

```csharp
public class RLPItem : IRLPElement
{
    public byte[] RLPData { get; }
}
```

### RLPCollection Class

Represents a list of RLP elements (can be nested).

```csharp
public class RLPCollection : List<IRLPElement>, IRLPElement
{
    public byte[] RLPData { get; set; }

    // List operations via base class
    public int Count { get; }
    public IRLPElement this[int index] { get; }
}
```

### Conversion Extension Methods

Extensions for converting types to/from RLP encoding.

```csharp
public static class ConvertorForRLPEncodingExtensions
{
    // Encoding (to bytes for RLP)
    public static byte[] ToBytesForRLPEncoding(this int number);
    public static byte[] ToBytesForRLPEncoding(this long number);
    public static byte[] ToBytesForRLPEncoding(this BigInteger bigInteger);
    public static byte[] ToBytesForRLPEncoding(this string str);
    public static byte[][] ToBytesForRLPEncoding(this string[] strings);

    // Decoding (from RLP bytes)
    public static int ToIntFromRLPDecoded(this byte[] bytes);
    public static long ToLongFromRLPDecoded(this byte[] bytes);
    public static BigInteger ToBigIntegerFromRLPDecoded(this byte[] bytes);
    public static string ToStringFromRLPDecoded(this byte[] bytes);

    public static long ToLongBitsFromRLPDecoded(this byte[] bytes);

    // Utilities
    public static byte[] TrimZeroBytes(this byte[] bytes);
}
```

`ToLongBitsFromRLPDecoded` reads the scalar as a `uint64` and returns the same 64 bits as a `long`, so a value in `[2^63, 2^64)` does not overflow; a scalar wider than 64 bits still throws.

### RlpReader

A forward-only reader over an RLP byte array for decoding input you do not trust. It shares the canonical header parser of `RLP.Decode`, allocates nothing for structure, and reads each field in the shape you expect, so a list where a string is expected, a leading-zero integer or a wrong-width fixed field throws `InvalidOperationException`.

```csharp
public enum RlpKind { Byte, String, List }

public readonly struct RlpListScope
{
    public int Start { get; }
    public int End { get; }
    public int Length { get; }
}

public readonly struct RlpSegment
{
    public RlpSegment(int offset, int length);
    public int Offset { get; }
    public int Length { get; }
}

public sealed class RlpReader
{
    public RlpReader(byte[] data);
    public RlpReader(byte[] data, int offset, int length);

    public int Position { get; }
    public bool IsAtEnd();
    public bool IsAtEnd(RlpListScope scope);
    public RlpKind PeekKind();
    public int PeekSize();

    public RlpListScope ReadListHeader();
    public void EnsureEnd(RlpListScope scope);
    public void EnsureEnd();
    public int CountItems(RlpListScope scope);

    public byte[] ReadBytes();
    public byte[] ReadFixed(int length);
    public ulong ReadUInt64();
    public BigInteger ReadBigInteger();
    public bool ReadBool();

    public void Skip();
    public RlpSegment ReadRawSegment();
    public byte[] ReadRaw();
    public byte[] ReadData();
}
```

```csharp
var encoded = RLP.EncodeList(
    RLP.EncodeElement("cat".ToBytesForRLPEncoding()),
    RLP.EncodeElement(1000.ToBytesForRLPEncoding()),
    RLP.EncodeElement(new byte[] { 0x01 }));

var reader = new RlpReader(encoded);
var list = reader.ReadListHeader();
var name = Encoding.UTF8.GetString(reader.ReadBytes());
var amount = reader.ReadUInt64();
var flag = reader.ReadBool();
reader.EnsureEnd(list);
reader.EnsureEnd();
```

Backed by `FoundationsRlpDocExampleTests.Given_AnEncodedList_When_ReadWithRlpReader_Then_EachFieldIsReadInOrderAndTheEndIsChecked`.

`ReadUInt64`, `ReadBigInteger` and `ReadBool` refuse a leading zero, an integer wider than the target and a boolean other than 0 or 1. `ReadFixed(length)` refuses any other length. `ReadListHeader` refuses nesting deeper than `RLP.MaxDecodeDepth`. `EnsureEnd(scope)` throws when the list holds more elements than were read and `EnsureEnd()` when bytes follow the top-level value. `ReadRaw` returns the encoded bytes of the next element, header included, and `ReadData` returns the payload of a string or the raw encoding of a list.

```csharp
var leadingZero = new RlpReader("820001".HexToByteArray());
Assert.Throws<InvalidOperationException>(() => leadingZero.ReadUInt64());

var fixedField = new RlpReader("8301020304".HexToByteArray());
Assert.Throws<InvalidOperationException>(() => fixedField.ReadFixed(32));
```

Backed by `FoundationsRlpDocExampleTests.Given_NonCanonicalOrWrongWidthFields_When_ReadWithRlpReader_Then_ItThrows`.

## Related Packages

### Used By (Consumers)

RLP is used throughout Nethereum for Ethereum data encoding:

- **Nethereum.Signer** - Transaction and message signing uses RLP encoding
- **Nethereum.Model** - Transaction and block models use RLP
- **Nethereum.Util** - Address derivation uses RLP encoding
- **Nethereum.Merkle.Patricia** - Patricia Merkle Trie uses RLP for nodes
- **Nethereum.RPC** - Some RPC methods work with RLP-encoded data

### Dependencies

- **Nethereum.Hex** - Hexadecimal encoding/decoding for displaying RLP data

## Important Notes

### Encoding Behavior

#### Empty Values
- Empty string/byte array encodes as `0x80`
- Decoded empty value returns `null`
- Zero integer encodes as `0x80` (empty byte array)

```csharp
var empty = RLP.EncodeElement(new byte[0]);
// Result: 0x80

var decoded = RLP.Decode(empty);
// Result: decoded.RLPData == null
```

#### Single Bytes
Single bytes in range 0x00-0x7f encode as themselves:

```csharp
// Character 'd' (0x64) encodes as itself
var encoded = RLP.EncodeElement(new byte[] { 0x64 });
// Result: [0x64]
```

#### Big-Endian Encoding
All integers are encoded in big-endian format with leading zeros trimmed:

```csharp
int value = 1000; // 0x000003e8
var bytes = value.ToBytesForRLPEncoding();
// Result: [0x03, 0xe8] (leading zeros removed)
```

### Threshold Values

The RLP specification uses specific threshold values:

- **56 bytes**: Threshold between short and long item encoding
  - 0-55 bytes: Use single-byte length prefix (0x80-0xb7 or 0xc0-0xf7)
  - 56+ bytes: Use length-of-length prefix (0xb8+ or 0xf8+)

### Decoding Collections

When decoding, check the element type:

```csharp
var decoded = RLP.Decode(encoded);

if (decoded is RLPCollection collection)
{
    // It's a list - iterate items
    foreach (var item in collection)
    {
        var value = item.RLPData;
    }
}
else
{
    // It's a single item
    var value = decoded.RLPData;
}
```

### Untrusted Input

`RLP.Decode` rejects non-canonical encodings and nesting deeper than `RLP.MaxDecodeDepth`, and the `maxElements` overloads cap the element count:

```csharp
var budget = RLP.UntrustedElementBudget(payload.Length);

Assert.Throws<Exception>(() => RLP.Decode(payload, 100));
var decoded = (RLPCollection)RLP.Decode(payload, budget);

Assert.Throws<Exception>(() => RLP.Decode("8100".HexToByteArray()));
Assert.Throws<Exception>(() => RLP.Decode("b80101".HexToByteArray()));
```

Backed by `FoundationsRlpDocExampleTests.Given_APayloadWithMoreElementsThanTheBudget_When_Decoded_Then_ItIsRejectedBeforeTheTreeIsBuilt` and `FoundationsRlpDocExampleTests.Given_NonCanonicalOrOverDeepRlp_When_Decoded_Then_AnExceptionIsThrown`.

### Null vs Empty

- Decoded empty items return `null` for RLPData
- Check for null before using decoded data:

```csharp
var decoded = RLP.Decode(encoded);
string str = decoded.RLPData?.ToStringFromRLPDecoded() ?? "";
```

## Common Use Cases

### Transaction Encoding

Ethereum transactions are RLP-encoded:

```csharp
// Transaction: [nonce, gasPrice, gasLimit, to, value, data, v, r, s]
var transaction = new List<byte[]>
{
    nonce.ToBytesForRLPEncoding(),
    gasPrice.ToBytesForRLPEncoding(),
    gasLimit.ToBytesForRLPEncoding(),
    to.HexToByteArray(),
    value.ToBytesForRLPEncoding(),
    data.HexToByteArray(),
    v.ToBytesForRLPEncoding(),
    r,
    s
};

var encodedElements = transaction.Select(RLP.EncodeElement).ToArray();
var rlpTransaction = RLP.EncodeList(encodedElements);
```

### Block Header Encoding

Block headers use RLP for hashing:

```csharp
// Block header fields encoded as RLP list
var headerFields = new List<byte[]>
{
    parentHash,
    unclesHash,
    beneficiary,
    stateRoot,
    transactionsRoot,
    receiptsRoot,
    logsBloom,
    difficulty.ToBytesForRLPEncoding(),
    number.ToBytesForRLPEncoding(),
    gasLimit.ToBytesForRLPEncoding(),
    gasUsed.ToBytesForRLPEncoding(),
    timestamp.ToBytesForRLPEncoding(),
    extraData,
    mixHash,
    nonce
};

var encodedHeader = RLP.EncodeList(
    headerFields.Select(RLP.EncodeElement).ToArray()
);
```

## Additional Resources

- [Ethereum RLP Specification](https://ethereum.org/en/developers/docs/data-structures-and-encoding/rlp/)
- [Ethereum Wiki - RLP](https://github.com/ethereum/wiki/wiki/RLP)
- [Ethereum Yellow Paper](https://ethereum.github.io/yellowpaper/paper.pdf) (Appendix B - RLP)
- [Nethereum Documentation](https://docs.nethereum.com)
