using Nethereum.Web3;
using System.Collections.Generic;

namespace Nethereum.Mud.Contracts.Core.Tables
{
    public class EmptyTablesServices : TablesServices
    {
        public EmptyTablesServices(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
            TableServices = new List<ITableServiceBase>();
        }
    }
}
