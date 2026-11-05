using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public interface IAddressAccessRecorder : IStateAccessRecorder
    {
        void RecordAccountRead(EvmAddress address);

        void RecordStorageRead(EvmAddress address, EvmUInt256 key);
    }
}
