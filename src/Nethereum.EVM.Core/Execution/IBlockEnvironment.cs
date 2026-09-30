using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public interface IBlockEnvironment
    {
        EvmUInt256 BlockNumber { get; }
        EvmUInt256 Timestamp { get; }
        string Coinbase { get; }
        EvmUInt256 BaseFee { get; }

        EvmUInt256 Difficulty { get; }

        EvmUInt256 BlockGasLimit { get; }
        EvmUInt256 ChainId { get; }
        EvmUInt256 ExcessBlobGas { get; }

        ulong? SlotNumber { get; }
    }
}
