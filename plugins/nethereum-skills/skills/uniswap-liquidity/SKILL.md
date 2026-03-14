---
name: uniswap-liquidity
description: Manage Uniswap V4 liquidity positions using Nethereum (.NET/C#). Use this skill whenever the user asks about providing liquidity, LP positions, concentrated liquidity, collecting fees, minting/burning positions, rebalancing, or any Uniswap liquidity operation with C# or .NET.
user-invocable: true
---

# Uniswap: Manage Liquidity

Providing liquidity on Uniswap V4 means depositing tokens into a concentrated price range to earn trading fees. Nethereum's Position Manager service handles the full lifecycle: mint, increase, decrease, collect fees, rebalance, and burn. Each position is an ERC-721 NFT with a unique token ID.

NuGet: `Nethereum.Uniswap`

```bash
dotnet add package Nethereum.Uniswap
```

## Connect to the Position Manager

All position operations use the actions builder pattern — you build a list of actions, then submit them in a single transaction:

```csharp
var uniswap = web3.UniswapV4();
var positionManager = uniswap.Positions.Manager;
```

## Create a Position

Define the pool, tick range, and liquidity amount. `MintPosition` creates the NFT and deposits tokens:

```csharp
var actionsBuilder = uniswap.Positions.CreatePositionManagerActionsBuilder();

actionsBuilder.AddCommand(new MintPosition
{
    PoolKey = poolKey,
    TickLower = -600,
    TickUpper = 600,
    Liquidity = Web3.Convert.ToWei(0.01m),
    Amount0Max = Web3.Convert.ToWei(0.1m),
    Amount1Max = Web3.Convert.ToWei(300, UnitConversion.EthUnit.Mwei),
    Recipient = account,
    HookData = new byte[0]
});

actionsBuilder.AddCommand(new SettlePair { Currency0 = eth, Currency1 = usdc });

var receipt = await positionManager.ModifyLiquiditiesRequestAndWaitForReceiptAsync(
    new ModifyLiquiditiesFunction
    {
        UnlockData = actionsBuilder.GetUnlockData(),
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
        AmountToSend = Web3.Convert.ToWei(0.1m)
    });

var tokenId = V4PositionReceiptHelper.GetMintedTokenId(receipt, positionManagerAddress);
```

`Amount0Max`/`Amount1Max` are slippage protection — the tx reverts if the pool price moves too much. After minting, extract the NFT token ID from the receipt.

## Query Position Info

```csharp
var liquidity = await positionManager.GetPositionLiquidityQueryAsync(tokenId);
var info = await positionManager.GetPoolAndPositionInfoQueryAsync(tokenId);
var decoded = uniswap.Positions.PositionInfoDecoder.DecodePositionInfo(
    await positionManager.PositionInfoQueryAsync(tokenId));
Console.WriteLine($"Range: tick {decoded.TickLower} to {decoded.TickUpper}");

var owner = await positionManager.OwnerOfQueryAsync(tokenId);
```

## Calculate Position Value

See how much each token your position currently holds:

```csharp
var value = await uniswap.Positions.PositionValueCalculator.GetPositionValueAsync(
    tokenId, token0Decimals: 18, token1Decimals: 6);
Console.WriteLine($"ETH: {Web3.Convert.FromWei(value.Amount0, 18)}, USDC: {Web3.Convert.FromWei(value.Amount1, 6)}");
```

## Calculate Token Amounts from Liquidity

Convert between liquidity units and token amounts for precise control:

```csharp
var amounts = uniswap.Positions.LiquidityCalculator.GetAmountsForLiquidityByTicks(
    sqrtPriceX96, tickLower, tickUpper, liquidity);

var amount0 = amounts.Item1; // Token0 amount in wei
var amount1 = amounts.Item2; // Token1 amount in wei
```

## Increase Liquidity

Add more tokens to an existing position (same range):

```csharp
actionsBuilder.AddCommand(new IncreaseLiquidity
{
    TokenId = tokenId,
    Liquidity = Web3.Convert.ToWei(0.005m),
    Amount0Max = Web3.Convert.ToWei(0.05m),
    Amount1Max = Web3.Convert.ToWei(150, UnitConversion.EthUnit.Mwei),
    HookData = new byte[0]
});
actionsBuilder.AddCommand(new SettlePair { Currency0 = eth, Currency1 = usdc });
```

## Decrease Liquidity

Remove liquidity and withdraw tokens:

```csharp
actionsBuilder.AddCommand(new DecreaseLiquidity
{
    TokenId = tokenId,
    Liquidity = Web3.Convert.ToWei(0.005m),
    Amount0Min = 0, Amount1Min = 0,
    HookData = new byte[0]
});
actionsBuilder.AddCommand(new TakePair { Currency0 = eth, Currency1 = usdc, Recipient = account });
```

After decreasing, use `TakePair` (not `SettlePair`) to receive the withdrawn tokens.

## Collect Fees

Call `DecreaseLiquidity` with `Liquidity = 0` to collect accumulated fees without removing liquidity:

```csharp
actionsBuilder.AddCommand(new DecreaseLiquidity
{
    TokenId = tokenId,
    Liquidity = 0,  // Zero = fees only
    Amount0Min = 0, Amount1Min = 0,
    HookData = new byte[0]
});
actionsBuilder.AddCommand(new TakePair { Currency0 = eth, Currency1 = usdc, Recipient = account });
```

You can batch fee collection from multiple positions in a single transaction by adding multiple `DecreaseLiquidity` commands before the `TakePair`.

## Atomic Rebalancing

Close an old position and open a new one with a different range in a single atomic transaction:

```csharp
// Close old
actionsBuilder.AddCommand(new DecreaseLiquidity { TokenId = oldId, Liquidity = oldLiquidity, ... });
// Open new
actionsBuilder.AddCommand(new MintPosition { PoolKey = poolKey, TickLower = -1200, TickUpper = 1200, ... });
// Settle net difference
actionsBuilder.AddCommand(new CloseCurrency { Currency = eth });
actionsBuilder.AddCommand(new CloseCurrency { Currency = usdc });
```

`CloseCurrency` handles the net settlement — if the old position returns more than the new needs, you receive the difference.

## Burn a Position

Remove all liquidity and destroy the NFT:

```csharp
actionsBuilder.AddCommand(new DecreaseLiquidity { TokenId = tokenId, Liquidity = totalLiquidity, ... });
actionsBuilder.AddCommand(new BurnPosition { TokenId = tokenId, Amount0Min = 0, Amount1Min = 0, HookData = new byte[0] });
actionsBuilder.AddCommand(new TakePair { Currency0 = eth, Currency1 = usdc, Recipient = account });
```

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-uniswap-liquidity
