using System;
using System.Collections.Generic;

namespace Nethereum.Mud
{
    public class TableTypeRegistry
    {
        public static Dictionary<string, Type> TableTypes = new Dictionary<string, Type>();

        public static void RegisterTableType(string tableIdHex, Type tableType)
        {
            TableTypes[tableIdHex] = tableType;
        }

        public static Type GetTableType(string tableIdHex)
        {
            return TableTypes[tableIdHex];
        }
    }

    public class TableIdRegistry
    {
        public static Dictionary<Type, byte[]> TableIds = new Dictionary<Type, byte[]>();

        public static void RegisterTableId<TTable>() where TTable : ITableRecordSingleton, new()
        {
            if (!TableIds.ContainsKey(typeof(TTable)))
            {
                TableIds[typeof(TTable)] = new TTable().ResourceId;
            }
        }

        public static byte[] GetTableId<TTable>() where TTable : ITableRecordSingleton, new()
        {
            if(!TableIds.ContainsKey(typeof(TTable)))
            {
                TableIds[typeof(TTable)] = new TTable().ResourceId;
            }

            return TableIds[typeof(TTable)];
        }
        
    }
}
