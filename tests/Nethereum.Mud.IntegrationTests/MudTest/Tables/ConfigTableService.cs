using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;


namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{
    public class ConfigTableService : TableSingletonService<ConfigTableRecord, ConfigTableRecord.ConfigValue>
    {
        public ConfigTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }
}
