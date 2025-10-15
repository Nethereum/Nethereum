using System;
using System.Collections.Generic;


namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class UniversalRouterDecoder
    {
        public T Decode<T>(byte[] data) where T : UniversalRouterCommand
        {
            var command = (T)Activator.CreateInstance(typeof(T));
            command.DecodeInputData(data);
            return command;
        }

        public T DecodeRevertable<T>(byte[] data, bool revertable = false) where T : UniversalRouterCommandRevertable
        {
            var command = Decode<T>(data);
            command.AllowRevert = revertable;
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

        public UniversalRouterCommand Decode(byte command, byte[] data)
        {
            switch (command)
            {
                case (byte)UniversalRouterCommandType.V3_SWAP_EXACT_IN:
                    return Decode<V3SwapExactInCommand>(data);
                case (byte)UniversalRouterCommandType.V3_SWAP_EXACT_OUT:
                    return Decode<V3SwapExactOutCommand>(data);
                case (byte)UniversalRouterCommandType.PERMIT2_TRANSFER_FROM:
                    return Decode<Permit2TransferFromCommand>(data);
                case (byte)UniversalRouterCommandType.PERMIT2_PERMIT_BATCH:
                    return DecodeRevertable<Permit2PermitBatchCommand>(data);
                case (byte)UniversalRouterCommandType.SWEEP:
                    return Decode<SweepCommand>(data);
                case (byte)UniversalRouterCommandType.TRANSFER:
                    return Decode<TransferCommand>(data);
                case (byte)UniversalRouterCommandType.PAY_PORTION:
                    return Decode<PayPortionCommand>(data);
                case (byte)UniversalRouterCommandType.V2_SWAP_EXACT_IN:
                    return Decode<V2SwapExactInCommand>(data);
                case (byte)UniversalRouterCommandType.V2_SWAP_EXACT_OUT:
                    return Decode<V2SwapExactOutCommand>(data);
                case (byte)UniversalRouterCommandType.PERMIT2_PERMIT:
                    return DecodeRevertable<Permit2PermitCommand>(data);
                case (byte)UniversalRouterCommandType.WRAP_ETH:
                    return Decode<WrapEthCommand>(data);
                case (byte)UniversalRouterCommandType.UNWRAP_WETH:
                    return Decode<UnwrapWethCommand>(data);
                case (byte)UniversalRouterCommandType.PERMIT2_TRANSFER_FROM_BATCH:
                    return Decode<Permit2TransferFromBatchCommand>(data);

                //revertable

                case (byte) UniversalRouterCommandType.PERMIT2_PERMIT_BATCH | UniversalRouterCommandRevertable.ALLOW_REVERT_FLAG:
                    return DecodeRevertable<Permit2PermitBatchCommand>(data, true);
                case (byte)UniversalRouterCommandType.PERMIT2_PERMIT | UniversalRouterCommandRevertable.ALLOW_REVERT_FLAG:
                    return DecodeRevertable<Permit2PermitCommand>(data, true);
                
                default:
                    return new UnknownCommand() { CommandType = command, Data = data };
            }
        }
    }
    
}
