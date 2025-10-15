using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Uniswap.V4.PositionManager.ContractDefinition;
using Nethereum.Util;
using System.Linq;
using System.Numerics;

namespace Nethereum.Uniswap.V4
{
    public static class V4PositionReceiptHelper
    {
        public static BigInteger GetMintedTokenId(TransactionReceipt receipt, string positionManagerAddress)
        {
            var transferEvent = receipt.DecodeAllEvents<TransferEventDTO>()
                .FirstOrDefault(e => e.Event.From == AddressUtil.ZERO_ADDRESS &&
                                    e.Log.Address.Equals(positionManagerAddress, System.StringComparison.OrdinalIgnoreCase));

            if (transferEvent == null)
                throw new System.Exception("No Transfer event found in receipt for minting");

            return transferEvent.Event.Id;
        }
    }
}
