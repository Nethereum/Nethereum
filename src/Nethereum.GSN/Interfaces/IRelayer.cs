using Nethereum.GSN.Models;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.GSN.Interfaces
{
    public interface IRelayer
    {
        Task<string> Relay(TransactionInput transaction, Func<Relay, TransactionInput, string, Task<string>> relayFunc);
    }
}