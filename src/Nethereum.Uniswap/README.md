# Nethereum.Uniswap V2, V3, V4, Universal Router and Permit 2

Uniswap V2, V3, V4, Universal Router and Permit 2 integration with Nethereum.

---

# Uniswap V4 Integration with Nethereum

Comprehensive examples for interacting with Uniswap V4 on Ethereum mainnet and testnets using Nethereum.

## Setup
```csharp
var url = "https://base-sepolia.drpc.org";
var privateKey = "0xYOUR_PRIVATE_KEY";
var web3 = new Web3.Web3(new Account(privateKey), url);

// Access Uniswap V4 services - defaults to Mainnet
var uniswap = web3.UniswapV4();

// Or specify a different network
var uniswap = web3.UniswapV4(UniswapV4Addresses.BaseSepolia);

// Access pool manager through service hierarchy
var poolManager = uniswap.Pools.Manager;
var usdc = "0x91D1e0b9f6975655A381c79fd6f1D118D1c5b958";

var pool = new PoolKey()
{
    Currency0 = AddressUtil.ZERO_ADDRESS,
    Currency1 = usdc,
    Fee = 500,
    TickSpacing = 10,
    Hooks = "0x24F7c9ea6B5be5227caAeB61366b56052386eae4"
};
```

## Quoting Prices

```csharp
var uniswap = web3.UniswapV4(UniswapV4Addresses.BaseSepolia);

// Access StateView and Quoter through service hierarchy
var stateViewService = uniswap.Positions.StateView;
var v4Quoter = uniswap.Pricing.Quoter;

var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(new List<PoolKey> { pool }, AddressUtil.ZERO_ADDRESS);
var amountIn = Web3.Web3.Convert.ToWei(0.001);

var quoteExactParams = new QuoteExactParams()
{
    Path = pathKeys,
    ExactAmount = amountIn,
    ExactCurrency = AddressUtil.ZERO_ADDRESS
};

var quote = await v4Quoter.QuoteExactInputQueryAsync(quoteExactParams);
var quoteAmount = Web3.Web3.Convert.FromWei(quote.AmountOut, 6); // USDC has 6 decimals
```

## Executing Swaps with Universal Router

```csharp
var uniswap = web3.UniswapV4();
var universalRouter = uniswap.UniversalRouter;
var v4ActionBuilder = uniswap.GetUniversalRouterV4ActionsBuilder();

// Add swap action
var swapExactIn = new SwapExactIn()
{
    CurrencyIn = eth,
    AmountIn = amountIn,
    AmountOutMinimum = quote.AmountOut * 95 / 100, // 5% slippage
    Path = pathKeys.MapToActionV4()
};
v4ActionBuilder.AddCommand(swapExactIn);

// Settle input currency (ETH)
var settleAll = new SettleAll()
{
    Currency = eth,
    Amount = amountIn
};
v4ActionBuilder.AddCommand(settleAll);

// Take output currency (USDC)
var takeAll = new TakeAll()
{
    Currency = usdc,
    MinAmount = 0
};
v4ActionBuilder.AddCommand(takeAll);

var routerBuilder = new UniversalRouterBuilder();
routerBuilder.AddCommand(v4ActionBuilder.GetV4SwapCommand());

var executeFunction = routerBuilder.GetExecuteFunction(amountIn);
var receipt = await universalRouter.ExecuteRequestAndWaitForReceiptAsync(executeFunction);
```

## Price Calculations and Math Utilities

### Calculate Pool Prices from SqrtPriceX96
```csharp
var uniswap = web3.UniswapV4();
var prices = uniswap.Pricing.PriceCalculator.CalculatePricesFromSqrtPriceX96(
    sqrtPriceX96,
    token0Decimals: 18,
    token1Decimals: 6);

var priceToken0InToken1 = prices.Item1; // Price of token0 in terms of token1
var priceToken1InToken0 = prices.Item2; // Price of token1 in terms of token0
```

### Tick Math - Convert Between Ticks and Prices
```csharp
var uniswap = web3.UniswapV4();

// Get sqrt price from tick
var sqrtPriceX96 = uniswap.Math.Tick.GetSqrtRatioAtTick(tick);

// Get tick from sqrt price
var tick = uniswap.Math.Tick.GetTickAtSqrtRatio(sqrtPriceX96);
```

### Liquidity Math - Calculate Token Amounts
```csharp
var uniswap = web3.UniswapV4();
var amounts = uniswap.Positions.LiquidityCalculator.GetAmountsForLiquidityByTicks(
    sqrtPriceX96,
    tickLower,
    tickUpper,
    liquidity);

var amount0 = amounts.Item1;
var amount1 = amounts.Item2;
```

## Slippage and Price Impact Protection

### Calculate Slippage-Protected Amounts
```csharp
var uniswap = web3.UniswapV4();

// For exact input swaps (you know input, calculate min output)
var tolerance = new BigDecimal(0.5m); // 0.5% slippage

var minAmountOut = uniswap.Pricing.SlippageCalculator.CalculateMinimumAmountOut(
    expectedAmountOut,
    tolerance);

// For exact output swaps (you know output, calculate max input)
var maxAmountIn = uniswap.Pricing.SlippageCalculator.CalculateMaximumAmountIn(
    expectedAmountIn,
    tolerance);
```

### Calculate and Monitor Price Impact
```csharp
var uniswap = web3.UniswapV4();
var calculator = uniswap.Pricing.PriceImpactCalculator;

// Calculate price impact percentage
var priceImpact = calculator.CalculatePriceImpact(
    inputAmount,
    outputAmount,
    midPrice);

// Classify impact level
var impactLevel = calculator.ClassifyPriceImpact(priceImpact);
// Returns: Low (<1%), Medium (1-3%), High (3-5%), Critical (>5%)

// Get user-friendly warning message
var warning = calculator.GetPriceImpactWarning(impactLevel);
```

## Pool Discovery and Caching

### Access Pool Cache Service
```csharp
var uniswap = web3.UniswapV4();
var poolCache = uniswap.Pools.Cache;
```

### Fetch and Cache Pool Data
```csharp
// Get or fetch pool (uses cache if available)
var pool = await poolCache.GetOrFetchPoolAsync(
    currency0: eth,
    currency1: usdc,
    fee: 500,
    tickSpacing: 10);

// Access pool information
var poolId = pool.PoolId;
var sqrtPriceX96 = pool.SqrtPriceX96;
var currentTick = pool.Tick;
var exists = pool.Exists;
```

### Event-Based Pool Discovery
```csharp
var uniswap = web3.UniswapV4();

// Find all pools containing a specific token using Initialize events
var latestBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
var startBlock = latestBlock.Value - 100_000; // look back ~100k blocks

var pools = await uniswap.Pools.Cache.FindPoolsForTokenAsync(
    token: usdc,
    fromBlockNumber: startBlock,
    toBlockNumber: latestBlock.Value);

// Pools are automatically cached for future use
foreach (var pool in pools)
{
    Console.WriteLine($"Pool: {pool.Currency0}/{pool.Currency1}, Fee: {pool.Fee}");
}
```

### Manage Cache
```csharp
var uniswap = web3.UniswapV4();

// Refresh specific pool
var updatedPool = await uniswap.Pools.Cache.RefreshPoolAsync(poolId);

// Get all cached pools
var allPools = await uniswap.Pools.Cache.GetAllCachedPoolsAsync();

// Clear cache
await uniswap.Pools.Cache.ClearCacheAsync();
```

## Position Management

### Creating a New Position
```csharp
var uniswap = web3.UniswapV4();
var positionManager = uniswap.Positions.Manager;

var poolKey = new PoolKey()
{
    Currency0 = AddressUtil.ZERO_ADDRESS,
    Currency1 = usdc,
    Fee = 500,
    TickSpacing = 10,
    Hooks = AddressUtil.ZERO_ADDRESS
};

var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

actionsBuilder.AddCommand(new MintPosition()
{
    PoolKey = poolKey,
    TickLower = -600,
    TickUpper = 600,
    Liquidity = Web3.Web3.Convert.ToWei(0.01m),
    Amount0Max = Web3.Web3.Convert.ToWei(0.1m),
    Amount1Max = Web3.Web3.Convert.ToWei(300, UnitConversion.EthUnit.Mwei),
    Recipient = account,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new SettlePair() { Currency0 = eth, Currency1 = usdc });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
        AmountToSend = Web3.Web3.Convert.ToWei(0.1m)
    });

var tokenId = V4PositionReceiptHelper.GetMintedTokenId(receipt, UniswapAddresses.MainnetPositionManagerV4);
```

### Querying Position Information
```csharp
// Get position liquidity
var liquidity = await positionManager.GetPositionLiquidityQueryAsync(tokenId);

// Get pool key and position info
var positionInfo = await positionManager.GetPoolAndPositionInfoQueryAsync(tokenId);
Console.WriteLine($"Pool: {positionInfo.PoolKey.Currency0}/{positionInfo.PoolKey.Currency1}");
Console.WriteLine($"Fee: {positionInfo.PoolKey.Fee}");

// Decode position details
var uniswap = web3.UniswapV4();
var positionInfoBytes = await positionManager.PositionInfoQueryAsync(tokenId);
var decodedInfo = uniswap.Positions.PositionInfoDecoder.DecodePositionInfo(positionInfoBytes);
Console.WriteLine($"Range: {decodedInfo.TickLower} to {decodedInfo.TickUpper}");

// Get position owner
var owner = await positionManager.OwnerOfQueryAsync(tokenId);
```

### Increasing Liquidity
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

actionsBuilder.AddCommand(new IncreaseLiquidity()
{
    TokenId = tokenId,
    Liquidity = Web3.Web3.Convert.ToWei(0.005m),
    Amount0Max = Web3.Web3.Convert.ToWei(0.05m),
    Amount1Max = Web3.Web3.Convert.ToWei(150, UnitConversion.EthUnit.Mwei),
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new SettlePair() { Currency0 = eth, Currency1 = usdc });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
        AmountToSend = Web3.Web3.Convert.ToWei(0.05m)
    });
```

### Decreasing Liquidity
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = tokenId,
    Liquidity = Web3.Web3.Convert.ToWei(0.005m),
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new TakePair() { Currency0 = eth, Currency1 = usdc, Recipient = account });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60
    });
```

### Atomic Position Rebalancing
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

// Close old position
actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = oldTokenId,
    Liquidity = oldLiquidity,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

// Open new position with different range
actionsBuilder.AddCommand(new MintPosition()
{
    PoolKey = poolKey,
    TickLower = -1200,
    TickUpper = 1200,
    Liquidity = Web3.Web3.Convert.ToWei(0.01m),
    Amount0Max = Web3.Web3.Convert.ToWei(0.1m),
    Amount1Max = Web3.Web3.Convert.ToWei(300, UnitConversion.EthUnit.Mwei),
    Recipient = account,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new CloseCurrency() { Currency = eth });
actionsBuilder.AddCommand(new CloseCurrency() { Currency = usdc });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
        AmountToSend = Web3.Web3.Convert.ToWei(0.15m)
    });
```

### Burning a Position
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = tokenId,
    Liquidity = totalLiquidity,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new BurnPosition()
{
    TokenId = tokenId,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new TakePair() { Currency0 = eth, Currency1 = usdc, Recipient = account });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60
    });
```

### Collecting Fees
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

// DecreaseLiquidity with liquidity=0 collects fees without removing liquidity
actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = tokenId,
    Liquidity = 0,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new TakePair() { Currency0 = eth, Currency1 = usdc, Recipient = account });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60
    });
```

### Batch Fee Collection from Multiple Positions
```csharp
var uniswap = web3.UniswapV4();
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

// Collect fees from position 1
actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = tokenId1,
    Liquidity = 0,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

// Collect fees from position 2
actionsBuilder.AddCommand(new DecreaseLiquidity()
{
    TokenId = tokenId2,
    Liquidity = 0,
    Amount0Min = 0,
    Amount1Min = 0,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new TakePair() { Currency0 = eth, Currency1 = usdc, Recipient = account });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60
    });
```

### Calculating Position Value
```csharp
var uniswap = web3.UniswapV4();
var valueCalculator = uniswap.Positions.PositionValueCalculator;

var positionValue = await valueCalculator.GetPositionValueAsync(
    tokenId,
    token0Decimals: 18,
    token1Decimals: 6);

Console.WriteLine($"Amount0: {Web3.Web3.Convert.FromWei(positionValue.Amount0, 18)}");
Console.WriteLine($"Amount1: {Web3.Web3.Convert.FromWei(positionValue.Amount1, 6)}");
Console.WriteLine($"Total value in Token0: {positionValue.ValueInToken0}");
Console.WriteLine($"Total value in Token1: {positionValue.ValueInToken1}");
```

## Finding Best Swap Paths

### Find Best Direct Path
```csharp
var uniswap = web3.UniswapV4();
var poolCache = uniswap.Pools.Cache;
var pathFinder = uniswap.Pricing.QuotePricePathFinder;

var cachedPools = await poolCache.GetAllCachedPoolsAsync();

var bestDirectPath = await pathFinder.FindBestDirectPathAsync(
    tokenIn: eth,
    tokenOut: usdc,
    amountIn: Web3.Web3.Convert.ToWei(1),
    candidatePools: cachedPools);

Console.WriteLine($"Best output: {Web3.Web3.Convert.FromWei(bestDirectPath.AmountOut, 6)} USDC");
Console.WriteLine($"Fee tier: {bestDirectPath.Fees[0]}");
```

### Find Best Path with Intermediate Tokens
```csharp
var weth = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
var dai = "0x6B175474E89094C44Da98b954EedeAC495271d0F";

var bestPath = await pathFinder.FindBestPathAsync(
    tokenIn: usdc,
    tokenOut: weth,
    amountIn: Web3.Web3.Convert.ToWei(1000, UnitConversion.EthUnit.Mwei),
    intermediateTokens: new[] { dai, eth },
    candidatePools: cachedPools);

Console.WriteLine($"Best path has {bestPath.Path.Count} hops");
Console.WriteLine($"Output amount: {Web3.Web3.Convert.FromWei(bestPath.AmountOut, 18)} WETH");
```
## Token Approval and Balance Validation

### Check and Manage Approvals
```csharp
var uniswap = web3.UniswapV4();

// Check if token is approved
var approvalStatus = await uniswap.Accounts.Approvals.CheckApprovalAsync(
    tokenAddress,
    owner,
    spender,
    requiredAmount);

if (!approvalStatus.IsApproved)
{
    // Approve token
    var txHash = await uniswap.Accounts.Approvals.ApproveAsync(
        tokenAddress,
        spender,
        AccountApprovalService.GetMaxApprovalAmount());
}

// Or check and approve in one call
await uniswap.Accounts.Approvals.CheckAndApproveIfNeededAsync(
    tokenAddress,
    owner,
    spender,
    requiredAmount);
```

### Validate Token Balances
```csharp
var uniswap = web3.UniswapV4();

// Check single token balance
var balanceResult = await uniswap.Accounts.Balances.ValidateBalanceAsync(
    tokenAddress,
    owner,
    requiredAmount);

if (!balanceResult.HasSufficientBalance)
{
    Console.WriteLine($"Insufficient balance. Need {balanceResult.Deficit} more tokens");
}

// Validate both tokens for liquidity operations
var hasBalance = await uniswap.Accounts.Balances.ValidateBalancesForLiquidityAsync(
    token0,
    token1,
    owner,
    amount0Required,
    amount1Required);
```

## Example Test Files

Complete working examples can be found in the test files:
- **V4SwapExamples.cs** - Swap operations and Universal Router usage
- **V4PriceAndQuoteExamples.cs** - Price calculations, slippage, and price impact
- **V4HelperExamples.cs** - Token approvals, balance validation, and price services
- **V4PoolCacheExamples.cs** - Pool discovery and caching strategies
- **V4PositionExamples.cs** - Position management, liquidity operations, and atomic rebalancing
```


# Uniswap V3 / Permit 2 / V2Quoter

## Setup
```csharp
var url = "https://ethereum-sepolia.rpc.subquery.network/public";
var privateKey = "0xYOUR_PRIVATE_KEY";
var account = new Account(privateKey);
var web3 = new Nethereum.Web3.Web3(account, url);

var factoryAddress = UniswapAddresses.SepoliaUniswapV3Factory;
var permit2 = UniswapAddresses.SepoliaPermit2;
var quoterAddress = UniswapAddresses.SepoliaQuoterV2;
var universalRouter = UniswapAddresses.SepoliaUniversalRouterV3;

var uni = "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984";
var weth = "0xfff9976782d46cc05630d1f6ebab18b2324d6b14";
```

## Quoting Prices
### Using Slot0 Price Calculator
```csharp
var calculator = new UniswapV3Slot0PriceCalculator(web3, factoryAddress);
var priceWethUni = await calculator.GetPoolPricesAsync(uni, weth, 500);
```

### Using Quoter V2
```csharp
var quoterService = new QuoterV2Service(web3, quoterAddress);
var weth9 = await quoterService.Weth9QueryAsync();

var amountIn = Web3.Web3.Convert.ToWei(0.001);
var abiEncoder = new Nethereum.ABI.ABIEncode();
var path = abiEncoder.GetABIEncodedPacked(
    new ABIValue("address", weth9),
    new ABIValue("uint24", 500),
    new ABIValue("address", uni));

var quote = await quoterService.QuoteExactInputQueryAsync(path, amountIn);
```

## Executing Swaps with Universal Router

### Prepare ERC20 Approval
```csharp
var weth9Service = web3.Eth.ERC20.GetContractService(weth9);
await weth9Service.ApproveRequestAndWaitForReceiptAsync(permit2, IntType.MAX_INT256_VALUE);
```

### Create and Sign Permit2
```csharp
var permit = new PermitSingle()
{
    Spender = universalRouter,
    SigDeadline = 2000000000,
    Details = new PermitDetails()
    {
        Amount = amountIn * 100000,
        Expiration = 0,
        Nonce = 0,
        Token = weth9
    }
};

var permitService = new Permit2Service(web3, permit2);
var signedPermit = await permitService.GetSinglePermitWithSignatureAsync(permit, new EthECKey(privateKey));
```

### Build and Execute Swap
```csharp
var universalRouterService = new UniversalRouterService(web3, universalRouter);
var planner = new UniversalRouterBuilder();

planner.AddCommand(new WrapEthCommand
{
    Amount = amountIn,
    Recipient = account.Address
});

planner.AddCommand(new Permit2PermitCommand
{
    Permit = signedPermit.PermitRequest,
    Signature = signedPermit.GetSignatureBytes()
});

planner.AddCommand(new V3SwapExactInCommand
{
    AmountIn = amountIn,
    AmountOutMinimum = quote.AmountOut - 10000, // slippage
    Path = path,
    Recipient = account.Address,
    FundsFromPermit2OrUniversalRouter = true
});

var receipt = await universalRouterService.ExecuteRequestAndWaitForReceiptAsync(planner.GetExecuteFunction(amountIn));
```

### Checking Balances
```csharp
var balanceWethWei = await weth9Service.BalanceOfQueryAsync(account.Address);
var balanceInEth = Web3.Web3.Convert.FromWei(balanceWethWei);

var uniService = web3.Eth.ERC20.GetContractService(uni);
var balanceUniWei = await uniService.BalanceOfQueryAsync(account.Address);
var balanceInUni = Web3.Web3.Convert.FromWei(balanceUniWei);
```

### Error Handling
```csharp
catch (SmartContractCustomErrorRevertException e)
{
    var error = universalRouterService.FindCustomErrorException(e);
    if (error != null)
    {
        Debug.WriteLine(error.Message);
        universalRouterService.HandleCustomErrorException(e);
    }
    throw;
}
```

## Uniswap V2 ERC20 single path and multipath

To enable hardhat.

1. Go to the directory testchains\hardhat and run ```npm install```
2. Configure your fork alchemy api key and block number in your Test settings https://github.com/Nethereum/Nethereum.UniswapV2/blob/main/Nethereum.Uniswap.Testing/appsettings.test.json#L6
3. When you run your tests it will automatically launch hardhat and fork on the configured block number.

### Code example

```csharp
        [Fact]
        public async void ShouldBeAbleToGetThePairForDaiWeth()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var factoryAddress = "0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f";
            var factoryService = new UniswapV2FactoryService(web3, factoryAddress);
            var weth = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2";
            var dai = "0x6b175474e89094c44da98b954eedeac495271d0f";
            var pair = await factoryService.GetPairQueryAsync(weth, dai);
            Assert.True(pair.IsTheSameAddress("0xa478c2975ab1ea89e8196811f51a7b7ade33eb11"));
        }


        [Fact]
        public async Task ShouldBeAbleToSwapEthForDai()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var myAddress = web3.TransactionManager.Account.Address;
            var routerV2Address = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            var uniswapV2Router02Service = new UniswapV2Router02Service(web3, routerV2Address);
            var weth = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2";
            var dai = "0x6b175474e89094c44da98b954eedeac495271d0f";
            var serviceDAI = new StandardTokenEIP20.StandardTokenService(web3, dai);

            var path = new List<string> {weth, dai};
            var amountEth = Web3.Web3.Convert.ToWei(100); //10 Ether
            
            var amounts = await uniswapV2Router02Service.GetAmountsOutQueryAsync(amountEth, path);
            
            var deadline = DateTimeOffset.Now.AddMinutes(15).ToUnixTimeSeconds();
            
            var swapEthForExactTokens = new Contracts.UniswapV2Router02.ContractDefinition.SwapExactETHForTokensFunction()
            {
                AmountOutMin = amounts[1],
                Path = path,
                Deadline = deadline,
                To = myAddress,
                AmountToSend = amountEth
            };
           
            var balanceOriginal = await serviceDAI.BalanceOfQueryAsync(myAddress);


            var swapReceipt = await uniswapV2Router02Service.SwapExactETHForTokensRequestAndWaitForReceiptAsync(swapEthForExactTokens);
            var swapLog = swapReceipt.Logs.DecodeAllEvents<SwapEventDTO>();
            var transferLog = swapReceipt.Logs.DecodeAllEvents<TransferEventDTO>();

            var balanceNew = await serviceDAI.BalanceOfQueryAsync(myAddress);
            
            Assert.Equal(swapLog[0].Event.Amount0Out, balanceNew - balanceOriginal);

        }

        [Fact]
        public async Task ShouldBeAbleToSwapEthForDaiThenUSDC()
        {
            await ShouldBeAbleToSwapEthForDai(); //lets get some DAI


            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var myAddress = web3.TransactionManager.Account.Address;
            var routerV2Address = "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D";
            var uniswapV2Router02Service = new UniswapV2Router02Service(web3, routerV2Address);
            var usdc = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48";
            var dai = "0x6b175474e89094c44da98b954eedeac495271d0f";
            var serviceDAI = new StandardTokenEIP20.StandardTokenService(web3, dai);
            var serviceUSDC = new StandardTokenEIP20.StandardTokenService(web3, usdc);

            var path = new List<string> { dai, usdc };
            var amountDAI = Web3.Web3.Convert.ToWei(10000);  //DAI 18 dec

            var amounts = await uniswapV2Router02Service.GetAmountsOutQueryAsync(amountDAI, path);

            var deadline = DateTimeOffset.Now.AddMinutes(15).ToUnixTimeSeconds();

            var swapTokensForExactTokens = new Contracts.UniswapV2Router02.ContractDefinition.SwapExactTokensForTokensFunction()
            {
                AmountOutMin = amounts[1],
                Path = path,
                Deadline = deadline,
                To = myAddress,
                AmountIn = amountDAI
            };

            var balanceOriginal = await serviceUSDC.BalanceOfQueryAsync(myAddress);

            var approveReceipt = await serviceDAI.ApproveRequestAndWaitForReceiptAsync(routerV2Address, amountDAI);

            var swapReceipt = await uniswapV2Router02Service.SwapExactTokensForTokensRequestAndWaitForReceiptAsync(swapTokensForExactTokens);
            var swapLog = swapReceipt.Logs.DecodeAllEvents<SwapEventDTO>();
            var transferLog = swapReceipt.Logs.DecodeAllEvents<TransferEventDTO>();

            var balanceNew = await serviceUSDC.BalanceOfQueryAsync(myAddress);

            Assert.Equal(swapLog[0].Event.Amount1Out, balanceNew - balanceOriginal);

        }

    }

```


