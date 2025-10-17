using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition;
using Nethereum.Util;
using System.Linq;
using System.Numerics;

namespace Nethereum.Uniswap.V4.Positions
{
    public static class PositionReceiptExtension
    {
        public static BigInteger GetMintedTokenId(this TransactionReceipt receipt, string positionManagerAddress)
        {
            var transferEvent = receipt.DecodeAllEvents<TransferEventDTO>()
                .FirstOrDefault(e => e.Event.From == AddressUtil.ZERO_ADDRESS &&
                                    e.Log.Address.IsTheSameAddress(positionManagerAddress));

            if (transferEvent == null)
                throw new System.Exception("No Transfer event found in receipt for minting");

            return transferEvent.Event.Id;
        }
    }
}
