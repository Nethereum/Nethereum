using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    internal static class BlockTagResolver
    {
        public static async Task<BigInteger?> TryResolveNamedAsync(string tag, RpcContext context)
        {
            if (tag == BlockParameter.BlockParameterType.safe.ToString() ||
                tag == BlockParameter.BlockParameterType.finalized.ToString())
            {
                var cursor = context.Services != null ? context.GetService<IFinalityCursorProvider>() : null;
                var latest = await context.Node.GetBlockNumberAsync();
                return FinalityLabelResolver.Resolve(tag, cursor, latest);
            }

            if (tag == BlockParameter.BlockParameterType.latest.ToString() ||
                tag == BlockParameter.BlockParameterType.pending.ToString())
                return await context.Node.GetBlockNumberAsync();

            if (tag == BlockParameter.BlockParameterType.earliest.ToString())
                return 0;

            return null;
        }

        /// <summary>EIP-1898: a block parameter given as a 32-byte hex string (0x + 64 hex digits) is a block HASH, not a quantity.</summary>
        public static bool IsBlockHash(string tag) =>
            tag != null && tag.Length == 66 && tag.StartsWith("0x");

        public static async Task<BigInteger?> TryResolveBlockHashAsync(string tag, RpcContext context)
        {
            var header = await context.Node.GetBlockByHashAsync(tag.HexToByteArray());
            return header != null ? (BigInteger?)header.BlockNumber : null;
        }

        public static async Task<BigInteger> ResolveAsync(string tag, RpcContext context)
        {
            var named = await TryResolveNamedAsync(tag, context);
            if (named.HasValue) return named.Value;

            if (tag == null || !tag.StartsWith("0x"))
                throw RpcException.InvalidParams("invalid argument 0: hex string without 0x prefix");

            if (IsBlockHash(tag))
            {
                var byHash = await TryResolveBlockHashAsync(tag, context);
                if (byHash.HasValue) return byHash.Value;
                throw RpcException.InvalidParams("block not found");
            }

            return tag.HexToBigInteger(false);
        }
    }
}
