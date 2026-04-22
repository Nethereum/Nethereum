---
name: uniswap-swap
description: "Swap tokens on Uniswap V2/V3/V4 using Nethereum (.NET/C#). Use this skill whenever the user asks about token swaps, DEX trading, Uniswap integration, quoting prices, slippage protection, price impact, Universal Router, Permit2, or any DeFi swap operation with C# or .NET."
user-invocable: true
---

# Uniswap: Swap Tokens

NuGet: `Nethereum.Uniswap`

```bash
dotnet add package Nethereum.Uniswap
```

## Connect to Uniswap V4

The `UniswapV4()` extension on `Web3` exposes the full service hierarchy. It defaults to Mainnet addresses but accepts any network's addresses:

```csharp
var web3 = new Web3(new Account(privateKey), rpcUrl);
var uniswap = web3.UniswapV4();
// Or for a specific network:
var uniswap = web3.UniswapV4(UniswapV4Addresses.BaseSepolia);
```

The `uniswap` object provides: `Pools` (state, cache, discovery), `Pricing` (quoter, price calculator, slippage, price impact), `Positions` (liquidity management), `UniversalRouter` (swap execution), and `Math` (tick math).

## Quote a Price

Before swapping, simulate the trade with the quoter — this is a read-only call, no gas cost:

```csharp
var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(new List<PoolKey> { pool }, eth);
var amountIn = Web3.Convert.ToWei(0.001);

var quoteParams = new QuoteExactParams
{
    Path = pathKeys,
    ExactAmount = amountIn,
    ExactCurrency = eth
};

var quote = await uniswap.Pricing.Quoter.QuoteExactInputQueryAsync(quoteParams);
var quoteAmount = Web3.Convert.FromWei(quote.AmountOut, 6); // USDC = 6 decimals
```

The `V4PathEncoder` encodes the swap route. For single-hop, pass one pool key. For multi-hop (e.g., ETH→DAI→USDC), pass multiple pool keys.

## Execute a Swap

Follow these steps for a complete swap:

**Step 1: Validate balance**
```csharp
var balanceResult = await uniswap.Accounts.Balances.ValidateBalanceAsync(
    tokenAddress, owner, amountIn);
if (!balanceResult.HasSufficientBalance)
    throw new Exception($"Need {balanceResult.Deficit} more tokens");
```

**Step 2: Check and approve token spending (ERC-20 only, skip for native ETH)**
```csharp
await uniswap.Accounts.Approvals.CheckAndApproveIfNeededAsync(
    tokenAddress, owner, spender, amountIn);
```

**Step 3: Build and execute the swap via Universal Router**
```csharp
var v4ActionBuilder = uniswap.GetUniversalRouterV4ActionsBuilder();

v4ActionBuilder.AddCommand(new SwapExactIn
{
    CurrencyIn = eth,
    AmountIn = amountIn,
    AmountOutMinimum = quote.AmountOut * 95 / 100, // 5% slippage
    Path = pathKeys.MapToActionV4()
});

v4ActionBuilder.AddCommand(new SettleAll { Currency = eth, Amount = amountIn });
v4ActionBuilder.AddCommand(new TakeAll { Currency = usdc, MinAmount = 0 });

var routerBuilder = new UniversalRouterBuilder();
routerBuilder.AddCommand(v4ActionBuilder.GetV4SwapCommand());

var executeFunction = routerBuilder.GetExecuteFunction(amountIn);
var receipt = await uniswap.UniversalRouter.ExecuteRequestAndWaitForReceiptAsync(executeFunction);
```

**Step 4: Verify transaction success**
```csharp
if (receipt.Status.Value != 1)
    throw new Exception($"Swap failed — tx: {receipt.TransactionHash}");
```

## Slippage and Price Impact

Nethereum provides calculators for both:

```csharp
var slippage = uniswap.Pricing.SlippageCalculator;

// For exact-input swaps: minimum acceptable output
var tolerance = new BigDecimal(0.5m); // 0.5%
var minAmountOut = slippage.CalculateMinimumAmountOut(expectedAmountOut, tolerance);

// For exact-output swaps: maximum you're willing to pay
var maxAmountIn = slippage.CalculateMaximumAmountIn(expectedAmountIn, tolerance);

// Price impact classification
var impactCalc = uniswap.Pricing.PriceImpactCalculator;
var impact = impactCalc.CalculatePriceImpact(input, output, midPrice);
var level = impactCalc.ClassifyPriceImpact(impact);
// Returns: Low (<1%), Medium (1-3%), High (3-5%), Critical (>5%)
var warning = impactCalc.GetPriceImpactWarning(level);
```

## Price Calculations

Calculate human-readable prices from the pool's raw `sqrtPriceX96` value:

```csharp
// Get price of token0 in terms of token1 (adjusted for decimals)
var priceToken0InToken1 = uniswap.Pricing.PriceCalculator.CalculatePriceFromSqrtPriceX96(
    sqrtPriceX96, decimals0: 18, decimals1: 6);

// Inverse gives token1 price in terms of token0
var priceToken1InToken0 = priceToken0InToken1 == 0 ? 0 : 1 / priceToken0InToken1;
```

Tick math conversions for working with price ranges:

```csharp
var sqrtPriceX96 = uniswap.Math.Tick.GetSqrtRatioAtTick(tick);
var tick = uniswap.Math.Tick.GetTickAtSqrtRatio(sqrtPriceX96);
```

## Pool Discovery

Find available pools for a token pair using on-chain Initialize events:

```csharp
var poolCache = uniswap.Pools.Cache;
var pools = await poolCache.FindPoolsForTokenAsync(token: usdc, fromBlockNumber: start, toBlockNumber: end);
```

Or fetch a specific pool directly:

```csharp
var pool = await poolCache.GetOrFetchPoolAsync(currency0: eth, currency1: usdc, fee: 500, tickSpacing: 10);
```

## Find Best Path

For large trades or exotic pairs, route through intermediate tokens for better prices:

```csharp
var pathFinder = uniswap.Pricing.QuotePricePathFinder;
var cachedPools = await poolCache.GetAllCachedPoolsAsync();

// Direct path only
var bestDirect = await pathFinder.FindBestDirectPathAsync(
    tokenIn: eth, tokenOut: usdc,
    amountIn: Web3.Convert.ToWei(1),
    candidatePools: cachedPools);

// Multi-hop through intermediate tokens
var bestPath = await pathFinder.FindBestPathAsync(
    tokenIn: usdc, tokenOut: weth,
    amountIn: Web3.Convert.ToWei(1000, UnitConversion.EthUnit.Mwei),
    intermediateTokens: new[] { dai, eth },
    candidatePools: cachedPools);
```

## Token Approvals

Before swapping ERC-20 tokens (not native ETH), ensure the router has approval:

```csharp
await uniswap.Accounts.Approvals.CheckAndApproveIfNeededAsync(
    tokenAddress, owner, spender, requiredAmount);

// Or check and approve separately
var status = await uniswap.Accounts.Approvals.CheckApprovalAsync(
    tokenAddress, owner, spender, requiredAmount);
if (!status.IsApproved)
{
    await uniswap.Accounts.Approvals.ApproveAsync(
        tokenAddress, spender, AccountApprovalService.GetMaxApprovalAmount());
}
```

## Validate Balances

Before executing a swap, verify the sender has enough tokens:

```csharp
var balanceResult = await uniswap.Accounts.Balances.ValidateBalanceAsync(
    tokenAddress, owner, requiredAmount);

if (!balanceResult.HasSufficientBalance)
    Console.WriteLine($"Need {balanceResult.Deficit} more tokens");
```

## V3 Swaps with Permit2

For Uniswap V3, swaps use the same Universal Router but with Permit2 for gasless approvals. The flow: approve Permit2 once, then sign off-chain permits per swap:

```csharp
// One-time: approve Permit2 to spend your WETH
var weth9Service = web3.Eth.ERC20.GetContractService(weth9);
await weth9Service.ApproveRequestAndWaitForReceiptAsync(permit2Address, IntType.MAX_INT256_VALUE);

// Per-swap: sign a Permit2 authorization
var permit = new PermitSingle
{
    Spender = universalRouterAddress,
    SigDeadline = 2000000000,
    Details = new PermitDetails
    {
        Amount = amountIn * 100000,
        Expiration = 0,
        Nonce = 0,
        Token = weth9
    }
};

var permitService = new Permit2Service(web3, permit2Address);
var signedPermit = await permitService.GetSinglePermitWithSignatureAsync(
    permit, new EthECKey(privateKey));

// Build the swap via Universal Router
var planner = new UniversalRouterBuilder();
planner.AddCommand(new Permit2PermitCommand
{
    Permit = signedPermit.PermitRequest,
    Signature = signedPermit.GetSignatureBytes()
});
planner.AddCommand(new V3SwapExactInCommand
{
    AmountIn = amountIn,
    AmountOutMinimum = quote.AmountOut - 10000,
    Path = path,
    Recipient = account.Address,
    FundsFromPermit2OrUniversalRouter = true
});

var receipt = await universalRouterService.ExecuteRequestAndWaitForReceiptAsync(
    planner.GetExecuteFunction(amountIn));
```

## Error Handling

Uniswap reverts with custom errors that the router service can decode:

```csharp
catch (SmartContractCustomErrorRevertException e)
{
    var error = universalRouterService.FindCustomErrorException(e);
    if (error != null) Console.WriteLine($"Uniswap error: {error.Message}");
}
```

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-uniswap-swap
