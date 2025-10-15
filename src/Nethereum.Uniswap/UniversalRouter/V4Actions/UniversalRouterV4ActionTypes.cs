using Nethereum.Uniswap.UniversalRouter.Commands;
using System;
using System.Collections.Generic;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    //https://github.com/Uniswap/v4-periphery/blob/main/src/libraries/Actions.sol
    public enum UniversalRouterV4ActionTypes
    {
        INCREASE_LIQUIDITY = 0x00,
        DECREASE_LIQUIDITY = 0x01,
        MINT_POSITION = 0x02,
        BURN_POSITION = 0x03,
        INCREASE_LIQUIDITY_FROM_DELTAS = 0x04,
        MINT_POSITION_FROM_DELTAS = 0x05,

        SWAP_EXACT_IN_SINGLE = 0x06,
        SWAP_EXACT_IN = 0x07,
        SWAP_EXACT_OUT_SINGLE = 0x08,
        SWAP_EXACT_OUT = 0x09,

        SETTLE = 0x0b,
        SETTLE_ALL = 0x0c,
        SETTLE_PAIR = 0x0d,

        TAKE = 0x0e,
        TAKE_ALL = 0x0f,
        TAKE_PORTION = 0x10,
        TAKE_PAIR = 0x11,

        CLOSE_CURRENCY = 0x12,
        CLEAR_OR_TAKE = 0x13,
        SWEEP = 0x14,

        WRAP = 0x15,
        UNWRAP = 0x16,

        MINT_6909 = 0x17,
        BURN_6909 = 0x18,
    }

    public class UniversalRouterV4ActionDecoder
    {
        public T Decode<T>(byte[] data) where T : V4ActionRouterCommand
        {
            var command = (T)Activator.CreateInstance(typeof(T));
            command.DecodeInputData(data);
            return command;
        }

        public List<UniversalRouterCommand> Decode(byte[] commands, List<byte[]> inputs)
        {
            var decodedCommands = new List<UniversalRouterCommand>();
            for (int i = 0; i < inputs.Count; i++)
            {
                decodedCommands.Add(Decode(commands[i], inputs[i]));
            }

            return decodedCommands;
        }

        public V4ActionRouterCommand Decode(byte command, byte[] data)
        {
            switch (command)
            {
                case (byte)UniversalRouterV4ActionTypes.INCREASE_LIQUIDITY:
                    return Decode<IncreaseLiquidity>(data);
                case (byte)UniversalRouterV4ActionTypes.DECREASE_LIQUIDITY:
                    return Decode<DecreaseLiquidity>(data);
                case (byte)UniversalRouterV4ActionTypes.MINT_POSITION:
                    return Decode<MintPosition>(data);
                case (byte)UniversalRouterV4ActionTypes.BURN_POSITION:
                    return Decode<BurnPosition>(data);
                case (byte)UniversalRouterV4ActionTypes.INCREASE_LIQUIDITY_FROM_DELTAS:
                    return Decode<IncreaseLiquidityFromDeltas>(data);
                case (byte)UniversalRouterV4ActionTypes.MINT_POSITION_FROM_DELTAS:
                    return Decode<MintPositionFromDeltas>(data);
                case (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_IN_SINGLE:
                    return Decode<SwapExactInSingle>(data);
                case (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_IN:
                    return Decode<SwapExactIn>(data);
                case (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_OUT_SINGLE:
                    return Decode<SwapExactOutSingle>(data);
                case (byte)UniversalRouterV4ActionTypes.SWAP_EXACT_OUT:
                    return Decode<SwapExactOut>(data);
                case (byte)UniversalRouterV4ActionTypes.SETTLE:
                    return Decode<Settle>(data);
                case (byte)UniversalRouterV4ActionTypes.SETTLE_ALL:
                    return Decode<SettleAll>(data);
                case (byte)UniversalRouterV4ActionTypes.SETTLE_PAIR:
                    return Decode<SettlePair>(data);
                case (byte)UniversalRouterV4ActionTypes.TAKE:
                    return Decode<Take>(data);
                case (byte)UniversalRouterV4ActionTypes.TAKE_ALL:
                    return Decode<TakeAll>(data);
                case (byte)UniversalRouterV4ActionTypes.TAKE_PORTION:
                    return Decode<TakePortion>(data);
                case (byte)UniversalRouterV4ActionTypes.TAKE_PAIR:
                    return Decode<TakePair>(data);
                case (byte)UniversalRouterV4ActionTypes.CLOSE_CURRENCY:
                    return Decode<CloseCurrency>(data);
                case (byte)UniversalRouterV4ActionTypes.SWEEP:
                    return Decode<Sweep>(data);
                case (byte)UniversalRouterV4ActionTypes.WRAP:
                    return Decode<Wrap>(data);
                case (byte)UniversalRouterV4ActionTypes.UNWRAP:
                    return Decode<Unwrap>(data);
                default:
                    return new V4UnknownAction() { CommandType = command, Data = data };
            }
        }
    }

    public class V4UnknownAction : V4ActionRouterCommand
    {
        public override byte CommandType { get; set; } = 0;
        public byte[] Data { get; set; } = new byte[0];
        public override void DecodeInputData(byte[] data)
        {
            Data = data;
        }
        public override byte[] GetInputData()
        {
            return Data;
        }
    }

}

