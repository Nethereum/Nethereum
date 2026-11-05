using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    internal static class TestingBlockOptions
    {
        private const long GasLimitBoundDivisor = 1024;
        private const long MinGasLimit = 5000;

        public static BlockProductionOptions Create(
            DevChainNode node, BlockHeader parent, PayloadAttributesV3 attributes, string extraData)
        {
            return new BlockProductionOptions
            {
                Timestamp = (long)attributes.Timestamp.Value,
                Coinbase = attributes.SuggestedFeeRecipient,
                BaseFee = Model.BaseFeeCalculator
                    .CalculateExpectedBaseFeePerGas(parent.BaseFee, parent.GasLimit, parent.GasUsed).ToBigInteger(),
                BlockGasLimit = CalcGasLimit(parent.GasLimit, node.Config.TargetGasLimit),
                Difficulty = 0,
                PrevRandao = attributes.PrevRandao.HexToByteArray(),
                ExtraData = extraData?.HexToByteArray() ?? new byte[0],
                ChainId = node.Config.ChainId,
                ParentBeaconBlockRoot = attributes.ParentBeaconBlockRoot?.HexToByteArray(),
                Withdrawals = CoreChain.Engine.EnginePayloadDecoder.ToModelWithdrawals(attributes.Withdrawals)
            };
        }

        private static long CalcGasLimit(long parentGasLimit, long gasCeil)
        {
            var delta = parentGasLimit / GasLimitBoundDivisor - 1;
            var desired = gasCeil < MinGasLimit ? MinGasLimit : gasCeil;
            if (parentGasLimit < desired) return System.Math.Min(parentGasLimit + delta, desired);
            if (parentGasLimit > desired) return System.Math.Max(parentGasLimit - delta, desired);
            return parentGasLimit;
        }
    }
}
