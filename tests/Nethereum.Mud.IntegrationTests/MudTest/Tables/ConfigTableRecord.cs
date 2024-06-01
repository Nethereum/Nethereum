using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Mud;
using Nethereum.Mud.Contracts.World.Tables;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{

    public class ConfigTableRecord:TableRecordSingleton<ConfigTableRecord.ConfigValue>
    {
        public ConfigTableRecord() : base("Config")
        {
        }

        public class ConfigValue
        {
            [Parameter("uint256[7]", "config1", 1)]
            public List<BigInteger> Config1 { get; set; }

            [Parameter("uint256[3]", "config2", 2)]
            public List<BigInteger> Config2 { get; set; }

            [Parameter("uint256[]", "config3", 3)]
            public List<BigInteger> Config3 { get; set; }

            
        }
       
    }
}
