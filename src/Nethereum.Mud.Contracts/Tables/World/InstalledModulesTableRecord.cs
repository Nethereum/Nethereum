using Nethereum.ABI.FunctionEncoding.Attributes;
using System;
using static Nethereum.Mud.Contracts.Tables.World.InstalledModulesTableRecord;

namespace Nethereum.Mud.Contracts.Tables.World
{

    /*
      InstalledModules: {
      schema: {
        moduleAddress: "address",
        argumentsHash: "bytes32", // Hash of the params passed to the `install` function
        isInstalled: "bool",
      },
      key: ["moduleAddress", "argumentsHash"],
    },
    */
    public class InstalledModulesTableRecord : TableRecord<InstalledModulesKey, InstalledModulesValue>
    {
        public InstalledModulesTableRecord() : base("world", "InstalledModules")
        {
        }

        public class InstalledModulesKey
        {
            [Parameter("address", "moduleAddress", 1)]
            public string ModuleAddress { get; set; }

            [Parameter("bytes32", "argumentsHash", 2)]
            public byte[] ArgumentsHash { get; set; }
        }

        public class InstalledModulesValue
        {
            [Parameter("bool", "isInstalled", 1)]
            public bool IsInstalled { get; set; }
        }
    }
}




