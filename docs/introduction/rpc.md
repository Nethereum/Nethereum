#RPC

The communication uses the JSON RPC interface, the full documentation can be found [in the Ethereum wiki](https://github.com/ethereum/wiki/wiki/JSON-RPC)

##RPC Data Types

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

##DTOs

There are various RPC Data transfer objects for Block, Transaction, FilterInput, and BlockParameter, to name a few.

The BlockParameter can be either for the latest, earliest, pending or for a given block number, this is defaulted to latest on all the requests that uses it. 

Note: If using Web3, changing the Default Block Parameter, will cascade to all the Eth transaction commands, GetCode, GetStorageAt, GetBalance

```csharp
var ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
return await ethGetBlockTransactionCountByNumber.SendRequestAsync(BlockParameter.CreateLatest());
```

or if using Web3

```csharp
var web3 = new Web3();
web3.Eth.Blocks.GetBlockTransactionCountByNumber.SendRequestAsync(BlockParameter.CreateLatest());
```

##Interceptors
Sometimes it might be a need to provide a different implementation to the RPC methods. For example, we might want to sign offline all the transaction requests, or might want to route to another provider.

The clients provide an OverridingRequestInterceptor which can be used in these scenarios.

This is an example of mock implementation of an interceptor

```csharp
 public class OverridingInterceptorMock:RequestInterceptor
    {
        public override async Task<RpcResponse> InterceptSendRequestAsync(Func<RpcRequest, string, Task<RpcResponse>> interceptedSendRequestAsync, RpcRequest request, string route = null)
        {
            if (request.Method == "eth_accounts")
            {
                return BuildResponse(new string[] { "hello", "hello2"}, route);
            }

            if (request.Method == "eth_getCode")
            {
                return BuildResponse("the code", route);
            }
            return await interceptedSendRequestAsync(request, route);
        }

        public RpcResponse BuildResponse(object results, string route = null)
        {
            var token = JToken.FromObject(results);
            return new RpcResponse(route, token);
        }

       ...
    }

```

And the usage on a unit test
```csharp
    [Fact]
    public async void ShouldInterceptNoParamsRequest()
    {
        var client = new RpcClient(new Uri("http://localhost:8545/"));
        
        client.OverridingRequestInterceptor = new OverridingInterceptorMock();
        var ethAccounts = new EthAccounts(client);
        var accounts = await ethAccounts.SendRequestAsync();
        Assert.True(accounts.Length == 2);
        Assert.Equal(accounts[0],"hello");
    }

    [Fact]
    public async void ShouldInterceptParamsRequest()
    {
        var client = new RpcClient(new Uri("http://localhost:8545/"));

        client.OverridingRequestInterceptor = new OverridingInterceptorMock();
        var ethGetCode = new EthGetCode(client);
        var code = await ethGetCode.SendRequestAsync("address");
        Assert.Equal(code, "the code");
    }
```
