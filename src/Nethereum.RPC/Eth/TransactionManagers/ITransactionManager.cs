using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public interface ITransactionManager
    {
        IClient Client { get; set; }
        Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput;
        Task<HexBigInteger> EstimateGasAsync<T>(T callInput) where T : CallInput;
    }
}
