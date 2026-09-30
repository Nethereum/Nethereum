using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>Simulation coverage: eth_call, estimateGas, and createAccessList against the USDT contract.</summary>
    internal static class SimGroup
    {
        private const string Group = "sim";

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                var block = new BlockParameter(new HexBigInteger(n));

                var totalSupplyCall = new CallInput { To = Erc20Fixtures.UsdtAddress, Data = Erc20Fixtures.TotalSupplySelector };
                await ctx.Runner.RunAsync(Group, "Call/totalSupply", $"{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.Transactions.Call.SendRequestAsync(totalSupplyCall, block),
                    () => ctx.NodeY.Eth.Transactions.Call.SendRequestAsync(totalSupplyCall, block)).ConfigureAwait(false);

                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference) || reference.Transactions.Length == 0)
                    continue;

                var busyAddress = reference.Transactions[0].From;
                var transferData = BuildTransferData(Erc20Fixtures.BurnAddress, BigInteger.Zero);

                var estimateGasCall = new CallInput { From = busyAddress, To = Erc20Fixtures.UsdtAddress, Data = transferData };
                await ctx.Runner.RunAsync(Group, "EstimateGas/transfer", $"{busyAddress}->{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.Transactions.EstimateGas.SendRequestAsync(estimateGasCall),
                    () => ctx.NodeY.Eth.Transactions.EstimateGas.SendRequestAsync(estimateGasCall)).ConfigureAwait(false);

                var accessListInput = new TransactionInput { From = busyAddress, To = Erc20Fixtures.UsdtAddress, Data = transferData };
                await ctx.Runner.RunAsync(Group, "CreateAccessList/transfer", $"{busyAddress}->{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Eth.CreateAccessList.SendRequestAsync(accessListInput, block),
                    () => ctx.NodeY.Eth.CreateAccessList.SendRequestAsync(accessListInput, block)).ConfigureAwait(false);
            }
        }

        private static string BuildTransferData(string recipient, BigInteger amount)
        {
            var recipientHex = recipient.Substring(2).PadLeft(64, '0');
            var amountHex = amount.ToString("x").PadLeft(64, '0');
            return Erc20Fixtures.TransferSelector + recipientHex + amountHex;
        }
    }
}
