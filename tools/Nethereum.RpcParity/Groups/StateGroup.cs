using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>Flat-state coverage: balance, code, and a storage slot on the USDT contract.</summary>
    internal static class StateGroup
    {
        private const string Group = "state";
        private static readonly HexBigInteger SlotZero = new HexBigInteger(0);

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                var block = new BlockParameter(new HexBigInteger(n));

                await ctx.Runner.RunAsync(Group, "GetBalance", $"{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.GetBalance.SendRequestAsync(Erc20Fixtures.UsdtAddress, block),
                    () => ctx.NodeY.Eth.GetBalance.SendRequestAsync(Erc20Fixtures.UsdtAddress, block)).ConfigureAwait(false);

                await ctx.Runner.RunAsync(Group, "GetCode", $"{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.GetCode.SendRequestAsync(Erc20Fixtures.UsdtAddress, block),
                    () => ctx.NodeY.Eth.GetCode.SendRequestAsync(Erc20Fixtures.UsdtAddress, block)).ConfigureAwait(false);

                await ctx.Runner.RunAsync(Group, "GetStorageAt/slot0", $"{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.GetStorageAt.SendRequestAsync(Erc20Fixtures.UsdtAddress, SlotZero, block),
                    () => ctx.NodeY.Eth.GetStorageAt.SendRequestAsync(Erc20Fixtures.UsdtAddress, SlotZero, block)).ConfigureAwait(false);
            }
        }
    }
}
