###RPC

The communication uses the JSON RPC interface, the full documentation can be found [in the Ethereum wiki](https://github.com/ethereum/wiki/wiki/JSON-RPC)

###RPC Data Types

The simplest datatypes to communicate with Ethereum are Numeric and Data.

Numeric: A HexBigInteger data type has been created to allow the simple conversion of the input and output of numbers from the RPC.
This types also handles the conversion to and from Big Endian, together with specific usages for Eth "0x0"

```csharp
    var number = new HexBigInteger(21000);
    var anotherNumber = new HexBigInteger("0x5208");
    var hex = number.HexValue; //0x5208
    BigInteger val = anotherNumber; //2100
```

Data: The transaction, blocks, uncles, addresses hashes are treated as strings to simplify usage.

Hex strings: Similar to HexBigInteger some strings need to be encoded and decoded Hex. HexString has been created to handle the conversion using UTF8 encoding, from and to Hex.

####DTOs

There are varoious RPC Data transfer objects for Block, Transaction, FilterInput, and BlockParameter, to name a few.

The BlockParameter can be either for the latest, earliest, pending or for a given blocknumber, this is defaulted to latest on the requests that uses it. 

If using Web3, changing the Default Block Parameter, will cascade to all the commands.

