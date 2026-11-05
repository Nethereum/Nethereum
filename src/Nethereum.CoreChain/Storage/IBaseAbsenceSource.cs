using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IBaseAbsenceSource
    {
        bool WasAbsentInBase(EvmAddress address);
    }
}
