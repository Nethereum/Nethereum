using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{

    public interface IEventDTO
    {

    }

    public interface IFunctionOutputDTO
    {

    }

    public interface IEventLog
    {
        FilterLog Log { get; }
    }
}