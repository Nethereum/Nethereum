using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class FinalityLabelResolver
    {
        public static BigInteger Resolve(string blockTag, IFinalityCursorProvider cursor, BigInteger latest)
        {
            if (blockTag == BlockParameter.BlockParameterType.finalized.ToString())
            {
                return cursor?.GetFinalizedBlockNumber() is BigInteger fin ? fin : latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.safe.ToString())
            {
                return cursor?.GetSafeBlockNumber() is BigInteger safe ? safe : latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.latest.ToString() ||
                blockTag == BlockParameter.BlockParameterType.pending.ToString())
            {
                return latest;
            }

            if (blockTag == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return 0;
            }

            return blockTag.HexToBigInteger(false);
        }
    }
}
